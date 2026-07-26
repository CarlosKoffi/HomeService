using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.HasKey(template => template.Id);
        builder.Property(template => template.EventKey).HasMaxLength(96).IsRequired();
        builder.Property(template => template.Channel).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(template => template.Label).HasMaxLength(180).IsRequired();
        builder.Property(template => template.Audience).HasMaxLength(32).IsRequired();
        builder.Property(template => template.SubjectTemplate).HasMaxLength(180).IsRequired();
        builder.Property(template => template.BodyTemplate).HasMaxLength(2000).IsRequired();
        builder.Property(template => template.AvailableVariables).HasMaxLength(1000);
        builder.HasIndex(template => new { template.EventKey, template.Channel }).IsUnique();
        builder.HasIndex(template => new { template.Audience, template.Channel, template.IsActive });
        builder.HasOne(template => template.DeliveryRule)
            .WithMany(rule => rule.Templates)
            .HasPrincipalKey(rule => rule.EventKey)
            .HasForeignKey(template => template.EventKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
