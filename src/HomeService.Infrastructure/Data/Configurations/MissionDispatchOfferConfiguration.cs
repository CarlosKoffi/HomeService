using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionDispatchOfferConfiguration : IEntityTypeConfiguration<MissionDispatchOffer>
{
    public void Configure(EntityTypeBuilder<MissionDispatchOffer> builder)
    {
        builder.HasKey(offer => offer.Id);
        builder.Property(offer => offer.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(offer => offer.ScoreDetails).HasMaxLength(1200).IsRequired();

        builder.HasOne(offer => offer.Mission)
            .WithMany()
            .HasForeignKey(offer => offer.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(offer => offer.Company)
            .WithMany()
            .HasForeignKey(offer => offer.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(offer => new { offer.MissionId, offer.CompanyId }).IsUnique();
        builder.HasIndex(offer => new { offer.MissionId, offer.Status, offer.Rank });
        builder.HasIndex(offer => new { offer.CompanyId, offer.Status, offer.ExpiresAt });
    }
}
