namespace HabitsTracker.Identity.Core;

public interface IUsers
{
    Task<User?> FindByEmailAsync(Email email, CancellationToken ct);

    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);

    // Inserts; on a concurrent insert of the same email returns the stored user (one user per email, FR-008).
    Task<User> AddOrGetAsync(User user, CancellationToken ct);

    Task SaveAsync(User user, CancellationToken ct);
}
