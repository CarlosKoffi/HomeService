using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyWalletEntryConfiguration : IEntityTypeConfiguration<CompanyWalletEntry>
{
    public void Configure(EntityTypeBuilder<CompanyWalletEntry> builder)
    {
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(entry => entry.Currency).HasMaxLength(8).IsRequired();
        builder.Property(entry => entry.IdempotencyKey).HasMaxLength(160).IsRequired();
        builder.Property(entry => entry.Description).HasMaxLength(500).IsRequired();
        builder.HasOne(entry => entry.Wallet)
            .WithMany()
            .HasForeignKey(entry => entry.WalletId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entry => entry.IdempotencyKey).IsUnique();
        builder.HasIndex(entry => new { entry.CompanyId, entry.CreatedAt });
        builder.HasIndex(entry => new { entry.Type, entry.EligibleAt });
        builder.HasIndex(entry => entry.MissionId);
        builder.HasIndex(entry => entry.PayoutRequestId);
    }
}
