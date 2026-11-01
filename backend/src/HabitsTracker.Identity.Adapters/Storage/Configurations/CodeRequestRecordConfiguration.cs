using HabitsTracker.Identity.Adapters.Storage.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitsTracker.Identity.Adapters.Storage.Configurations;

/// <summary><c>identity.code_requests</c> as data-model.md, "Schema identity".</summary>
public sealed class CodeRequestRecordConfiguration : IEntityTypeConfiguration<CodeRequestRecord>
{
    public void Configure(EntityTypeBuilder<CodeRequestRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("code_requests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnName("id").HasColumnType("bigint").UseIdentityByDefaultColumn();
        builder.Property(request => request.Email).HasColumnName("email").HasColumnType("text");
        builder.Property(request => request.IpPrefix).HasColumnName("ip_prefix").HasColumnType("cidr");
        builder.Property(request => request.RequestedAt).HasColumnName("requested_at").HasColumnType("timestamptz");

        // The counts per email and per prefix in the window, and the retention cleanup.
        builder.HasIndex(request => new { request.Email, request.RequestedAt });
        builder.HasIndex(request => new { request.IpPrefix, request.RequestedAt });
        builder.HasIndex(request => request.RequestedAt);
    }
}
