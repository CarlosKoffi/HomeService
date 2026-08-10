using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyCommissionTierConfiguration : IEntityTypeConfiguration<CompanyCommissionTier>
{
    public void Configure(EntityTypeBuilder<CompanyCommissionTier> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.IsActive).HasDefaultValue(true);
        builder.HasIndex(item => item.MinimumMissionCount).IsUnique();
        builder.HasIndex(item => new { item.IsActive, item.SortOrder });
    }
}
