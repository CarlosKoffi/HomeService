using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsPageTranslationConfiguration : IEntityTypeConfiguration<CmsPageTranslation>
{
    public void Configure(EntityTypeBuilder<CmsPageTranslation> builder)
    {
        builder.HasKey(translation => translation.Id);
        builder.Property(translation => translation.Slug).HasMaxLength(180).IsRequired();
        builder.Property(translation => translation.Title).HasMaxLength(220).IsRequired();
        builder.Property(translation => translation.SeoTitle).HasMaxLength(220);
        builder.Property(translation => translation.MetaDescription).HasMaxLength(400);
        builder.Property(translation => translation.TranslationStatus).HasConversion<string>().HasMaxLength(32);

        builder.HasOne(translation => translation.Site)
            .WithMany()
            .HasForeignKey(translation => translation.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(translation => translation.Language)
            .WithMany()
            .HasForeignKey(translation => translation.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(translation => new { translation.PageId, translation.LanguageId }).IsUnique();
        builder.HasIndex(translation => new { translation.SiteId, translation.LanguageId, translation.Slug }).IsUnique();
    }
}
