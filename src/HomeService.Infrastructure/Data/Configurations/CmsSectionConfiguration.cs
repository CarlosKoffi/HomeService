using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsSectionConfiguration : IEntityTypeConfiguration<CmsSection>
{
    public void Configure(EntityTypeBuilder<CmsSection> builder)
    {
        builder.HasKey(section => section.Id);
        builder.Property(section => section.InternalName).HasMaxLength(180).IsRequired();
        builder.Property(section => section.Zone).HasMaxLength(80).IsRequired();
        builder.Property(section => section.Anchor).HasMaxLength(120);
        builder.Property(section => section.Variant).HasMaxLength(120);

        builder.HasOne(section => section.ComponentDefinition)
            .WithMany()
            .HasForeignKey(section => section.ComponentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(section => section.ContentValues)
            .WithOne(value => value.Section)
            .HasForeignKey(value => value.SectionId);

        builder.HasIndex(section => new { section.PageVersionId, section.Zone, section.Position }).IsUnique();
        builder.HasIndex(section => new { section.ComponentDefinitionId, section.IsActive });
    }
}
