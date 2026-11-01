using System.Buffers.Text;
using System.Security.Cryptography;

namespace HabitsTracker.Identity.Core;

// The secret a browser holds in its session cookie; the server stores only its SHA-256 (the session id).
public sealed record SessionToken
{
    private const int ByteLength = 32;
    private const int Length = 43;

    private SessionToken(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static SessionToken Generate() =>
        new(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteLength)));

    // Base64Url.IsValid skips whitespace and accepts "=" padding, so the decoded length is checked along with
    // the length: together they leave 43 characters of the alphabet.
    public static SessionToken Create(string? cookieValue)
    {
        if (cookieValue is not { Length: Length }
            || !Base64Url.IsValid(cookieValue, out var decodedLength)
            || decodedLength != ByteLength)
            throw new DomainException(DomainError.InvalidSessionToken, "The session token is not a valid session token.");

        return new SessionToken(cookieValue);
    }

    public byte[] ComputeId() => SHA256.HashData(Base64Url.DecodeFromChars(Value));

    // Secrets do not print (FR-011): the token is read only through Value.
    public override string ToString() => "***";
}
