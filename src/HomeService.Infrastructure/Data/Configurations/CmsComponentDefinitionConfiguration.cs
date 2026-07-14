using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsComponentDefinitionConfiguration : IEntityTypeConfiguration<CmsComponentDefinition>
{
    public void Configure(EntityTypeBuilder<CmsComponentDefinition> builder)
    {
        builder.HasKey(component => component.Id);
        builder.Property(component => component.Key).HasMaxLength(120).IsRequired();
        builder.Property(component => component.Name).HasMaxLength(160).IsRequired();
        builder.Property(component => component.Description).HasMaxLength(700);
        builder.HasIndex(component => new { component.Key, component.SchemaVersion }).IsUnique();
    }
}
