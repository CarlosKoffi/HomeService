using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.HasKey(user => user.Id);
        builder.Property(user => user.FullName).HasMaxLength(160).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(256).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(256);
        builder.Property(user => user.InvitationTokenHash).HasMaxLength(128);
        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasIndex(user => user.InvitationTokenHash);
        builder.HasMany(user => user.Roles)
            .WithOne(role => role.AdminUser)
            .HasForeignKey(role => role.AdminUserId);
    }
}
