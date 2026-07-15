using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderServicePrestationConfiguration : IEntityTypeConfiguration<ProviderServicePrestation>
{
    public void Configure(EntityTypeBuilder<ProviderServicePrestation> builder)
    {
        builder.HasKey(prestation => prestation.Id);
        builder.Property(prestation => prestation.IsActive).HasDefaultValue(true);

        builder.HasOne(prestation => prestation.ProviderService)
            .WithMany(providerService => providerService.Prestations)
            .HasForeignKey(prestation => prestation.ProviderServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(prestation => prestation.ServicePrestation)
            .WithMany()
            .HasForeignKey(prestation => prestation.ServicePrestationId);

        builder.HasIndex(prestation => new { prestation.ProviderServiceId, prestation.ServicePrestationId }).IsUnique();
        builder.HasIndex(prestation => new { prestation.ServicePrestationId, prestation.IsActive });
    }
}
