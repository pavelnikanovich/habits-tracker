using HabitsTracker.Identity.Adapters.Storage.Records;
using HabitsTracker.Identity.Core;
using Microsoft.EntityFrameworkCore;

namespace HabitsTracker.Identity.Adapters.Storage;

/// <summary><see cref="ISessions"/> on <c>identity.sessions</c>.</summary>
public sealed class EfSessions(IdentityDbContext context) : ISessions
{
    public async Task<Session?> FindAsync(byte[] id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(id);

        var record = await context.Sessions.AsNoTracking().SingleOrDefaultAsync(session => session.Id == id, ct);
        return record is null ? null : ToSession(record);
    }

    public async Task AddAsync(Session session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);

        var record = new SessionRecord
        {
            Id = session.Id,
            UserId = session.UserId,
            Remembered = session.Remembered,
            CreatedAt = session.CreatedAt,
            LastActivityAt = session.LastActivityAt,
        };
        context.Sessions.Add(record);
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

    // An UPDATE that inserts nothing, so a session signed out meanwhile stays deleted: sign out always wins
    // (data-model.md, "Concurrent changes"). The last activity is the only state a session changes.
    public async Task SaveAsync(Session session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);

        await context.Sessions
            .Where(record => record.Id == session.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(record => record.LastActivityAt, session.LastActivityAt), ct);
    }

    public async Task DeleteAsync(byte[] id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(id);

        await context.Sessions.Where(record => record.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task DeleteAllOfUserAsync(Guid userId, CancellationToken ct) =>
        await context.Sessions.Where(record => record.UserId == userId).ExecuteDeleteAsync(ct);

    public Task<int> DeleteInactiveAtOrBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) =>
        context.Sessions.Where(record => record.LastActivityAt <= cutoff).ExecuteDeleteAsync(ct);

    private static Session ToSession(SessionRecord record) =>
        Session.FromStorage(record.Id, record.UserId, record.Remembered, record.CreatedAt, record.LastActivityAt);
}
