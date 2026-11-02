using HabitsTracker.Identity.Adapters.Storage.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitsTracker.Identity.Adapters.Storage.Configurations;

/// <summary><c>identity.sessions</c> as data-model.md, "Schema identity".</summary>
public sealed class SessionRecordConfiguration : IEntityTypeConfiguration<SessionRecord>
{
    public void Configure(EntityTypeBuilder<SessionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasColumnName("id").HasColumnType("bytea");
        builder.Property(session => session.UserId).HasColumnName("user_id").HasColumnType("uuid");
        builder.Property(session => session.Remembered).HasColumnName("remembered").HasColumnType("boolean");
        builder.Property(session => session.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        builder.Property(session => session.LastActivityAt).HasColumnName("last_activity_at").HasColumnType("timestamptz");

        // data-model.md names the key and no delete rule, so the database default (NO ACTION) holds: no user is
        // deleted in this epic. Revisit with account deletion, which decides what happens to the sessions.
        builder.HasOne<UserRecord>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(session => session.UserId);
        builder.HasIndex(session => session.LastActivityAt);
    }
}
