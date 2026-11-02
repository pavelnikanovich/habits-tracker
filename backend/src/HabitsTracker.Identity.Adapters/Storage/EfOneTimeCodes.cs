using HabitsTracker.Identity.Adapters.Storage.Records;
using HabitsTracker.Identity.Core;
using Microsoft.EntityFrameworkCore;

namespace HabitsTracker.Identity.Adapters.Storage;

/// <summary><see cref="IOneTimeCodes"/> on <c>identity.one_time_codes</c>: one code per email.</summary>
public sealed class EfOneTimeCodes(IdentityDbContext context) : IOneTimeCodes
{
    // FOR UPDATE holds the row until the transaction ends, so concurrent entries are counted one after another
    // (research R5). The query is not composed further: EF Core would wrap it in a subquery.
    public async Task<OneTimeCode?> FindExclusiveAsync(Email email, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);

        var records = await context.OneTimeCodes
            .FromSql($"SELECT * FROM identity.one_time_codes WHERE email = {email.Value} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(ct);
        return records.Count == 0 ? null : ToOneTimeCode(records.Single());
    }

    public async Task ReplaceAsync(OneTimeCode code, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(code);

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO identity.one_time_codes (email, code_hash, issued_at, expires_at, entry_attempts)
             VALUES ({code.Email.Value}, {code.CodeHash.ToArray()}, {code.IssuedAt}, {code.ExpiresAt}, {(short)code.EntryAttempts})
             ON CONFLICT (email) DO UPDATE SET
                 code_hash = EXCLUDED.code_hash,
                 issued_at = EXCLUDED.issued_at,
                 expires_at = EXCLUDED.expires_at,
                 entry_attempts = EXCLUDED.entry_attempts
             """,
            ct);
    }

    // A used code is deleted at once (data-model.md, "Retention"); the entry attempts are the only state a code changes.
    public async Task SaveAsync(OneTimeCode code, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(code);

        var stored = context.OneTimeCodes.Where(record => record.Email == code.Email.Value);
        if (code.IsUsed)
            await stored.ExecuteDeleteAsync(ct);
        else
            await stored.ExecuteUpdateAsync(set => set.SetProperty(record => record.EntryAttempts, (short)code.EntryAttempts), ct);
    }

    public Task<int> DeleteExpiredOrExhaustedAsync(DateTimeOffset now, int maxEntryAttempts, CancellationToken ct) =>
        context.OneTimeCodes
            .Where(record => record.ExpiresAt <= now || record.EntryAttempts >= maxEntryAttempts)
            .ExecuteDeleteAsync(ct);

    private static OneTimeCode ToOneTimeCode(OneTimeCodeRecord record) =>
        OneTimeCode.FromStorage(
            Email.FromStorage(record.Email),
            CodeHash.FromStorage(record.CodeHash),
            record.IssuedAt,
            record.ExpiresAt,
            record.EntryAttempts);
}
