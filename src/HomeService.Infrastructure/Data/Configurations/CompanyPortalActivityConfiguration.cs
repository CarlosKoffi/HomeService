using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyPortalActivityConfiguration : IEntityTypeConfiguration<CompanyPortalActivity>
{
    public void Configure(EntityTypeBuilder<CompanyPortalActivity> builder)
    {
        builder.Property(activity => activity.Type).HasMaxLength(64);
        builder.Property(activity => activity.Title).HasMaxLength(160);
        builder.Property(activity => activity.Description).HasMaxLength(320);
        builder.Property(activity => activity.Tone).HasMaxLength(32);
        builder.Property(activity => activity.EntityType).HasMaxLength(96);

        builder.HasIndex(activity => new { activity.CompanyId, activity.OccurredAt });
        builder.HasIndex(activity => new { activity.CompanyId, activity.IsRead, activity.OccurredAt });

        builder.HasOne(activity => activity.Company)
            .WithMany()
            .HasForeignKey(activity => activity.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
