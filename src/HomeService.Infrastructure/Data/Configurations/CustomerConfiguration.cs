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
    }
}
