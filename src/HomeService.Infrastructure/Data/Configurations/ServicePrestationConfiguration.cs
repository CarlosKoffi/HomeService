using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ServicePrestationConfiguration : IEntityTypeConfiguration<ServicePrestation>
{
    public void Configure(EntityTypeBuilder<ServicePrestation> builder)
    {
        builder.HasKey(prestation => prestation.Id);
        builder.Property(prestation => prestation.Name).HasMaxLength(140).IsRequired();
        builder.Property(prestation => prestation.NormalizedName).HasMaxLength(140).IsRequired();
        builder.Property(prestation => prestation.Description).HasMaxLength(800);
        builder.Property(prestation => prestation.SortOrder).HasDefaultValue(0);
        builder.Property(prestation => prestation.Currency).HasMaxLength(8);
        builder.Property(prestation => prestation.IsActive).HasDefaultValue(true);

        builder.HasOne(prestation => prestation.Service)
            .WithMany(service => service.Prestations)
            .HasForeignKey(prestation => prestation.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(prestation => new { prestation.ServiceId, prestation.NormalizedName }).IsUnique();
        builder.HasIndex(prestation => new { prestation.ServiceId, prestation.IsActive, prestation.SortOrder });
    }
}
