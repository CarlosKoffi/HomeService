using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionAdditionalQuoteConfiguration : IEntityTypeConfiguration<MissionAdditionalQuote>
{
    public void Configure(EntityTypeBuilder<MissionAdditionalQuote> builder)
    {
        builder.HasKey(quote => quote.Id);
        builder.Property(quote => quote.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(quote => quote.Reason).HasMaxLength(1200).IsRequired();
        builder.Property(quote => quote.RequestedPhotoStoragePath).HasMaxLength(500);
        builder.Property(quote => quote.Currency).HasMaxLength(8).IsRequired();
        builder.Property(quote => quote.CompanyDescription).HasMaxLength(1200);
        builder.Property(quote => quote.PaymentReference).HasMaxLength(160);

        builder.HasOne(quote => quote.Mission)
            .WithMany()
            .HasForeignKey(quote => quote.MissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(quote => quote.Provider)
            .WithMany()
            .HasForeignKey(quote => quote.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(quote => quote.Company)
            .WithMany()
            .HasForeignKey(quote => quote.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(quote => new { quote.MissionId, quote.Status });
        builder.HasIndex(quote => new { quote.CompanyId, quote.Status, quote.RequestedAt });
        builder.HasIndex(quote => new { quote.ProviderId, quote.Status, quote.RequestedAt });
    }
}
