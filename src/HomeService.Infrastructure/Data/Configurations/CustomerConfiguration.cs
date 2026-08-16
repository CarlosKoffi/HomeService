using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.FirstName).HasMaxLength(120).IsRequired();
        builder.Property(customer => customer.LastName).HasMaxLength(120).IsRequired();
        builder.Property(customer => customer.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(customer => customer.AccountType).IsRequired();
        builder.Property(customer => customer.Email).HasMaxLength(180);
        builder.Property(customer => customer.PasswordHash).HasMaxLength(512);
        builder.Property(customer => customer.ProfilePhotoPath).HasMaxLength(500);
        builder.HasIndex(customer => new { customer.PhoneNumber, customer.AccountType }).IsUnique();
        builder.HasIndex(customer => customer.Email);
    }
}
