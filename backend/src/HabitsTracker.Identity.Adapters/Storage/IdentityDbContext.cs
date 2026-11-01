using HabitsTracker.Identity.Adapters.Storage.Configurations;
using HabitsTracker.Identity.Adapters.Storage.Records;
using Microsoft.EntityFrameworkCore;

namespace HabitsTracker.Identity.Adapters.Storage;

/// <summary>
/// Identity's storage, schema <c>identity</c>. It maps only the record classes; the stores turn records into
/// aggregates through the core's <c>FromStorage</c> factories (data-model.md, "Storage"; research R23).
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<UserRecord> Users => Set<UserRecord>();

    public DbSet<OneTimeCodeRecord> OneTimeCodes => Set<OneTimeCodeRecord>();

    public DbSet<CodeRequestRecord> CodeRequests => Set<CodeRequestRecord>();

    public DbSet<SessionRecord> Sessions => Set<SessionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new UserRecordConfiguration());
        modelBuilder.ApplyConfiguration(new OneTimeCodeRecordConfiguration());
        modelBuilder.ApplyConfiguration(new CodeRequestRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SessionRecordConfiguration());
    }
}
