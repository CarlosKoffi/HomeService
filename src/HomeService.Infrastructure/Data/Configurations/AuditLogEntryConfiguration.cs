using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.ActorType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entry => entry.ActorDisplayName).HasMaxLength(180);
        builder.Property(entry => entry.Action).HasMaxLength(120).IsRequired();
        builder.Property(entry => entry.EntityType).HasMaxLength(160).IsRequired();
        builder.Property(entry => entry.Summary).HasMaxLength(1000);
        builder.Property(entry => entry.BeforeJson).HasColumnType("jsonb");
        builder.Property(entry => entry.AfterJson).HasColumnType("jsonb");
        builder.Property(entry => entry.IpAddress).HasMaxLength(80);
        builder.Property(entry => entry.UserAgent).HasMaxLength(500);
        builder.Property(entry => entry.CorrelationId).HasMaxLength(120);
        builder.HasIndex(entry => entry.OccurredAt);
        builder.HasIndex(entry => new { entry.ActorType, entry.ActorId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId, entry.OccurredAt });
        builder.HasIndex(entry => entry.CorrelationId);
    }
}
