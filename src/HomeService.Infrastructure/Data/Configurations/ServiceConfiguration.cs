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
        builder.Property(service => service.Currency).HasMaxLength(3).IsRequired();
        builder.Property(service => service.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(service => service.Name);
        builder.HasIndex(service => service.NormalizedName).IsUnique();
    }
}
