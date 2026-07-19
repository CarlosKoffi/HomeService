using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionPaymentMilestoneConfiguration : IEntityTypeConfiguration<MissionPaymentMilestone>
{
    public void Configure(EntityTypeBuilder<MissionPaymentMilestone> builder)
    {
        builder.HasKey(milestone => milestone.Id);
        builder.Property(milestone => milestone.Trigger).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(milestone => milestone.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(milestone => milestone.Currency).HasMaxLength(8).IsRequired();
        builder.Property(milestone => milestone.Label).HasMaxLength(160).IsRequired();
        builder.Property(milestone => milestone.ExternalPaymentReference).HasMaxLength(160);
        builder.HasOne(milestone => milestone.Mission)
            .WithMany()
            .HasForeignKey(milestone => milestone.MissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(milestone => new { milestone.MissionId, milestone.Trigger });
        builder.HasIndex(milestone => new { milestone.Status, milestone.DueAt });
    }
}
