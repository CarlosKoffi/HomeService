using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class AdminRoleConfiguration : IEntityTypeConfiguration<AdminRole>
{
    public void Configure(EntityTypeBuilder<AdminRole> builder)
    {
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Name).HasMaxLength(120).IsRequired();
        builder.Property(role => role.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(role => role.Name).IsUnique();
        builder.HasMany(role => role.Permissions)
            .WithOne(permission => permission.Role)
            .HasForeignKey(permission => permission.RoleId);
    }
}
