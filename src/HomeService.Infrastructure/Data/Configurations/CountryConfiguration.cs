using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.HasKey(country => country.Id);
        builder.Property(country => country.IsoCode).HasMaxLength(2).IsRequired();
        builder.Property(country => country.Name).HasMaxLength(120).IsRequired();
        builder.Property(country => country.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.HasIndex(country => country.IsoCode).IsUnique();
    }
}
