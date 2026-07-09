using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class AdminRolePermissionConfiguration : IEntityTypeConfiguration<AdminRolePermission>
{
    public void Configure(EntityTypeBuilder<AdminRolePermission> builder)
    {
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Action).HasConversion<string>().HasMaxLength(80);
        builder.HasOne(permission => permission.Module)
            .WithMany()
            .HasForeignKey(permission => permission.ModuleId);
        builder.HasIndex(permission => new { permission.RoleId, permission.ModuleId, permission.Action }).IsUnique();
    }
}
