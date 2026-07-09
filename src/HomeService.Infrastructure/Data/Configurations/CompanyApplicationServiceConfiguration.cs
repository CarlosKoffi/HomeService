using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CompanyApplicationServiceConfiguration : IEntityTypeConfiguration<CompanyApplicationService>
{
    public void Configure(EntityTypeBuilder<CompanyApplicationService> builder)
    {
        builder.HasKey(service => service.Id);
        builder.Property(service => service.RawName).HasMaxLength(160).IsRequired();
        builder.Property(service => service.NormalizedName).HasMaxLength(160).IsRequired();
        builder.Property(service => service.MatchStatus).HasConversion<string>().HasMaxLength(40);
        builder.Property(service => service.ReviewNote).HasMaxLength(800);
        builder.HasOne(service => service.MatchedService)
            .WithMany()
            .HasForeignKey(service => service.MatchedServiceId);
        builder.HasIndex(service => new { service.CompanyApplicationId, service.NormalizedName }).IsUnique();
    }
}
