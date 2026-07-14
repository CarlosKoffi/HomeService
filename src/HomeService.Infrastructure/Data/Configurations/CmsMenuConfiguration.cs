using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsMenuConfiguration : IEntityTypeConfiguration<CmsMenu>
{
    public void Configure(EntityTypeBuilder<CmsMenu> builder)
    {
        builder.HasKey(menu => menu.Id);
        builder.Property(menu => menu.Code).HasMaxLength(80).IsRequired();
        builder.Property(menu => menu.Name).HasMaxLength(160).IsRequired();
        builder.Property(menu => menu.Placement).HasMaxLength(80).IsRequired();

        builder.HasMany(menu => menu.Items)
            .WithOne(item => item.Menu)
            .HasForeignKey(item => item.MenuId);

        builder.HasIndex(menu => new { menu.SiteId, menu.Code }).IsUnique();
        builder.HasIndex(menu => new { menu.SiteId, menu.Placement });
    }
}
