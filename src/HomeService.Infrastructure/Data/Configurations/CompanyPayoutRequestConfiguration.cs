using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyPayoutRequestConfiguration : IEntityTypeConfiguration<CompanyPayoutRequest>
{
    public void Configure(EntityTypeBuilder<CompanyPayoutRequest> builder)
    {
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Method).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(request => request.Frequency).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(request => request.Currency).HasMaxLength(8).IsRequired();
        builder.Property(request => request.Reference).HasMaxLength(40).IsRequired();
        builder.Property(request => request.ExternalTransactionId).HasMaxLength(160);
        builder.Property(request => request.FailureReason).HasMaxLength(1000);
        builder.Property(request => request.ProofReference).HasMaxLength(500);
        builder.HasOne(request => request.Destination)
            .WithMany()
            .HasForeignKey(request => request.DestinationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(request => request.Reference).IsUnique();
        builder.HasIndex(request => new { request.CompanyId, request.Status });
        builder.HasIndex(request => request.ExternalTransactionId);
    }
}
