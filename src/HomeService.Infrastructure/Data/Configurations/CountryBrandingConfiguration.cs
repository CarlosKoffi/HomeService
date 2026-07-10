using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CountryBrandingConfiguration : IEntityTypeConfiguration<CountryBranding>
{
    public void Configure(EntityTypeBuilder<CountryBranding> builder)
    {
        builder.HasKey(branding => branding.Id);
        builder.Property(branding => branding.BrandName).HasMaxLength(120).IsRequired();
        builder.Property(branding => branding.PrimaryColor).HasMaxLength(16).IsRequired();
        builder.Property(branding => branding.SecondaryColor).HasMaxLength(16).IsRequired();
        builder.Property(branding => branding.AccentColor).HasMaxLength(16).IsRequired();
        builder.Property(branding => branding.HeroTitle).HasMaxLength(220).IsRequired();
        builder.Property(branding => branding.HeroSubtitle).HasMaxLength(600).IsRequired();
        builder.Property(branding => branding.HeroImageUrl).HasMaxLength(1000);
        builder.Property(branding => branding.MotifStyle).HasMaxLength(80).IsRequired();
        builder.HasOne(branding => branding.Country)
            .WithOne()
            .HasForeignKey<CountryBranding>(branding => branding.CountryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(branding => branding.CountryId).IsUnique();
    }
}
