using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsPageConfiguration : IEntityTypeConfiguration<CmsPage>
{
    public void Configure(EntityTypeBuilder<CmsPage> builder)
    {
        builder.HasKey(page => page.Id);
        builder.Property(page => page.Code).HasMaxLength(100).IsRequired();
        builder.Property(page => page.InternalName).HasMaxLength(180).IsRequired();
        builder.Property(page => page.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(page => page.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasMany(page => page.Translations)
            .WithOne(translation => translation.Page)
            .HasForeignKey(translation => translation.PageId);

        builder.HasMany(page => page.Versions)
            .WithOne(version => version.Page)
            .HasForeignKey(version => version.PageId);

        builder.HasIndex(page => new { page.SiteId, page.Code }).IsUnique();
        builder.HasIndex(page => new { page.SiteId, page.Status });
    }
}
