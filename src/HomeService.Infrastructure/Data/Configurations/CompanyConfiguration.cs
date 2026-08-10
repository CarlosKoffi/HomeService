using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(company => company.Id);
        builder.Property(company => company.Name).HasMaxLength(160).IsRequired();
        builder.Property(company => company.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(company => company.Email).HasMaxLength(256);
        builder.Property(company => company.LegalForm).HasMaxLength(80);
        builder.Property(company => company.RegistrationNumber).HasMaxLength(80);
        builder.Property(company => company.TaxIdentificationNumber).HasMaxLength(80);
        builder.Property(company => company.City).HasMaxLength(120);
        builder.Property(company => company.Address).HasMaxLength(240);
        builder.Property(company => company.InterventionZones).HasMaxLength(1000);
        builder.Property(company => company.PlannedServices).HasMaxLength(1000);
        builder.Property(company => company.WavePaymentNumber).HasMaxLength(32);
        builder.Property(company => company.OrangeMoneyPaymentNumber).HasMaxLength(32);
        builder.Property(company => company.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(company => company.AssignmentMode).HasConversion<string>().HasMaxLength(32);
        builder.Property(company => company.SettlementFrequency)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(Domain.Enums.CompanySettlementFrequency.Monthly)
            .HasSentinel(Domain.Enums.CompanySettlementFrequency.Unspecified);
        builder.Property(company => company.AcceptsInterimApplications).HasDefaultValue(false);
        builder.Property(company => company.MissionDispatchPriority).HasDefaultValue(100);
        builder.Property(company => company.AcceptsUrgentMissions).HasDefaultValue(false);
        builder.Property(company => company.CurrentCommissionTierName).HasMaxLength(120).HasDefaultValue("Lancement");
        builder.Property(company => company.CurrentCommissionTierMinimumMissionCount).HasDefaultValue(1);
        builder.Property(company => company.CurrentCommissionRateBasisPoints).HasDefaultValue(1500);
        builder.HasIndex(company => new { company.Status, company.MissionDispatchPriority });
    }
}
