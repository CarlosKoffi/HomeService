using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionAttachmentConfiguration : IEntityTypeConfiguration<MissionAttachment>
{
    public void Configure(EntityTypeBuilder<MissionAttachment> builder)
    {
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.AttachmentType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(attachment => attachment.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(attachment => attachment.StoragePath).HasMaxLength(720).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(attachment => attachment.Caption).HasMaxLength(500);
        builder.Property(attachment => attachment.IsDeleted).HasDefaultValue(false);
        builder.HasOne(attachment => attachment.Mission)
            .WithMany()
            .HasForeignKey(attachment => attachment.MissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(attachment => new { attachment.MissionId, attachment.AttachmentType, attachment.IsDeleted });
    }
}
