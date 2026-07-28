using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class AdminSessionConfiguration : IEntityTypeConfiguration<AdminSession>
{
    public void Configure(EntityTypeBuilder<AdminSession> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => new { session.AdminUserId, session.ExpiresAt });
        builder.HasOne(session => session.AdminUser)
            .WithMany()
            .HasForeignKey(session => session.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
