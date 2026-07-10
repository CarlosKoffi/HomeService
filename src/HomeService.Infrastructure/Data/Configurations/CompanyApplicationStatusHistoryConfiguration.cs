using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<CompanyApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<CompanyApplicationStatusHistory> builder)
    {
        builder.HasKey(history => history.Id);
        builder.Property(history => history.PreviousStatus).HasConversion<string>().HasMaxLength(40);
        builder.Property(history => history.NewStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(history => history.Note).HasMaxLength(1000);
        builder.Property(history => history.ChangedBy).HasMaxLength(256);
        builder.HasIndex(history => new { history.CompanyApplicationId, history.ChangedAt });
    }
}
