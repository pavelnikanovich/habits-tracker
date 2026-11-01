using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Identity.Adapters.Storage.Records;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using TimeZone = HabitsTracker.Identity.Core.TimeZone;

namespace HabitsTracker.Host.Tests;

// POST /api/sessions (contracts/http-api.md), over PostgreSQL, with a code requested through the API and read from
// the capturing sender.
public class SignInEndpointTests(PostgresFixture postgres)
{
    private const string Ann = "ann@example.com";
    private const string Bob = "bob@example.com";
    private const string CodeIncorrect = "code-incorrect";
    private const string NewCodeRequired = "new-code-required";
    private const string NotSignedIn = "not-signed-in";
    private static readonly Uri Me = new("/api/me", UriKind.Relative);

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-02T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task A_correct_code_signs_in()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var code = await host.RequestCodeAsync(client, Ann, "pl");

        using var response = await SignInAsync(client, Ann, code, ct: ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """{"email":"ann@example.com","timeZone":"Europe/Warsaw","timeZoneRecognized":true,"locale":"pl"}""",
            await response.Content.ReadAsStringAsync(ct));
        Assert.Matches("^[A-Za-z0-9_-]{43}$", SessionTokenOf(response));
    }

    [Fact]
    public async Task A_wrong_code()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var code = await host.RequestCodeAsync(client, Ann, "pl");

        using var response = await SignInAsync(client, Ann, WrongCode(code), ct: ct);

        await Problem.AssertAsync(response, HttpStatusCode.Unauthorized, CodeIncorrect, ct);
        Assert.False(response.Headers.Contains(HeaderNames.SetCookie));
    }

    // Review Focus 1: an entry at `expires at` is refused.
    [Fact]
    public async Task Code_at_ten_minutes_requires_a_new_code()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var code = await host.RequestCodeAsync(client, Ann, "pl");

        host.Time.Advance(TimeSpan.FromMinutes(10));
        using var response = await SignInAsync(client, Ann, code, ct: ct);

        await Problem.AssertAsync(response, HttpStatusCode.Unauthorized, NewCodeRequired, ct);
    }

    // Review Focus 1: a code is used once.
    [Fact]
    public async Task Reused_code_requires_a_new_code()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var code = await host.RequestCodeAsync(client, Ann, "pl");
        using var first = await SignInAsync(client, Ann, code, ct: ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await SignInAsync(client, Ann, code, ct: ct);

        await Problem.AssertAsync(second, HttpStatusCode.Unauthorized, NewCodeRequired, ct);
    }

    // Review Focus 2: the 5th wrong entry, and any entry after it, requires a new code.
    [Fact]
    public async Task Correct_code_after_five_wrong_entries_requires_a_new_code()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var code = await host.RequestCodeAsync(client, Ann, "pl");

        for (var entry = 1; entry <= 5; entry++)
        {
            using var wrong = await SignInAsync(client, Ann, WrongCode(code), ct: ct);
            await Problem.AssertAsync(wrong, HttpStatusCode.Unauthorized, entry < 5 ? CodeIncorrect : NewCodeRequired, ct);
        }

        using var correct = await SignInAsync(client, Ann, code, ct: ct);
        await Problem.AssertAsync(correct, HttpStatusCode.Unauthorized, NewCodeRequired, ct);
    }

    // Review Focus 2: concurrent entries are counted one after another, so ten at once still allow only five.
    [Fact]
    public async Task Ten_concurrent_wrong_entries_leave_the_code_exhausted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var code = await host.RequestCodeAsync(client, Ann, "pl");

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => SignInAsync(client, Ann, WrongCode(code), ct: ct)));
        var codes = new List<string?>();
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                codes.Add((await ReadAsync(response, ct)).GetProperty("code").GetString());
            }
        }

        Assert.Equal(4, codes.Count(c => c == CodeIncorrect));
        Assert.Equal(6, codes.Count(c => c == NewCodeRequired));
        using var correct = await SignInAsync(client, Ann, code, ct: ct);
        await Problem.AssertAsync(correct, HttpStatusCode.Unauthorized, NewCodeRequired, ct);
    }

    // Review Focus 5: a code is bound to its email.
    [Fact]
    public async Task Code_of_another_email_does_not_sign_in()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var annCode = await host.RequestCodeAsync(client, Ann, "pl");
        var bobCode = await host.RequestCodeAsync(client, Bob, "pl");
        // Two codes are equal one time in a million; another request gives Bob another code.
        while (bobCode == annCode)
            bobCode = await host.RequestCodeAsync(client, Bob, "pl");

        using var crossed = await SignInAsync(client, Bob, annCode, ct: ct);
        await Problem.AssertAsync(crossed, HttpStatusCode.Unauthorized, CodeIncorrect, ct);

        using var own = await SignInAsync(client, Ann, annCode, ct: ct);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    // US-1.1, scenario 5: a new code replaces the earlier one.
    [Fact]
    public async Task An_earlier_code_no_longer_signs_in()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var earlier = await host.RequestCodeAsync(client, Ann, "pl");
        var later = await host.RequestCodeAsync(client, Ann, "pl");
        while (later == earlier)
            later = await host.RequestCodeAsync(client, Ann, "pl");

        using var refused = await SignInAsync(client, Ann, earlier, ct: ct);
        await Problem.AssertAsync(refused, HttpStatusCode.Unauthorized, CodeIncorrect, ct);

        using var signedIn = await SignInAsync(client, Ann, later, ct: ct);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }

    // US-1.2, scenario 3: the second sign-in finds the user of the first, whose time zone and locale it keeps.
    [Fact]
    public async Task Email_case_does_not_matter()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var first = await host.RequestCodeAsync(client, "Ann@example.com", "pl");
        using var signedUp = await SignInAsync(client, "Ann@example.com", first, ct: ct);
        Assert.Equal(HttpStatusCode.OK, signedUp.StatusCode);

        var second = await host.RequestCodeAsync(client, Ann, "pl");
        using var signedIn = await SignInAsync(client, Ann, second, timeZone: "UTC", locale: "en", ct: ct);

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(
            """{"email":"ann@example.com","timeZone":"Europe/Warsaw","timeZoneRecognized":true,"locale":"pl"}""",
            await signedIn.Content.ReadAsStringAsync(ct));
        await using var scope = host.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<UserRecord>().CountAsync(ct));
    }

    [Fact]
    public async Task Malformed_input_counts_no_attempt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var code = await host.RequestCodeAsync(client, Ann, "pl");

        using var badCode = await SignInAsync(client, Ann, "04291a", ct: ct);
        await Problem.AssertAsync(badCode, HttpStatusCode.BadRequest, "invalid-code-format", ct);
        using var badEmail = await SignInAsync(client, "ann", code, ct: ct);
        await Problem.AssertAsync(badEmail, HttpStatusCode.BadRequest, "invalid-email", ct);
        using var badLocale = await SignInAsync(client, Ann, code, locale: "de", ct: ct);
        await Problem.AssertAsync(badLocale, HttpStatusCode.BadRequest, "invalid-locale", ct);

        await using var scope = host.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<OneTimeCodeRecord>().SingleAsync(ct);
        Assert.Equal(0, stored.EntryAttempts);
    }

    // Review Focus 3: the answer to a sign-in does not reveal whether the email has a user.
    [Fact]
    public async Task Sign_in_answer_does_not_reveal_whether_a_user_exists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var locales = scope.ServiceProvider.GetRequiredService<ILocales>();
            await scope.ServiceProvider.GetRequiredService<IUsers>().AddOrGetAsync(
                User.SignUp(Email.Create("known@example.com"), TimeZone.FromBrowser("UTC"), Locale.Create("en", locales), Start), ct);
        }

        using var knownWithoutCode = await SignInAsync(client, "known@example.com", "123456", ct: ct);
        using var unknownWithoutCode = await SignInAsync(client, "unknown@example.com", "123456", ct: ct);
        await Problem.AssertAsync(knownWithoutCode, HttpStatusCode.Unauthorized, NewCodeRequired, ct);
        await AssertSameAnswerAsync(knownWithoutCode, unknownWithoutCode, ct);

        var knownCode = await host.RequestCodeAsync(client, "known@example.com", "pl");
        var unknownCode = await host.RequestCodeAsync(client, "unknown@example.com", "pl");
        using var knownWrong = await SignInAsync(client, "known@example.com", WrongCode(knownCode), ct: ct);
        using var unknownWrong = await SignInAsync(client, "unknown@example.com", WrongCode(unknownCode), ct: ct);
        await Problem.AssertAsync(knownWrong, HttpStatusCode.Unauthorized, CodeIncorrect, ct);
        await AssertSameAnswerAsync(knownWrong, unknownWrong, ct);
    }

    // Research R19, S4: the token is new, never one the request carried, and the session the browser held ends.
    [Fact]
    public async Task A_sign_in_with_a_held_cookie_revokes_it_and_issues_a_new_token()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();

        var madeUp = SessionToken.Generate().Value;
        var annCode = await host.RequestCodeAsync(client, Ann, "pl");
        using var ann = await SignInAsync(client, Ann, annCode, cookie: madeUp, ct: ct);
        Assert.Equal(HttpStatusCode.OK, ann.StatusCode);
        var annToken = SessionTokenOf(ann);
        Assert.NotEqual(madeUp, annToken);

        var bobCode = await host.RequestCodeAsync(client, Bob, "pl");
        using var bob = await SignInAsync(client, Bob, bobCode, cookie: annToken, ct: ct);
        Assert.Equal(HttpStatusCode.OK, bob.StatusCode);
        var bobToken = SessionTokenOf(bob);
        Assert.NotEqual(annToken, bobToken);

        using var annBrowser = SignedInClient.WithCookie(host, annToken);
        using var held = await annBrowser.Client.GetAsync(Me, ct);
        await Problem.AssertAsync(held, HttpStatusCode.Unauthorized, NotSignedIn, ct);
        using var bobBrowser = SignedInClient.WithCookie(host, bobToken);
        using var issued = await bobBrowser.Client.GetAsync(Me, ct);
        Assert.Equal(Bob, (await ReadAsync(issued, ct)).GetProperty("email").GetString());
    }

    [Fact]
    public async Task Every_field_is_required()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        var code = await host.RequestCodeAsync(client, Ann, "pl");

        foreach (var field in new[] { "email", "code", "rememberMe", "timeZone", "locale" })
        {
            var body = Body(Ann, code, rememberMe: false, timeZone: "Europe/Warsaw", locale: "pl");
            body.Remove(field);
            using var response = await PostAsync(client, body, cookie: null, ct);
            await Problem.AssertAsync(response, HttpStatusCode.BadRequest, "invalid-request", ct);
        }

        using var withoutTimeZone = await PostAsync(client, Body(Ann, code, rememberMe: false, timeZone: null, locale: "pl"), cookie: null, ct);
        Assert.Equal(HttpStatusCode.OK, withoutTimeZone.StatusCode);
        Assert.Equal(
            """{"email":"ann@example.com","timeZone":"UTC","timeZoneRecognized":false,"locale":"pl"}""",
            await withoutTimeZone.Content.ReadAsStringAsync(ct));
    }

    private static void AtStart(HostFactory host) => host.WithCapturingSender().Time.SetUtcNow(Start);

    private static string WrongCode(string code) => code == "000000" ? "000001" : "000000";

    private static JsonObject Body(string email, string code, bool rememberMe, string? timeZone, string locale) => new()
    {
        ["email"] = email,
        ["code"] = code,
        ["rememberMe"] = rememberMe,
        ["timeZone"] = timeZone,
        ["locale"] = locale,
    };

    private static Task<HttpResponseMessage> SignInAsync(
        HttpClient client, string email, string code, string timeZone = "Europe/Warsaw", string locale = "pl", string? cookie = null,
        CancellationToken ct = default) =>
        PostAsync(client, Body(email, code, rememberMe: false, timeZone, locale), cookie, ct);

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, JsonObject body, string? cookie, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sessions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (cookie is not null)
            request.Headers.Add(HeaderNames.Cookie, $"session={cookie}");
        return client.SendAsync(request, ct);
    }

    private static string SessionTokenOf(HttpResponseMessage response) =>
        response.Headers.GetValues(HeaderNames.SetCookie)
            .Select(header => SetCookieHeaderValue.Parse(header)).Single(cookie => cookie.Name == "session").Value.ToString();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, CancellationToken ct) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();

    // The same status, header names and body; traceId is each request's own.
    private static async Task AssertSameAnswerAsync(HttpResponseMessage first, HttpResponseMessage second, CancellationToken ct)
    {
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(HeaderNamesOf(first), HeaderNamesOf(second));
        Assert.Equal(await BodyWithoutTraceIdAsync(first, ct), await BodyWithoutTraceIdAsync(second, ct));
    }

    private static async Task<string> BodyWithoutTraceIdAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!.AsObject();
        Assert.True(body.Remove("traceId"), "the problem carries no traceId");
        return body.ToJsonString();
    }

    private static string[] HeaderNamesOf(HttpResponseMessage response) =>
        [.. response.Headers.Concat(response.Content.Headers).Select(header => header.Key).Where(name => name != "Date").Order(StringComparer.Ordinal)];
}
