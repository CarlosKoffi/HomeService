using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderAffiliationRequestConfiguration : IEntityTypeConfiguration<ProviderAffiliationRequest>
{
    public void Configure(EntityTypeBuilder<ProviderAffiliationRequest> builder)
    {
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(request => request.Message).HasMaxLength(800);
        builder.Property(request => request.ReviewNote).HasMaxLength(800);
        builder.HasOne(request => request.Provider)
            .WithMany()
            .HasForeignKey(request => request.ProviderId);
        builder.HasOne(request => request.Company)
            .WithMany()
            .HasForeignKey(request => request.CompanyId);
        builder.HasIndex(request => new { request.CompanyId, request.Status, request.RequestedAt });
        builder.HasIndex(request => new { request.ProviderId, request.CompanyId, request.Status });
    }
}
