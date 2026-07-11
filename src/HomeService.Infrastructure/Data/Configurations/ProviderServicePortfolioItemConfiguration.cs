using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderServicePortfolioItemConfiguration : IEntityTypeConfiguration<ProviderServicePortfolioItem>
{
    public void Configure(EntityTypeBuilder<ProviderServicePortfolioItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(item => item.StoragePath).HasMaxLength(640).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.RejectionReason).HasMaxLength(600);
        builder.HasOne(item => item.Provider)
            .WithMany()
            .HasForeignKey(item => item.ProviderId);
        builder.HasOne(item => item.Service)
            .WithMany()
            .HasForeignKey(item => item.ServiceId);
        builder.HasIndex(item => new { item.ProviderId, item.ServiceId, item.DisplayOrder });
    }
}
