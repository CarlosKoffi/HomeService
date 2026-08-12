using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class AdminFinancialApprovalConfiguration : IEntityTypeConfiguration<AdminFinancialApproval>
{
    public void Configure(EntityTypeBuilder<AdminFinancialApproval> builder)
    {
        builder.HasKey(approval => approval.Id);
        builder.Property(approval => approval.Operation).HasMaxLength(80).IsRequired();
        builder.Property(approval => approval.PayloadHash).HasMaxLength(128).IsRequired();
        builder.HasOne(approval => approval.AdminUser)
            .WithMany()
            .HasForeignKey(approval => approval.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(approval => new
        {
            approval.AdminUserId,
            approval.Operation,
            approval.ResourceId,
            approval.PayloadHash
        });
        builder.HasIndex(approval => new
        {
            approval.Operation,
            approval.ResourceId,
            approval.PayloadHash,
            approval.ExpiresAt
        });
    }
}
