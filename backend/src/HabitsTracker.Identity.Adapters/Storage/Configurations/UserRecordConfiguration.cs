using HabitsTracker.Identity.Adapters.Storage.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitsTracker.Identity.Adapters.Storage.Configurations;

/// <summary><c>identity.users</c> as data-model.md, "Schema identity".</summary>
public sealed class UserRecordConfiguration : IEntityTypeConfiguration<UserRecord>
{
    public void Configure(EntityTypeBuilder<UserRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(user => user.Email).HasColumnName("email").HasColumnType("text");
        builder.Property(user => user.TimeZone).HasColumnName("time_zone").HasColumnType("text");
        builder.Property(user => user.TimeZoneRecognized).HasColumnName("time_zone_recognized").HasColumnType("boolean");
        builder.Property(user => user.Locale).HasColumnName("locale").HasColumnType("text");
        builder.Property(user => user.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

        // Stored lower-cased (Email), so the unique index is one user per email (FR-008).
        builder.HasIndex(user => user.Email).IsUnique();
    }
}
