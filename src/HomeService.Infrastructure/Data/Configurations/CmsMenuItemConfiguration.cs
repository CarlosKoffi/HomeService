using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsMenuItemConfiguration : IEntityTypeConfiguration<CmsMenuItem>
{
    public void Configure(EntityTypeBuilder<CmsMenuItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Label).HasMaxLength(160).IsRequired();
        builder.Property(item => item.TargetType).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.TargetValue).HasMaxLength(700);
        builder.Property(item => item.IconName).HasMaxLength(80);

        builder.HasOne(item => item.ParentMenuItem)
            .WithMany()
            .HasForeignKey(item => item.ParentMenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Page)
            .WithMany()
            .HasForeignKey(item => item.PageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.MenuId, item.ParentMenuItemId, item.Position }).IsUnique();
    }
}
