using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyPortalUserConfiguration : IEntityTypeConfiguration<CompanyPortalUser>
{
    public void Configure(EntityTypeBuilder<CompanyPortalUser> builder)
    {
        builder.HasKey(user => user.Id);
        builder.Property(user => user.FullName).HasMaxLength(160).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(256).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasOne(user => user.Company)
            .WithMany()
            .HasForeignKey(user => user.CompanyId);
    }
}
