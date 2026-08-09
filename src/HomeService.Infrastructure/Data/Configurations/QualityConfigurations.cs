using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderPrestationQualificationConfiguration : IEntityTypeConfiguration<ProviderPrestationQualification>
{
    public void Configure(EntityTypeBuilder<ProviderPrestationQualification> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ReviewNote).HasMaxLength(1200);
        builder.HasOne(item => item.Provider).WithMany().HasForeignKey(item => item.ProviderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ServicePrestation).WithMany().HasForeignKey(item => item.ServicePrestationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.ProviderId, item.ServicePrestationId }).IsUnique();
        builder.HasIndex(item => new { item.ServicePrestationId, item.Status });
    }
}

public sealed class QualityChecklistTemplateConfiguration : IEntityTypeConfiguration<QualityChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<QualityChecklistTemplate> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(800);
        builder.HasOne(item => item.Service).WithMany().HasForeignKey(item => item.ServiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ServicePrestation).WithMany().HasForeignKey(item => item.ServicePrestationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.Items).WithOne(item => item.Template).HasForeignKey(item => item.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.ServiceId, item.ServicePrestationId, item.Version });
        builder.HasIndex(item => new { item.IsActive, item.ServiceId });
    }
}

public sealed class QualityChecklistItemConfiguration : IEntityTypeConfiguration<QualityChecklistItem>
{
    public void Configure(EntityTypeBuilder<QualityChecklistItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Label).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Guidance).HasMaxLength(600);
        builder.HasOne(item => item.ServiceOption).WithMany().HasForeignKey(item => item.ServiceOptionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(item => new { item.TemplateId, item.Code }).IsUnique();
        builder.HasIndex(item => new { item.TemplateId, item.Stage, item.SortOrder });
    }
}

public sealed class MissionQualityControlConfiguration : IEntityTypeConfiguration<MissionQualityControl>
{
    public void Configure(EntityTypeBuilder<MissionQualityControl> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasOne(item => item.Mission).WithMany().HasForeignKey(item => item.MissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Template).WithMany().HasForeignKey(item => item.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Items).WithOne(item => item.Control).HasForeignKey(item => item.ControlId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => item.MissionId).IsUnique();
        builder.HasIndex(item => item.Status);
    }
}

public sealed class MissionQualityItemConfiguration : IEntityTypeConfiguration<MissionQualityItem>
{
    public void Configure(EntityTypeBuilder<MissionQualityItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Label).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Guidance).HasMaxLength(600);
        builder.Property(item => item.TextValue).HasMaxLength(1200);
        builder.HasOne(item => item.EvidenceAttachment).WithMany().HasForeignKey(item => item.EvidenceAttachmentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(item => new { item.ControlId, item.Code }).IsUnique();
        builder.HasIndex(item => new { item.ControlId, item.Stage, item.SortOrder });
    }
}

public sealed class MissionQualityAuditConfiguration : IEntityTypeConfiguration<MissionQualityAudit>
{
    public void Configure(EntityTypeBuilder<MissionQualityAudit> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SamplingReason).HasMaxLength(240).IsRequired();
        builder.Property(item => item.ReviewNote).HasMaxLength(2000);
        builder.HasOne(item => item.Mission).WithMany().HasForeignKey(item => item.MissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Provider).WithMany().HasForeignKey(item => item.ProviderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Service).WithMany().HasForeignKey(item => item.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ServicePrestation).WithMany().HasForeignKey(item => item.ServicePrestationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.MissionId).IsUnique();
        builder.HasIndex(item => new { item.Status, item.CreatedAt });
        builder.HasIndex(item => new { item.ProviderId, item.CreatedAt });
    }
}

public sealed class ProviderQualitySummaryConfiguration : IEntityTypeConfiguration<ProviderQualitySummary>
{
    public void Configure(EntityTypeBuilder<ProviderQualitySummary> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.AverageRating).HasPrecision(4, 2);
        builder.Property(item => item.PunctualityRate).HasPrecision(5, 2);
        builder.HasOne(item => item.Provider).WithMany().HasForeignKey(item => item.ProviderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Service).WithMany().HasForeignKey(item => item.ServiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ServicePrestation).WithMany().HasForeignKey(item => item.ServicePrestationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.ProviderId, item.ServiceId, item.ServicePrestationId }).IsUnique();
        builder.HasIndex(item => new { item.ServiceId, item.ServicePrestationId, item.Score });
    }
}

public sealed class CompanyQualitySummaryConfiguration : IEntityTypeConfiguration<CompanyQualitySummary>
{
    public void Configure(EntityTypeBuilder<CompanyQualitySummary> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.AverageRating).HasPrecision(4, 2);
        builder.Property(item => item.AuditPassRate).HasPrecision(5, 2);
        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Service).WithMany().HasForeignKey(item => item.ServiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ServicePrestation).WithMany().HasForeignKey(item => item.ServicePrestationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.CompanyId, item.ServiceId, item.ServicePrestationId }).IsUnique();
        builder.HasIndex(item => new { item.ServiceId, item.ServicePrestationId, item.Score });
    }
}
