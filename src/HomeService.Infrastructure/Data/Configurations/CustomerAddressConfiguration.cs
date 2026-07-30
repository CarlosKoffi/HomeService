using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.HasKey(address => address.Id);
        builder.Property(address => address.Label).HasMaxLength(80).IsRequired();
        builder.Property(address => address.AddressLine).HasMaxLength(300).IsRequired();
        builder.Property(address => address.Latitude).HasPrecision(10, 7);
        builder.Property(address => address.Longitude).HasPrecision(10, 7);
        builder.HasIndex(address => new { address.CustomerId, address.IsDefault });
        builder.HasOne(address => address.Customer).WithMany().HasForeignKey(address => address.CustomerId);
    }
}
