using HabitsTracker.Host.Metrics;
using Microsoft.EntityFrameworkCore;

namespace HabitsTracker.Host.Operations;

/// <summary>The host's own storage, for what belongs to no context; schema <c>operations</c>.</summary>
public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public const string Schema = "operations";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        // FR-039; data-model.md, "Schema operations". The host writes it with an upsert (RequestTimingFlusher).
        modelBuilder.Entity<RequestTiming>(entity =>
        {
            entity.ToTable("request_timings");
            entity.HasKey(timing => new { timing.Hour, timing.Endpoint, timing.BucketUpperMs });
            entity.Property(timing => timing.Hour).HasColumnName("hour").HasColumnType("timestamptz");
            entity.Property(timing => timing.Endpoint).HasColumnName("endpoint").HasColumnType("text");
            entity.Property(timing => timing.BucketUpperMs).HasColumnName("bucket_upper_ms").HasColumnType("integer");
            entity.Property(timing => timing.Count).HasColumnName("count").HasColumnType("bigint");
        });
    }
}
