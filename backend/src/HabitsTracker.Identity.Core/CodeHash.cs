using System.Security.Cryptography;
using System.Text;

namespace HabitsTracker.Identity.Core;

// The stored form of a one-time code: HMAC-SHA-256 under the code hash key, bound to the email.
public sealed record CodeHash
{
    private const int Length = HMACSHA256.HashSizeInBytes;

    private readonly byte[] _bytes;

    private CodeHash(byte[] bytes)
    {
        _bytes = bytes;
    }

    public ReadOnlyMemory<byte> Bytes => _bytes;

    public static CodeHash Compute(ReadOnlySpan<byte> key, Email email, OneTimeCodeValue code)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(code);

        // A key of another length, an empty one included, still gives a hash: only the check shows the mistake.
        if (key.Length != IdentityContextOptions.CodeHashKeyLength)
            throw new ArgumentException("The code hash key is not 32 bytes.", nameof(key));

        return new CodeHash(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(email.Value + "\n" + code.Digits)));
    }

    public static CodeHash FromStorage(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length != Length)
            throw new DomainException(DomainError.CorruptRecord, "A stored code hash breaks the code hash rules.");

        return new CodeHash(bytes.AsSpan().ToArray());
    }

    public byte[] ToArray() => _bytes.AsSpan().ToArray();

    // Constant time, so the comparison does not tell how many leading bytes agree.
    public bool Matches(CodeHash other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return CryptographicOperations.FixedTimeEquals(_bytes, other._bytes);
    }

    public bool Equals(CodeHash? other) =>
        other is not null && CryptographicOperations.FixedTimeEquals(_bytes, other._bytes);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_bytes);
        return hash.ToHashCode();
    }

    // Secrets do not print (FR-011): the bytes are read only through Bytes and ToArray.
    public override string ToString() => "***";
}
