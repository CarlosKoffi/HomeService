using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.HasKey(service => service.Id);
        builder.Property(service => service.Name).HasMaxLength(120).IsRequired();
        builder.Property(service => service.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(service => service.Description).HasMaxLength(800);
        builder.Property(service => service.IconName).HasMaxLength(80).HasDefaultValue("sparkles").IsRequired();
        builder.Property(service => service.Currency).HasMaxLength(3).IsRequired();
        builder.Property(service => service.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(service => service.MinimumPortfolioItems).HasDefaultValue(0);
        builder.Property(service => service.RequiresPortfolio).HasDefaultValue(false);
        builder.Property(service => service.RequiresCompletionPhoto).HasDefaultValue(false);
        builder.Property(service => service.RequiresBeforeAfterPhotos).HasDefaultValue(false);
        builder.Property(service => service.RequiresDiploma).HasDefaultValue(false);
        builder.Property(service => service.RequiresAdminApprovalBeforeAssignment).HasDefaultValue(false);
        builder.HasIndex(service => service.Name);
        builder.HasIndex(service => service.NormalizedName).IsUnique();
    }
}
