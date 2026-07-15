using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyApplicationConfiguration : IEntityTypeConfiguration<CompanyApplication>
{
    public void Configure(EntityTypeBuilder<CompanyApplication> builder)
    {
        builder.HasKey(application => application.Id);
        builder.Property(application => application.CompanyId);
        builder.Property(application => application.CompanyName).HasMaxLength(180).IsRequired();
        builder.Property(application => application.RegistrationNumber).HasMaxLength(80);
        builder.Property(application => application.LegalForm).HasMaxLength(80);
        builder.Property(application => application.TaxIdentificationNumber).HasMaxLength(80);
        builder.Property(application => application.City).HasMaxLength(120).IsRequired();
        builder.Property(application => application.Address).HasMaxLength(240);
        builder.Property(application => application.ContactName).HasMaxLength(160).IsRequired();
        builder.Property(application => application.Email).HasMaxLength(256).IsRequired();
        builder.Property(application => application.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(application => application.PlannedServices).HasMaxLength(1000);
        builder.Property(application => application.InterventionZones).HasMaxLength(1000);
        builder.Property(application => application.WavePaymentNumber).HasMaxLength(32);
        builder.Property(application => application.OrangeMoneyPaymentNumber).HasMaxLength(32);
        builder.Property(application => application.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(application => application.ReviewNote).HasMaxLength(1000);
        builder.HasOne(application => application.Company)
            .WithMany(company => company.Applications)
            .HasForeignKey(application => application.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(application => new { application.Status, application.SubmittedAt });
        builder.HasIndex(application => application.CompanyId);
        builder.HasMany(application => application.Documents)
            .WithOne(document => document.CompanyApplication)
            .HasForeignKey(document => document.CompanyApplicationId);
        builder.HasMany(application => application.RequestedServices)
            .WithOne(service => service.CompanyApplication)
            .HasForeignKey(service => service.CompanyApplicationId);
        builder.HasMany(application => application.StatusHistory)
            .WithOne(history => history.CompanyApplication)
            .HasForeignKey(history => history.CompanyApplicationId);
        builder.HasMany(application => application.ActivationTokens)
            .WithOne(token => token.CompanyApplication)
            .HasForeignKey(token => token.CompanyApplicationId);
    }
}
