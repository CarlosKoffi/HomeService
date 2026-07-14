using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsMediaAssetConfiguration : IEntityTypeConfiguration<CmsMediaAsset>
{
    public void Configure(EntityTypeBuilder<CmsMediaAsset> builder)
    {
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.FileName).HasMaxLength(260).IsRequired();
        builder.Property(asset => asset.StoragePath).HasMaxLength(900).IsRequired();
        builder.Property(asset => asset.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(asset => asset.AltText).HasMaxLength(300);
        builder.Property(asset => asset.Checksum).HasMaxLength(128);
        builder.Property(asset => asset.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasMany(asset => asset.Variants)
            .WithOne(variant => variant.MediaAsset)
            .HasForeignKey(variant => variant.MediaAssetId);

        builder.HasIndex(asset => asset.StoragePath).IsUnique();
        builder.HasIndex(asset => asset.Checksum);
        builder.HasIndex(asset => asset.Status);
    }
}
