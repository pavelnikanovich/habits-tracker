using System.Net;
using System.Text.Json;

namespace HabitsTracker.Host.Tests;

/// <summary>The problem answers of contracts/http-api.md (RFC 9457), as the tests check them.</summary>
public static class Problem
{
    public const string MediaType = "application/problem+json";

    /// <summary>
    /// Asserts that <paramref name="response"/> is a problem (<c>application/problem+json</c>) with
    /// <paramref name="status"/> and <paramref name="code"/>, and returns it parsed, for a test that checks more of its
    /// members. <paramref name="request"/> names the request in a failure message, where a test sends several.
    /// </summary>
    public static async Task<JsonElement> AssertAsync(
        HttpResponseMessage response, HttpStatusCode status, string code, CancellationToken ct, string? request = null)
    {
        ArgumentNullException.ThrowIfNull(response);
        var prefix = request is null ? "" : $"{request}: ";
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.True(response.StatusCode == status, $"{prefix}status {(int)response.StatusCode}, expected {(int)status}; body '{body}'");
        Assert.True(
            response.Content.Headers.ContentType?.MediaType == MediaType,
            $"{prefix}Content-Type '{response.Content.Headers.ContentType}'");

        using var document = JsonDocument.Parse(body);
        var problem = document.RootElement.Clone();
        var actual = problem.TryGetProperty("code", out var value) ? value.GetString() : null;
        Assert.True(actual == code, $"{prefix}code '{actual}', expected '{code}'");
        return problem;
    }
}
