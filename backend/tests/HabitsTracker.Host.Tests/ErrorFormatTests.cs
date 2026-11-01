using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HabitsTracker.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace HabitsTracker.Host.Tests;

// The error format every endpoint under /api shares (contracts/http-api.md, the shared error table; data-model.md,
// "Error model"), observed through the test-only endpoints HostFactory maps inside the host's own pipeline.
public partial class ErrorFormatTests(PostgresFixture postgres)
{
    private const int BodyLimit = 16 * 1024;

    private sealed record Case(string Name, Func<HttpRequestMessage> Request, HttpStatusCode Status, string Code);

    private static readonly Case[] MalformedBodies =
    [
        new("missing fields", () => Post("/api/test/echo", "{}", "application/json"), HttpStatusCode.BadRequest, "invalid-request"),
        new("a field of the wrong type", () => Post("/api/test/echo", """{"text":"a","number":"one"}""", "application/json"),
            HttpStatusCode.BadRequest, "invalid-request"),
        new("not JSON", () => Post("/api/test/echo", "not json", "application/json"), HttpStatusCode.BadRequest, "invalid-request"),
    ];

    private static readonly Case[] OversizeBodies =
    [
        new("16 KB + 1 byte", () => Post("/api/test/echo", EchoJson(BodyLimit + 1), "application/json"),
            HttpStatusCode.RequestEntityTooLarge, "invalid-request"),
        new("16 KB + 1 byte without a length", () => PostWithoutLength("/api/test/echo", EchoJson(BodyLimit + 1)),
            HttpStatusCode.RequestEntityTooLarge, "invalid-request"),
    ];

    private static readonly Case[] NonJsonBodies =
    [
        new("text/plain", () => Post("/api/test/echo", EchoJson(100), "text/plain"), HttpStatusCode.UnsupportedMediaType, "invalid-request"),
        new("a form", () => Post("/api/test/echo", EchoJson(100), "application/x-www-form-urlencoded"),
            HttpStatusCode.UnsupportedMediaType, "invalid-request"),
    ];

    private static readonly Case[] UnknownPathsAndMethods =
    [
        new("an unknown path", () => new HttpRequestMessage(HttpMethod.Get, "/api/unknown"), HttpStatusCode.NotFound, "not-found"),
        new("an unknown path with a body", () => Post("/api/unknown", "{}", "application/json"), HttpStatusCode.NotFound, "not-found"),
        new("an unknown path asked for as HTML", () => new HttpRequestMessage(HttpMethod.Get, "/api/unknown") { Headers = { { "Accept", "text/html" } } },
            HttpStatusCode.NotFound, "not-found"),
        new("another method", () => new HttpRequestMessage(HttpMethod.Post, "/api/locales"), HttpStatusCode.MethodNotAllowed, "invalid-request"),
    ];

    private static readonly Case[] DomainErrors =
    [
        new("InvalidEmail", () => Get("/api/test/domain/InvalidEmail"), HttpStatusCode.BadRequest, "invalid-email"),
        new("InvalidLocale", () => Get("/api/test/domain/InvalidLocale"), HttpStatusCode.BadRequest, "invalid-locale"),
        new("InvalidOneTimeCode", () => Get("/api/test/domain/InvalidOneTimeCode"), HttpStatusCode.BadRequest, "invalid-code-format"),
        new("InvalidSessionToken", () => Get("/api/test/domain/InvalidSessionToken"), HttpStatusCode.Unauthorized, "not-signed-in"),
        new("CorruptRecord", () => Get("/api/test/domain/CorruptRecord"), HttpStatusCode.InternalServerError, "internal-error"),
    ];

    private static readonly Case[] UnexpectedFailures =
    [
        new("an unexpected failure", () => Get("/api/test/throw"), HttpStatusCode.InternalServerError, "internal-error"),
    ];

    [Fact]
    public async Task Malformed_bodies_are_invalid_request() => await AssertCodesAsync(MalformedBodies);

    [Fact]
    public async Task A_body_over_16_KB_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        await AssertCodesAsync(OversizeBodies);

        await using var host = await HostFactory.StartAsync(postgres);
        using var client = host.CreateClient();
        var json = EchoJson(BodyLimit);
        using var request = Post("/api/test/echo", json, "application/json");
        using var response = await client.SendAsync(request, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"exactly 16 KB: {(int)response.StatusCode}; body '{body}'");
        Assert.Equal(json, body);
    }

    [Fact]
    public async Task Only_json_is_accepted() => await AssertCodesAsync(NonJsonBodies);

    [Fact]
    public async Task Unknown_paths_and_methods() => await AssertCodesAsync(UnknownPathsAndMethods);

    [Fact]
    public async Task Domain_errors_map_to_the_error_model() => await AssertCodesAsync(DomainErrors);

    [Fact]
    public async Task An_unexpected_failure_is_internal_error()
    {
        var ct = TestContext.Current.CancellationToken;
        await AssertCodesAsync(UnexpectedFailures);

        await using var host = await HostFactory.StartAsync(postgres);
        var logs = new CapturingLoggerProvider();
        await using var logged = host.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = logged.CreateClient();
        using var response = await client.GetAsync(new Uri("/api/test/throw", UriKind.Relative), ct);

        Assert.DoesNotContain("secret detail", await response.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        // The detail goes to the log instead, with the exception: the handler answers it, so ASP.NET Core does not log it.
        Assert.Contains(logs.Entries, entry => entry.Level == LogLevel.Error && entry.Category.EndsWith(".DomainExceptionHandler", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_problem_carries_a_trace_id()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres);
        using var client = host.CreateClient();
        var traceIds = new HashSet<string>();

        foreach (var @case in MalformedBodies.Concat(OversizeBodies).Concat(NonJsonBodies).Concat(UnknownPathsAndMethods)
                     .Concat(DomainErrors).Concat(UnexpectedFailures))
        {
            using var request = @case.Request();
            using var response = await client.SendAsync(request, ct);
            var problem = await Problem.AssertAsync(response, @case.Status, @case.Code, ct, @case.Name);

            // RFC 9457 with the contract's choices: title is the reason phrase, type its default.
            Assert.True(
                problem.GetProperty("title").GetString() == ReasonPhrase(@case.Status),
                $"{@case.Name}: title {problem.GetProperty("title")}");
            Assert.True(problem.GetProperty("type").GetString() == "about:blank", $"{@case.Name}: type {problem.GetProperty("type")}");

            var traceId = problem.TryGetProperty("traceId", out var value) ? value.GetString() ?? "" : "";
            Assert.True(W3CTraceId().IsMatch(traceId), $"{@case.Name}: traceId '{traceId}'");
            Assert.True(traceIds.Add(traceId), $"{@case.Name}: traceId '{traceId}' is not the request's own");
        }
    }

    private async Task AssertCodesAsync(Case[] cases)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres);
        using var client = host.CreateClient();

        foreach (var @case in cases)
        {
            using var request = @case.Request();
            using var response = await client.SendAsync(request, ct);
            await Problem.AssertAsync(response, @case.Status, @case.Code, ct, @case.Name);
        }
    }

    private static string ReasonPhrase(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => "Bad Request",
        HttpStatusCode.Unauthorized => "Unauthorized",
        HttpStatusCode.NotFound => "Not Found",
        HttpStatusCode.MethodNotAllowed => "Method Not Allowed",
        HttpStatusCode.RequestEntityTooLarge => "Payload Too Large",
        HttpStatusCode.UnsupportedMediaType => "Unsupported Media Type",
        HttpStatusCode.InternalServerError => "Internal Server Error",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    // A body for POST /api/test/echo of exactly the given number of bytes.
    private static string EchoJson(int bytes)
    {
        const string Prefix = "{\"text\":\"";
        const string Suffix = "\",\"number\":1}";
        return Prefix + new string('a', bytes - Prefix.Length - Suffix.Length) + Suffix;
    }

    private static HttpRequestMessage Get(string path) => new(HttpMethod.Get, path);

    private static HttpRequestMessage Post(string path, string body, string mediaType) =>
        new(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    private static HttpRequestMessage PostWithoutLength(string path, string body) =>
        new(HttpMethod.Post, path) { Content = new UnknownLengthContent(Encoding.UTF8.GetBytes(body), "application/json") };

    // The W3C trace id: 32 lowercase hexadecimal characters, not all zeros.
    [GeneratedRegex("^(?!0{32})[0-9a-f]{32}$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex W3CTraceId();

    // A body sent without Content-Length, as a chunked request is.
    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] bytes;

        public UnknownLengthContent(byte[] bytes, string mediaType)
        {
            this.bytes = bytes;
            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
