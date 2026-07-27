using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionWorkflowSettingConfiguration : IEntityTypeConfiguration<MissionWorkflowSetting>
{
    public void Configure(EntityTypeBuilder<MissionWorkflowSetting> builder)
    {
        builder.HasKey(setting => setting.Id);
        builder.Property(setting => setting.Key).HasMaxLength(96).IsRequired();
        builder.Property(setting => setting.Label).HasMaxLength(180).IsRequired();
        builder.Property(setting => setting.Description).HasMaxLength(360).IsRequired();
        builder.Property(setting => setting.Unit).HasMaxLength(40).IsRequired();
        builder.Property(setting => setting.IsActive).HasDefaultValue(true);
        builder.HasIndex(setting => setting.Key).IsUnique();
        builder.HasIndex(setting => new { setting.IsActive, setting.SortOrder });
    }
}
