using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderMissionAssignmentConfiguration : IEntityTypeConfiguration<ProviderMissionAssignment>
{
    public void Configure(EntityTypeBuilder<ProviderMissionAssignment> builder)
    {
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(assignment => assignment.RefusalReason).HasConversion<string>().HasMaxLength(32);
        builder.Property(assignment => assignment.RefusalComment).HasMaxLength(600);
        builder.Property(assignment => assignment.CompletionNote).HasMaxLength(1000);
        builder.Property(assignment => assignment.CompletionPhotoPath).HasMaxLength(640);
        builder.Property(assignment => assignment.OfferedLatitude).HasPrecision(9, 6);
        builder.Property(assignment => assignment.OfferedLongitude).HasPrecision(9, 6);
        builder.Property(assignment => assignment.AcceptedLatitude).HasPrecision(9, 6);
        builder.Property(assignment => assignment.AcceptedLongitude).HasPrecision(9, 6);
        builder.Property(assignment => assignment.ArrivalLatitude).HasPrecision(9, 6);
        builder.Property(assignment => assignment.ArrivalLongitude).HasPrecision(9, 6);
        builder.Property(assignment => assignment.ArrivalVerificationStatus).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(assignment => assignment.Mission)
            .WithMany()
            .HasForeignKey(assignment => assignment.MissionId);
        builder.HasOne(assignment => assignment.Provider)
            .WithMany()
            .HasForeignKey(assignment => assignment.ProviderId);
        builder.HasOne(assignment => assignment.Company)
            .WithMany()
            .HasForeignKey(assignment => assignment.CompanyId);
        builder.HasIndex(assignment => new { assignment.ProviderId, assignment.Status });
        builder.HasIndex(assignment => new { assignment.MissionId, assignment.ProviderId });
        builder.HasIndex(assignment => new { assignment.ProviderId, assignment.ArrivalVerificationStatus });
    }
}
