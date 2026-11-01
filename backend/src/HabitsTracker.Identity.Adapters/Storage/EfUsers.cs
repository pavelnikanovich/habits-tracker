using HabitsTracker.Identity.Adapters.Storage.Records;
using HabitsTracker.Identity.Core;
using Microsoft.EntityFrameworkCore;
using TimeZone = HabitsTracker.Identity.Core.TimeZone;

namespace HabitsTracker.Identity.Adapters.Storage;

/// <summary><see cref="IUsers"/> on <c>identity.users</c>.</summary>
public sealed class EfUsers(IdentityDbContext context) : IUsers
{
    public async Task<User?> FindByEmailAsync(Email email, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);

        var record = await context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Email == email.Value, ct);
        return record is null ? null : ToUser(record);
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        var record = await context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, ct);
        return record is null ? null : ToUser(record);
    }

    // ON CONFLICT DO NOTHING waits for a concurrent insert of the same email and then skips, where a plain insert
    // would fail on the unique index and abort the sign-in's transaction; the select then reads the stored user.
    public async Task<User> AddOrGetAsync(User user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO identity.users (id, email, time_zone, time_zone_recognized, locale, created_at)
             VALUES ({user.Id}, {user.Email.Value}, {user.TimeZone.Id}, {user.TimeZone.Recognized}, {user.Locale.Code}, {user.CreatedAt})
             ON CONFLICT (email) DO NOTHING
             """,
            ct);

        return await FindByEmailAsync(user.Email, ct)
            ?? throw new InvalidOperationException("The user was neither inserted nor found.");
    }

    // The locale is the only state a user changes; the last change wins (data-model.md, "Concurrent changes").
    public async Task SaveAsync(User user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        await context.Users
            .Where(record => record.Id == user.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(record => record.Locale, user.Locale.Code), ct);
    }

    private static User ToUser(UserRecord record) =>
        User.FromStorage(
            record.Id,
            Email.FromStorage(record.Email),
            TimeZone.FromStorage(record.TimeZone, record.TimeZoneRecognized),
            Locale.FromStorage(record.Locale),
            record.CreatedAt);
}
