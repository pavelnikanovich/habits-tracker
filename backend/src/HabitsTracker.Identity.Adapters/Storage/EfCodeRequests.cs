using HabitsTracker.Identity.Adapters.Storage.Records;
using HabitsTracker.Identity.Core;
using Microsoft.EntityFrameworkCore;

namespace HabitsTracker.Identity.Adapters.Storage;

/// <summary><see cref="ICodeRequests"/> on <c>identity.code_requests</c>.</summary>
public sealed class EfCodeRequests(IdentityDbContext context) : ICodeRequests
{
    // Transaction-scoped advisory locks, always the email first and then the prefix, so two requests never wait
    // for each other in opposite order (research R5). Outside a transaction PostgreSQL would release them at once.
    public async Task LockAsync(Email email, IpAddressPrefix ipAddressPrefix, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(ipAddressPrefix);

        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Code request locks are taken inside a transaction.");

        var prefix = ipAddressPrefix.Network.ToString();
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended('email:' || {email.Value}, 0))", ct);
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended('ip:' || {prefix}, 0))", ct);
    }

    public async Task<CodeRequestCounts> CountAsync(
        Email email, IpAddressPrefix ipAddressPrefix, DateTimeOffset windowStart, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(ipAddressPrefix);

        var inWindow = context.CodeRequests.Where(record => record.RequestedAt > windowStart);
        var prefix = ipAddressPrefix.Network;
        var forEmail = await inWindow
            .Where(record => record.Email == email.Value)
            .GroupBy(record => record.Email)
            .Select(group => new { Count = group.Count(), Oldest = group.Min(record => record.RequestedAt) })
            .SingleOrDefaultAsync(ct);
        var forPrefix = await inWindow
            .Where(record => record.IpPrefix == prefix)
            .GroupBy(record => record.IpPrefix)
            .Select(group => new { Count = group.Count(), Oldest = group.Min(record => record.RequestedAt) })
            .SingleOrDefaultAsync(ct);

        return new CodeRequestCounts(forEmail?.Count ?? 0, forEmail?.Oldest, forPrefix?.Count ?? 0, forPrefix?.Oldest);
    }

    public async Task AddAsync(CodeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var record = new CodeRequestRecord
        {
            Email = request.Email.Value,
            IpPrefix = request.IpAddressPrefix.Network,
            RequestedAt = request.RequestedAt,
        };
        context.CodeRequests.Add(record);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        finally
        {
            // Stores read the database, never the change tracker: the record is not kept.
            context.Entry(record).State = EntityState.Detached;
        }
    }

    public Task<int> DeleteRequestedAtOrBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) =>
        context.CodeRequests.Where(record => record.RequestedAt <= cutoff).ExecuteDeleteAsync(ct);
}
