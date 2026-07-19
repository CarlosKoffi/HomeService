using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> builder)
    {
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Name).HasMaxLength(160).IsRequired();
        builder.Property(rule => rule.Target).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(rule => rule.AssignmentSource).HasConversion<string>().HasMaxLength(32);
        builder.Property(rule => rule.Currency).HasMaxLength(8).IsRequired();
        builder.Property(rule => rule.IsActive).HasDefaultValue(true);
        builder.HasOne(rule => rule.Service)
            .WithMany()
            .HasForeignKey(rule => rule.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(rule => rule.ServicePrestation)
            .WithMany()
            .HasForeignKey(rule => rule.ServicePrestationId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(rule => rule.Company)
            .WithMany()
            .HasForeignKey(rule => rule.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(rule => new { rule.Target, rule.IsActive, rule.EffectiveFrom });
        builder.HasIndex(rule => new { rule.ServiceId, rule.ServicePrestationId, rule.CompanyId, rule.AssignmentSource });
    }
}
