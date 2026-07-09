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
        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasMany(user => user.Roles)
            .WithOne(role => role.AdminUser)
            .HasForeignKey(role => role.AdminUserId);
    }
}
