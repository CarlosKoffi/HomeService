using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionReviewConfiguration : IEntityTypeConfiguration<MissionReview>
{
    public void Configure(EntityTypeBuilder<MissionReview> builder)
    {
        builder.HasKey(review => review.Id);
        builder.Property(review => review.Comment).HasMaxLength(1200);

        builder.HasOne(review => review.Mission)
            .WithMany()
            .HasForeignKey(review => review.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(review => review.Customer)
            .WithMany()
            .HasForeignKey(review => review.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(review => review.Company)
            .WithMany()
            .HasForeignKey(review => review.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(review => review.Provider)
            .WithMany()
            .HasForeignKey(review => review.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(review => review.MissionId).IsUnique();
        builder.HasIndex(review => new { review.CompanyId, review.SubmittedAt });
        builder.HasIndex(review => new { review.ProviderId, review.SubmittedAt });
    }
}
