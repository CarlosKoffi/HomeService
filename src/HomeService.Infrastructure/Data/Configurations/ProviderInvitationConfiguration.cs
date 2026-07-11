using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class ProviderInvitationConfiguration : IEntityTypeConfiguration<ProviderInvitation>
{
    public void Configure(EntityTypeBuilder<ProviderInvitation> builder)
    {
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Code).HasMaxLength(16).IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(invitation => invitation.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(invitation => invitation.InvitationLink).HasMaxLength(1000);
        builder.HasOne(invitation => invitation.Provider)
            .WithMany()
            .HasForeignKey(invitation => invitation.ProviderId);
        builder.HasOne(invitation => invitation.Company)
            .WithMany()
            .HasForeignKey(invitation => invitation.CompanyId);
        builder.HasIndex(invitation => invitation.Code).IsUnique();
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new { invitation.ProviderId, invitation.Status });
    }
}
