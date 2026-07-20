using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ContactRequestConfiguration : IEntityTypeConfiguration<ContactRequest>
{
    public void Configure(EntityTypeBuilder<ContactRequest> builder)
    {
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Source).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(request => request.FullName).HasMaxLength(180).IsRequired();
        builder.Property(request => request.CompanyName).HasMaxLength(180);
        builder.Property(request => request.PhoneNumber).HasMaxLength(80).IsRequired();
        builder.Property(request => request.Email).HasMaxLength(220);
        builder.Property(request => request.Subject).HasMaxLength(180).IsRequired();
        builder.Property(request => request.Message).HasMaxLength(2000).IsRequired();
        builder.Property(request => request.AdminNote).HasMaxLength(1000);
        builder.HasIndex(request => new { request.Status, request.CreatedAt });
        builder.HasIndex(request => request.Source);
    }
}
