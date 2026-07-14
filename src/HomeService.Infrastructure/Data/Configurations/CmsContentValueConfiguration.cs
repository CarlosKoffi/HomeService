using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsContentValueConfiguration : IEntityTypeConfiguration<CmsContentValue>
{
    public void Configure(EntityTypeBuilder<CmsContentValue> builder)
    {
        builder.HasKey(value => value.Id);
        builder.Property(value => value.FieldKey).HasMaxLength(120).IsRequired();
        builder.Property(value => value.ValueType).HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.TextValue).HasMaxLength(4000);
        builder.Property(value => value.JsonValue).HasColumnType("jsonb");

        builder.HasOne(value => value.Language)
            .WithMany()
            .HasForeignKey(value => value.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(value => value.MediaAsset)
            .WithMany()
            .HasForeignKey(value => value.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(value => new { value.SectionId, value.FieldKey, value.LanguageId }).IsUnique();
        builder.HasIndex(value => value.MediaAssetId);
    }
}
