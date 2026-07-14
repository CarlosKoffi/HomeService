using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsMediaVariantConfiguration : IEntityTypeConfiguration<CmsMediaVariant>
{
    public void Configure(EntityTypeBuilder<CmsMediaVariant> builder)
    {
        builder.HasKey(variant => variant.Id);
        builder.Property(variant => variant.VariantKey).HasMaxLength(80).IsRequired();
        builder.Property(variant => variant.StoragePath).HasMaxLength(900).IsRequired();
        builder.Property(variant => variant.ContentType).HasMaxLength(120).IsRequired();

        builder.HasIndex(variant => new { variant.MediaAssetId, variant.VariantKey }).IsUnique();
        builder.HasIndex(variant => variant.StoragePath).IsUnique();
    }
}
