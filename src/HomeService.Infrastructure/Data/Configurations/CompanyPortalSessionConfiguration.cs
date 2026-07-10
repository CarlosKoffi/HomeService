using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyPortalSessionConfiguration : IEntityTypeConfiguration<CompanyPortalSession>
{
    public void Configure(EntityTypeBuilder<CompanyPortalSession> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => new { session.CompanyPortalUserId, session.ExpiresAt });
        builder.HasOne(session => session.CompanyPortalUser).WithMany().HasForeignKey(session => session.CompanyPortalUserId);
    }
}
