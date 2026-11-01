using System.Buffers.Text;
using System.Security.Cryptography;

namespace HabitsTracker.Identity.Core.Tests;

public class SessionTokenTests
{
    private const string Base64UrlAlphabet = "^[A-Za-z0-9_-]{43}$";

    [Fact]
    public void Generated_token_is_43_base64url_characters_of_32_bytes()
    {
        var token = SessionToken.Generate();

        Assert.Matches(Base64UrlAlphabet, token.Value);
        Assert.Equal(32, Base64Url.DecodeFromChars(token.Value).Length);
    }

    [Fact]
    public void Generated_tokens_differ()
    {
        Assert.NotEqual(SessionToken.Generate().Value, SessionToken.Generate().Value);
    }

    [Fact]
    public void Id_is_sha256_of_the_token_bytes()
    {
        var token = SessionToken.Generate();

        Assert.Equal(SHA256.HashData(Base64Url.DecodeFromChars(token.Value)), token.ComputeId());
    }

    [Fact]
    public void Id_is_a_new_array_each_call()
    {
        var token = SessionToken.Generate();

        Assert.NotSame(token.ComputeId(), token.ComputeId());
    }

    [Fact]
    public void Creates_a_token_from_its_cookie_value()
    {
        var generated = SessionToken.Generate();

        var created = SessionToken.Create(generated.Value);

        Assert.Equal(generated.ComputeId(), created.ComputeId());
    }

    public static TheoryData<string?> Invalid => new()
    {
        "",
        "abc",
        new string('A', 44),
        new string('A', 42) + "+",
        new string('A', 42) + "/",
        new string('A', 42) + "=",
        // Base64 decoding skips whitespace: 43 characters with a space hold 42 of the alphabet.
        new string('A', 21) + " " + new string('A', 21),
        (string?)null,
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Rejects(string? cookieValue)
    {
        var exception = Assert.Throws<DomainException>(() => SessionToken.Create(cookieValue));

        Assert.Equal(DomainError.InvalidSessionToken, exception.Error);
    }
}
