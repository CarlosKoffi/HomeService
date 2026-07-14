using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsPageVersionConfiguration : IEntityTypeConfiguration<CmsPageVersion>
{
    public void Configure(EntityTypeBuilder<CmsPageVersion> builder)
    {
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasMany(version => version.Sections)
            .WithOne(section => section.PageVersion)
            .HasForeignKey(section => section.PageVersionId);

        builder.HasIndex(version => new { version.PageId, version.VersionNumber }).IsUnique();
        builder.HasIndex(version => new { version.PageId, version.Status });
    }
}
