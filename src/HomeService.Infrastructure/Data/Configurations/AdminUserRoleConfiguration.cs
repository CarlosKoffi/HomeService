using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class AdminUserRoleConfiguration : IEntityTypeConfiguration<AdminUserRole>
{
    public void Configure(EntityTypeBuilder<AdminUserRole> builder)
    {
        builder.HasKey(role => role.Id);
        builder.HasOne(role => role.Role)
            .WithMany()
            .HasForeignKey(role => role.RoleId);
        builder.HasIndex(role => new { role.AdminUserId, role.RoleId }).IsUnique();
    }
}
