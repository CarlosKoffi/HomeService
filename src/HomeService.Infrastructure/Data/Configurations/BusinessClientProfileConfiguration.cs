using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class BusinessClientProfileConfiguration : IEntityTypeConfiguration<BusinessClientProfile>
{
    public void Configure(EntityTypeBuilder<BusinessClientProfile> builder)
    {
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.LegalName).HasMaxLength(180).IsRequired();
        builder.Property(profile => profile.TradeName).HasMaxLength(180);
        builder.Property(profile => profile.LegalForm).HasMaxLength(80);
        builder.Property(profile => profile.RegistrationNumber).HasMaxLength(80);
        builder.Property(profile => profile.TaxIdentificationNumber).HasMaxLength(80);
        builder.Property(profile => profile.Address).HasMaxLength(240).IsRequired();
        builder.Property(profile => profile.City).HasMaxLength(120).IsRequired();
        builder.Property(profile => profile.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(profile => profile.RepresentativeName).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.RepresentativeRole).HasMaxLength(120).IsRequired();
        builder.Property(profile => profile.ContactEmail).HasMaxLength(256).IsRequired();
        builder.Property(profile => profile.ContactPhone).HasMaxLength(32).IsRequired();
        builder.Property(profile => profile.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(profile => profile.ReviewNote).HasMaxLength(1000);
        builder.HasOne(profile => profile.CustomerProfile)
            .WithOne()
            .HasForeignKey<BusinessClientProfile>(profile => profile.CustomerProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(profile => profile.CustomerProfileId).IsUnique();
        builder.HasIndex(profile => new { profile.Status, profile.SubmittedAt });
        builder.HasMany(profile => profile.Documents)
            .WithOne(document => document.BusinessClientProfile)
            .HasForeignKey(document => document.BusinessClientProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
