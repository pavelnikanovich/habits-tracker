using HabitsTracker.Identity.Adapters.Storage.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitsTracker.Identity.Adapters.Storage.Configurations;

/// <summary><c>identity.one_time_codes</c> as data-model.md, "Schema identity": one code per email, stored only as its hash.</summary>
public sealed class OneTimeCodeRecordConfiguration : IEntityTypeConfiguration<OneTimeCodeRecord>
{
    public void Configure(EntityTypeBuilder<OneTimeCodeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("one_time_codes");
        builder.HasKey(code => code.Email);
        builder.Property(code => code.Email).HasColumnName("email").HasColumnType("text");
        builder.Property(code => code.CodeHash).HasColumnName("code_hash").HasColumnType("bytea");
        builder.Property(code => code.IssuedAt).HasColumnName("issued_at").HasColumnType("timestamptz");
        builder.Property(code => code.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz");
        builder.Property(code => code.EntryAttempts).HasColumnName("entry_attempts").HasColumnType("smallint");

        builder.HasIndex(code => code.ExpiresAt);
    }
}
