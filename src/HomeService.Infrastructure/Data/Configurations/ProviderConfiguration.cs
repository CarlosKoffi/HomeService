using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderConfiguration : IEntityTypeConfiguration<ProviderProfile>
{
    public void Configure(EntityTypeBuilder<ProviderProfile> builder)
    {
        builder.HasKey(provider => provider.Id);
        builder.Property(provider => provider.FirstName).HasMaxLength(120).IsRequired();
        builder.Property(provider => provider.LastName).HasMaxLength(120).IsRequired();
        builder.Property(provider => provider.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(provider => provider.Address).HasMaxLength(320).IsRequired();
        builder.Property(provider => provider.Gender).HasConversion<string>().HasMaxLength(32);
        builder.Property(provider => provider.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(provider => provider.RegistrationSource)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(HomeService.Domain.Enums.ProviderRegistrationSource.CompanyInvitation);
        builder.Property(provider => provider.PasswordHash).HasMaxLength(256);
        builder.Property(provider => provider.MissionLatitude).HasPrecision(9, 6);
        builder.Property(provider => provider.MissionLongitude).HasPrecision(9, 6);
        builder.Property(provider => provider.CurrentLatitude).HasPrecision(9, 6);
        builder.Property(provider => provider.CurrentLongitude).HasPrecision(9, 6);
        builder.Property(provider => provider.EmploymentType).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(provider => provider.Company)
            .WithMany(company => company.Providers)
            .HasForeignKey(provider => provider.CompanyId)
            .IsRequired(false);
        builder.HasMany(provider => provider.Documents)
            .WithOne(document => document.Provider)
            .HasForeignKey(document => document.ProviderId);
        builder.HasMany(provider => provider.Services)
            .WithOne()
            .HasForeignKey(service => service.ProviderId);
        builder.HasMany(provider => provider.CandidateServices)
            .WithOne(candidateService => candidateService.Provider)
            .HasForeignKey(candidateService => candidateService.ProviderId);
        builder.HasIndex(provider => new { provider.CompanyId, provider.Status });
        builder.HasIndex(provider => provider.Status);
    }
}
