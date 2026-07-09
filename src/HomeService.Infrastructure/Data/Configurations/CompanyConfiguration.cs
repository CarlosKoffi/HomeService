using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(company => company.Id);
        builder.Property(company => company.Name).HasMaxLength(160).IsRequired();
        builder.Property(company => company.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(company => company.Email).HasMaxLength(256);
        builder.Property(company => company.Status).HasConversion<string>().HasMaxLength(32);
    }
}
