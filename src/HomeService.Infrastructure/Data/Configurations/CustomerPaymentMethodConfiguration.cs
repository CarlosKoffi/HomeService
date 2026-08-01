using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CustomerPaymentMethodConfiguration : IEntityTypeConfiguration<CustomerPaymentMethod>
{
    public void Configure(EntityTypeBuilder<CustomerPaymentMethod> builder)
    {
        builder.HasKey(paymentMethod => paymentMethod.Id);
        builder.Property(paymentMethod => paymentMethod.Method).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(paymentMethod => paymentMethod.Label).HasMaxLength(120).IsRequired();
        builder.Property(paymentMethod => paymentMethod.MaskedReference).HasMaxLength(120);
        builder.HasIndex(paymentMethod => new { paymentMethod.CustomerId, paymentMethod.IsDefault });
        builder.HasOne(paymentMethod => paymentMethod.Customer).WithMany().HasForeignKey(paymentMethod => paymentMethod.CustomerId);
        builder.HasOne(paymentMethod => paymentMethod.PaymentProvider).WithMany().HasForeignKey(paymentMethod => paymentMethod.PaymentProviderId).OnDelete(DeleteBehavior.SetNull);
    }
}
