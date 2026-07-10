using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class NotificationOutboxMessageConfiguration : IEntityTypeConfiguration<NotificationOutboxMessage>
{
    public void Configure(EntityTypeBuilder<NotificationOutboxMessage> builder)
    {
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Channel).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(notification => notification.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(notification => notification.Recipient).HasMaxLength(256).IsRequired();
        builder.Property(notification => notification.Subject).HasMaxLength(180).IsRequired();
        builder.Property(notification => notification.Body).HasMaxLength(2000).IsRequired();
        builder.Property(notification => notification.RelatedEntityType).HasMaxLength(80);
        builder.Property(notification => notification.MetadataJson).HasColumnType("jsonb");
        builder.Property(notification => notification.FailureReason).HasMaxLength(1000);
        builder.HasIndex(notification => new { notification.Status, notification.Channel, notification.ScheduledAt });
        builder.HasIndex(notification => new { notification.RelatedEntityType, notification.RelatedEntityId });
    }
}
