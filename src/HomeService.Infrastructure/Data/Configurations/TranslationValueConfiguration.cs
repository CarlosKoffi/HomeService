using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class TranslationValueConfiguration : IEntityTypeConfiguration<TranslationValue>
{
    public void Configure(EntityTypeBuilder<TranslationValue> builder)
    {
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Value).HasMaxLength(4000).IsRequired();
        builder.HasOne(value => value.Language)
            .WithMany()
            .HasForeignKey(value => value.LanguageId);
        builder.HasOne(value => value.Country)
            .WithMany()
            .HasForeignKey(value => value.CountryId);
        builder.HasIndex(value => new { value.TranslationKeyId, value.LanguageId, value.CountryId }).IsUnique();
    }
}
