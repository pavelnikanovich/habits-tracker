namespace HabitsTracker.Identity.Core.UseCases;

// The signed-in user, by the id the session authentication gave the request.
public sealed class GetCurrentUser
{
    private readonly IUsers users;

    public GetCurrentUser(IUsers users)
    {
        ArgumentNullException.ThrowIfNull(users);

        this.users = users;
    }

    public async Task<User> ExecuteAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("The user id is not set.", nameof(userId));

        return await users.FindByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("The signed-in user is not stored; users are never deleted in this epic.");
    }
}
