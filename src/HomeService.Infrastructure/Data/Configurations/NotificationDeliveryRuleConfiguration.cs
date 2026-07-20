using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class NotificationDeliveryRuleConfiguration : IEntityTypeConfiguration<NotificationDeliveryRule>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryRule> builder)
    {
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.EventKey).HasMaxLength(96).IsRequired();
        builder.Property(rule => rule.Label).HasMaxLength(180).IsRequired();
        builder.Property(rule => rule.Audience).HasMaxLength(32).IsRequired();
        builder.HasIndex(rule => rule.EventKey).IsUnique();
        builder.HasIndex(rule => new { rule.Audience, rule.EventKey });
    }
}
