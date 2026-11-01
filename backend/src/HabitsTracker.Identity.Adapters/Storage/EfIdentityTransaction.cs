using HabitsTracker.Identity.Core;

namespace HabitsTracker.Identity.Adapters.Storage;

/// <summary>
/// <see cref="IIdentityTransaction"/> as one PostgreSQL transaction on the request's context, which every store
/// of the request shares, so their writes commit or roll back together.
/// </summary>
public sealed class EfIdentityTransaction(IdentityDbContext context) : IIdentityTransaction
{
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);

        // Disposed without a commit when the work throws, which rolls the transaction back.
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var result = await work(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
