using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class TranslationKeyConfiguration : IEntityTypeConfiguration<TranslationKey>
{
    public void Configure(EntityTypeBuilder<TranslationKey> builder)
    {
        builder.HasKey(key => key.Id);
        builder.Property(key => key.Key).HasMaxLength(180).IsRequired();
        builder.Property(key => key.Description).HasMaxLength(500).IsRequired();
        builder.Property(key => key.Scope).HasMaxLength(80).IsRequired();
        builder.HasIndex(key => key.Key).IsUnique();
        builder.HasMany(key => key.Values)
            .WithOne(value => value.TranslationKey)
            .HasForeignKey(value => value.TranslationKeyId);
    }
}
