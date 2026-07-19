using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionFinancialBreakdownConfiguration : IEntityTypeConfiguration<MissionFinancialBreakdown>
{
    public void Configure(EntityTypeBuilder<MissionFinancialBreakdown> builder)
    {
        builder.HasKey(line => line.Id);
        builder.Property(line => line.LineType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(line => line.Label).HasMaxLength(160).IsRequired();
        builder.Property(line => line.Currency).HasMaxLength(8).IsRequired();
        builder.HasOne(line => line.Mission)
            .WithMany()
            .HasForeignKey(line => line.MissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(line => new { line.MissionId, line.LineType, line.SortOrder });
    }
}
