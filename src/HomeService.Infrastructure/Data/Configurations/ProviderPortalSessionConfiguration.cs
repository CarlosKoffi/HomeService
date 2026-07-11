using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderPortalSessionConfiguration : IEntityTypeConfiguration<ProviderPortalSession>
{
    public void Configure(EntityTypeBuilder<ProviderPortalSession> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasOne(session => session.Provider)
            .WithMany()
            .HasForeignKey(session => session.ProviderId);
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => new { session.ProviderId, session.ExpiresAt });
    }
}
