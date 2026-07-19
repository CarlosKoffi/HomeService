using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionStatusHistoryConfiguration : IEntityTypeConfiguration<MissionStatusHistory>
{
    public void Configure(EntityTypeBuilder<MissionStatusHistory> builder)
    {
        builder.HasKey(history => history.Id);
        builder.Property(history => history.FromStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(history => history.ToStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(history => history.ActorType).HasMaxLength(40).IsRequired();
        builder.Property(history => history.Note).HasMaxLength(1000);
        builder.HasOne(history => history.Mission)
            .WithMany()
            .HasForeignKey(history => history.MissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(history => new { history.MissionId, history.CreatedAt });
    }
}
