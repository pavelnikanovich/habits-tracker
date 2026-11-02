using System.Security.Cryptography;
using System.Text;

namespace HabitsTracker.Identity.Core.Tests;

public class CodeHashTests
{
    private static readonly byte[] Key = [.. Enumerable.Range(1, 32).Select(i => (byte)i)];
    private static readonly Email Ann = Email.Create("ann@example.com");
    private static readonly OneTimeCodeValue Code = OneTimeCodeValue.Create("042917");

    [Fact]
    public void Hash_is_hmac_sha256_over_the_email_and_the_code()
    {
        var expected = HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes("ann@example.com\n042917"));

        var hash = CodeHash.Compute(Key, Ann, Code);

        Assert.Equal(expected, hash.ToArray());
    }

    [Fact]
    public void Hashes_of_the_same_email_and_code_match()
    {
        Assert.True(CodeHash.Compute(Key, Ann, Code).Matches(CodeHash.Compute(Key, Ann, Code)));
    }

    [Fact]
    public void Hash_binds_the_email()
    {
        var bob = Email.Create("bob@example.com");

        Assert.False(CodeHash.Compute(Key, Ann, Code).Matches(CodeHash.Compute(Key, bob, Code)));
    }

    [Fact]
    public void Hash_depends_on_the_key()
    {
        byte[] otherKey = [.. Enumerable.Repeat((byte)0xFF, 32)];

        Assert.False(CodeHash.Compute(Key, Ann, Code).Matches(CodeHash.Compute(otherKey, Ann, Code)));
    }

    [Fact]
    public void From_storage_copies_the_bytes()
    {
        var stored = new byte[32];
        var hash = CodeHash.FromStorage(stored);

        stored[0] = 1;

        Assert.Equal(0, hash.Bytes.Span[0]);
    }

    [Fact]
    public void To_array_returns_a_copy()
    {
        var hash = CodeHash.FromStorage(new byte[32]);

        hash.ToArray()[0] = 1;

        Assert.Equal(0, hash.Bytes.Span[0]);
    }
}
