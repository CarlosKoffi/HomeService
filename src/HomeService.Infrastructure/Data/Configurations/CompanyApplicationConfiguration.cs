using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyApplicationConfiguration : IEntityTypeConfiguration<CompanyApplication>
{
    public void Configure(EntityTypeBuilder<CompanyApplication> builder)
    {
        builder.HasKey(application => application.Id);
        builder.Property(application => application.CompanyName).HasMaxLength(180).IsRequired();
        builder.Property(application => application.RegistrationNumber).HasMaxLength(80);
        builder.Property(application => application.City).HasMaxLength(120).IsRequired();
        builder.Property(application => application.Address).HasMaxLength(240);
        builder.Property(application => application.ContactName).HasMaxLength(160).IsRequired();
        builder.Property(application => application.Email).HasMaxLength(256).IsRequired();
        builder.Property(application => application.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(application => application.PlannedServices).HasMaxLength(1000);
        builder.Property(application => application.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(application => application.ReviewNote).HasMaxLength(1000);
        builder.HasIndex(application => new { application.Status, application.SubmittedAt });
        builder.HasMany(application => application.Documents)
            .WithOne(document => document.CompanyApplication)
            .HasForeignKey(document => document.CompanyApplicationId);
        builder.HasMany(application => application.RequestedServices)
            .WithOne(service => service.CompanyApplication)
            .HasForeignKey(service => service.CompanyApplicationId);
    }
}
