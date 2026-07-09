using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.HasKey(language => language.Id);
        builder.Property(language => language.Code).HasMaxLength(12).IsRequired();
        builder.Property(language => language.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(language => language.Code).IsUnique();
    }
}
