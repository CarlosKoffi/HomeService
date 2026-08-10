using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionPaymentRequestConfiguration : IEntityTypeConfiguration<MissionPaymentRequest>
{
    public void Configure(EntityTypeBuilder<MissionPaymentRequest> builder)
    {
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Reference).HasMaxLength(120).IsRequired();
        builder.Property(payment => payment.ExternalPaymentRequestId).HasMaxLength(160);
        builder.Property(payment => payment.ExternalTransactionId).HasMaxLength(160);
        builder.Property(payment => payment.ProviderCode).HasMaxLength(40).IsRequired();
        builder.Property(payment => payment.Currency).HasMaxLength(8).IsRequired();
        builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(payment => payment.RedirectUrl).HasMaxLength(1000);
        builder.Property(payment => payment.FailureMessage).HasMaxLength(500);

        builder.HasOne(payment => payment.Mission)
            .WithMany()
            .HasForeignKey(payment => payment.MissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(payment => payment.Customer)
            .WithMany()
            .HasForeignKey(payment => payment.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(payment => payment.CustomerPaymentMethod)
            .WithMany()
            .HasForeignKey(payment => payment.CustomerPaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(payment => payment.Reference).IsUnique();
        builder.HasIndex(payment => payment.ExternalPaymentRequestId).IsUnique();
        builder.HasIndex(payment => payment.ExternalTransactionId);
        builder.HasIndex(payment => new { payment.MissionId, payment.Status, payment.CreatedAt });
        builder.HasIndex(payment => payment.MissionId)
            .HasDatabaseName("IX_MissionPaymentRequests_OnePendingPerMission")
            .HasFilter("\"Status\" = 'Pending'")
            .IsUnique();
    }
}
