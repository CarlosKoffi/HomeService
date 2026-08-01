using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class PaymentProviderConfiguration : IEntityTypeConfiguration<PaymentProvider>
{
    public void Configure(EntityTypeBuilder<PaymentProvider> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Method).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.LogoUrl).HasMaxLength(500);
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasIndex(item => new { item.IsActive, item.SortOrder });
    }
}
