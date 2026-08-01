using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ServiceOptionConfiguration : IEntityTypeConfiguration<ServiceOption>
{
    public void Configure(EntityTypeBuilder<ServiceOption> builder)
    {
        builder.HasKey(option => option.Id);
        builder.Property(option => option.Name).HasMaxLength(160).IsRequired();
        builder.Property(option => option.NormalizedName).HasMaxLength(160).IsRequired();
        builder.Property(option => option.Description).HasMaxLength(800);
        builder.Property(option => option.SortOrder).HasDefaultValue(0);
        builder.Property(option => option.PriceMinAmount).HasDefaultValue(0);
        builder.Property(option => option.PriceMaxAmount).HasDefaultValue(0);
        builder.Property(option => option.IsFixedPrice).HasDefaultValue(false);
        builder.Property(option => option.Currency).HasMaxLength(8).IsRequired();
        builder.Property(option => option.IsActive).HasDefaultValue(true);

        builder.HasOne(option => option.ServicePrestation)
            .WithMany(prestation => prestation.Options)
            .HasForeignKey(option => option.ServicePrestationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(option => new { option.ServicePrestationId, option.NormalizedName }).IsUnique();
        builder.HasIndex(option => new { option.ServicePrestationId, option.IsActive, option.SortOrder });
    }
}
