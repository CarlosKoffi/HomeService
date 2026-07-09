using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class AdminModuleConfiguration : IEntityTypeConfiguration<AdminModule>
{
    public void Configure(EntityTypeBuilder<AdminModule> builder)
    {
        builder.HasKey(module => module.Id);
        builder.Property(module => module.Key).HasConversion<string>().HasMaxLength(80);
        builder.Property(module => module.Name).HasMaxLength(140).IsRequired();
        builder.Property(module => module.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(module => module.Key).IsUnique();
    }
}
