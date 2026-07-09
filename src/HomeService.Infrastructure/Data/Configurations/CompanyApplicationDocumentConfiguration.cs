using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyApplicationDocumentConfiguration : IEntityTypeConfiguration<CompanyApplicationDocument>
{
    public void Configure(EntityTypeBuilder<CompanyApplicationDocument> builder)
    {
        builder.HasKey(document => document.Id);
        builder.Property(document => document.DocumentType).HasConversion<string>().HasMaxLength(48);
        builder.Property(document => document.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(document => document.StoragePath).HasMaxLength(500).IsRequired();
        builder.Property(document => document.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(document => document.ReviewStatus).HasConversion<string>().HasMaxLength(40);
        builder.Property(document => document.ReviewNote).HasMaxLength(800);
    }
}
