using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionDisputeConfiguration : IEntityTypeConfiguration<MissionDispute>
{
    public void Configure(EntityTypeBuilder<MissionDispute> builder)
    {
        builder.HasKey(dispute => dispute.Id);
        builder.Property(dispute => dispute.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(dispute => dispute.OpenedBy).HasConversion<string>().HasMaxLength(32);
        builder.Property(dispute => dispute.Reason).HasConversion<string>().HasMaxLength(64);
        builder.Property(dispute => dispute.Resolution).HasConversion<string>().HasMaxLength(64);
        builder.Property(dispute => dispute.Description).HasMaxLength(1200).IsRequired();
        builder.Property(dispute => dispute.ResolutionNote).HasMaxLength(1200);
        builder.HasOne(dispute => dispute.Mission)
            .WithMany()
            .HasForeignKey(dispute => dispute.MissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(dispute => new { dispute.MissionId, dispute.Status });
        builder.HasIndex(dispute => new { dispute.Status, dispute.OpenedAt });
    }
}
