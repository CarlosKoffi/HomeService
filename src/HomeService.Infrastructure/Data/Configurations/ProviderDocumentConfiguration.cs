using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderDocumentConfiguration : IEntityTypeConfiguration<ProviderDocument>
{
    public void Configure(EntityTypeBuilder<ProviderDocument> builder)
    {
        builder.HasKey(document => document.Id);
        builder.Property(document => document.DocumentType).HasConversion<string>().HasMaxLength(40);
        builder.Property(document => document.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(document => document.StoragePath).HasMaxLength(700).IsRequired();
        builder.Property(document => document.ContentType).HasMaxLength(120).IsRequired();
        builder.HasIndex(document => new { document.ProviderId, document.DocumentType });
    }
}
