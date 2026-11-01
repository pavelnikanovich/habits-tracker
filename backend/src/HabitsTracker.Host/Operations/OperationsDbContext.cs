using Microsoft.EntityFrameworkCore;

namespace HabitsTracker.Host.Operations;

/// <summary>The host's own storage, for what belongs to no context; schema <c>operations</c>.</summary>
public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public const string Schema = "operations";

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema);
}
