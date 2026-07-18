using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyPortalNotificationConfiguration : IEntityTypeConfiguration<CompanyPortalNotification>
{
    public void Configure(EntityTypeBuilder<CompanyPortalNotification> builder)
    {
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Type).HasMaxLength(64).IsRequired();
        builder.Property(notification => notification.Title).HasMaxLength(160).IsRequired();
        builder.Property(notification => notification.Message).HasMaxLength(700).IsRequired();
        builder.Property(notification => notification.Tone).HasMaxLength(32).IsRequired();
        builder.Property(notification => notification.ActionUrl).HasMaxLength(240);

        builder.HasIndex(notification => new { notification.CompanyId, notification.IsRead, notification.OccurredAt });
        builder.HasIndex(notification => new { notification.CompanyApplicationId, notification.OccurredAt });
        builder.HasIndex(notification => new { notification.CompanyApplicationDocumentId, notification.OccurredAt });

        builder.HasOne(notification => notification.Company)
            .WithMany()
            .HasForeignKey(notification => notification.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(notification => notification.CompanyApplication)
            .WithMany()
            .HasForeignKey(notification => notification.CompanyApplicationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(notification => notification.CompanyApplicationDocument)
            .WithMany()
            .HasForeignKey(notification => notification.CompanyApplicationDocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
