using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyWalletConfiguration : IEntityTypeConfiguration<CompanyWallet>
{
    public void Configure(EntityTypeBuilder<CompanyWallet> builder)
    {
        builder.HasKey(wallet => wallet.Id);
        builder.Property(wallet => wallet.Currency).HasMaxLength(8).IsRequired();
        builder.Property(wallet => wallet.Version).IsConcurrencyToken();
        builder.HasOne(wallet => wallet.Company)
            .WithOne()
            .HasForeignKey<CompanyWallet>(wallet => wallet.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(wallet => wallet.CompanyId).IsUnique();
    }
}
