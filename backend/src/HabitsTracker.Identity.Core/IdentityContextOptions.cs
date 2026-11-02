namespace HabitsTracker.Identity.Core;

// The configuration of the Identity context. Not IdentityOptions, a type of ASP.NET Core Identity.
public sealed record IdentityContextOptions
{
    internal const int CodeHashKeyLength = 32;

    // Breaks "value objects are compared by value" (data-model.md, "Domain model rules") on purpose: the generated
    // Equals compares this array by reference, so two options with equal keys are not equal. The host builds one
    // instance at start and nothing compares options; comparing by content would be code without a caller.
    private readonly byte[] _codeHashKey;

    private IdentityContextOptions(byte[] codeHashKey, CodeRequestLimits codeRequestLimits)
    {
        _codeHashKey = codeHashKey;
        CodeRequestLimits = codeRequestLimits;
    }

    public ReadOnlyMemory<byte> CodeHashKey => _codeHashKey;

    public CodeRequestLimits CodeRequestLimits { get; }

    public static IdentityContextOptions Create(byte[] codeHashKey, CodeRequestLimits limits)
    {
        ArgumentNullException.ThrowIfNull(codeHashKey);
        ArgumentNullException.ThrowIfNull(limits);

        if (codeHashKey.Length != CodeHashKeyLength)
            throw new DomainException(DomainError.InvalidConfiguration, "The code hash key is not 32 bytes.");

        return new IdentityContextOptions(codeHashKey.AsSpan().ToArray(), limits);
    }
}
