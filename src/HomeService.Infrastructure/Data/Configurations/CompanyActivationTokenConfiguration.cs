using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyActivationTokenConfiguration : IEntityTypeConfiguration<CompanyActivationToken>
{
    public void Configure(EntityTypeBuilder<CompanyActivationToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash).HasMaxLength(256).IsRequired();
        builder.Property(token => token.ActivationLink).HasMaxLength(1200).IsRequired();
        builder.Property(token => token.RevocationReason).HasMaxLength(500);
        builder.Ignore(token => token.IsActive);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.CompanyApplicationId, token.ExpiresAt });
    }
}
