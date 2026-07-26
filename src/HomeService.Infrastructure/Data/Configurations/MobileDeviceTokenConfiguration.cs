using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MobileDeviceTokenConfiguration : IEntityTypeConfiguration<MobileDeviceToken>
{
    public void Configure(EntityTypeBuilder<MobileDeviceToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.OwnerType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(token => token.Platform).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(token => token.Token).HasMaxLength(4096).IsRequired();
        builder.Property(token => token.DeviceLabel).HasMaxLength(120);
        builder.Property(token => token.FailureReason).HasMaxLength(500);
        builder.HasIndex(token => new { token.OwnerType, token.OwnerId, token.IsActive });
        builder.HasIndex(token => token.Token).IsUnique();
    }
}
