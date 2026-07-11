using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderCandidateServiceConfiguration : IEntityTypeConfiguration<ProviderCandidateService>
{
    public void Configure(EntityTypeBuilder<ProviderCandidateService> builder)
    {
        builder.HasKey(candidateService => candidateService.Id);
        builder.Property(candidateService => candidateService.ExperienceLevel).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(candidateService => candidateService.Service)
            .WithMany()
            .HasForeignKey(candidateService => candidateService.ServiceId);
        builder.HasIndex(candidateService => new { candidateService.ProviderId, candidateService.ServiceId }).IsUnique();
        builder.HasIndex(candidateService => new { candidateService.ServiceId, candidateService.IsActive });
    }
}
