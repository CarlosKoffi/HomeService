using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyPayoutDestinationConfiguration : IEntityTypeConfiguration<CompanyPayoutDestination>
{
    public void Configure(EntityTypeBuilder<CompanyPayoutDestination> builder)
    {
        builder.HasKey(destination => destination.Id);
        builder.Property(destination => destination.Method).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(destination => destination.Label).HasMaxLength(120).IsRequired();
        builder.Property(destination => destination.BeneficiaryName).HasMaxLength(180).IsRequired();
        builder.Property(destination => destination.ProviderCode).HasMaxLength(48).IsRequired();
        builder.Property(destination => destination.ProtectedDetails).HasMaxLength(4000).IsRequired();
        builder.Property(destination => destination.MaskedIdentifier).HasMaxLength(120).IsRequired();
        builder.Property(destination => destination.ExternalContactId).HasMaxLength(160);
        builder.HasOne(destination => destination.Company)
            .WithMany()
            .HasForeignKey(destination => destination.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(destination => new { destination.CompanyId, destination.IsActive });
        builder.HasIndex(destination => new { destination.CompanyId, destination.IsDefault });
    }
}
