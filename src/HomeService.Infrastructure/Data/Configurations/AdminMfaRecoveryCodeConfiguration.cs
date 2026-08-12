using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class AdminMfaRecoveryCodeConfiguration : IEntityTypeConfiguration<AdminMfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<AdminMfaRecoveryCode> builder)
    {
        builder.HasKey(code => code.Id);
        builder.Property(code => code.CodeHash).HasMaxLength(128).IsRequired();
        builder.HasOne(code => code.AdminUser)
            .WithMany()
            .HasForeignKey(code => code.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(code => new { code.AdminUserId, code.CodeHash }).IsUnique();
    }
}
