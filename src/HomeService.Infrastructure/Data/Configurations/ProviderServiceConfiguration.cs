using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderServiceConfiguration : IEntityTypeConfiguration<ProviderService>
{
    public void Configure(EntityTypeBuilder<ProviderService> builder)
    {
        builder.HasKey(providerService => providerService.Id);
        builder.Property(providerService => providerService.ExperienceLevel).HasConversion<string>().HasMaxLength(32);
        builder.Property(providerService => providerService.PricingUnit).HasConversion<string>().HasMaxLength(32);
        builder.Property(providerService => providerService.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(providerService => new { providerService.ProviderId, providerService.ServiceId }).IsUnique();
    }
}
