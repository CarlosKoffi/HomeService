using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.HasKey(mission => mission.Id);
        builder.Property(mission => mission.Mode).HasConversion<string>().HasMaxLength(32);
        builder.Property(mission => mission.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(mission => mission.PaymentMethod).HasConversion<string>().HasMaxLength(32);
        builder.Property(mission => mission.PaymentStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(mission => mission.Currency).HasMaxLength(3).IsRequired();
        builder.Property(mission => mission.ServiceAddress).HasMaxLength(360);
        builder.Property(mission => mission.ServiceLatitude).HasPrecision(9, 6);
        builder.Property(mission => mission.ServiceLongitude).HasPrecision(9, 6);
        builder.Ignore(mission => mission.CanRevealContactDetails);
        builder.HasIndex(mission => new { mission.ServiceId, mission.Status });
    }
}
