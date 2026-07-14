using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CmsSiteConfiguration : IEntityTypeConfiguration<CmsSite>
{
    public void Configure(EntityTypeBuilder<CmsSite> builder)
    {
        builder.HasKey(site => site.Id);
        builder.Property(site => site.Code).HasMaxLength(80).IsRequired();
        builder.Property(site => site.Name).HasMaxLength(160).IsRequired();
        builder.Property(site => site.Surface).HasConversion<string>().HasMaxLength(40);
        builder.Property(site => site.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(site => site.HomePageCode).HasMaxLength(80);

        builder.HasOne(site => site.DefaultCountry)
            .WithMany()
            .HasForeignKey(site => site.DefaultCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(site => site.DefaultLanguage)
            .WithMany()
            .HasForeignKey(site => site.DefaultLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(site => site.Pages)
            .WithOne(page => page.Site)
            .HasForeignKey(page => page.SiteId);

        builder.HasMany(site => site.Menus)
            .WithOne(menu => menu.Site)
            .HasForeignKey(menu => menu.SiteId);

        builder.HasIndex(site => site.Code).IsUnique();
        builder.HasIndex(site => new { site.Surface, site.DefaultCountryId });
    }
}
