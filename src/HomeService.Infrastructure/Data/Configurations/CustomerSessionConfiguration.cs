using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class CustomerSessionConfiguration : IEntityTypeConfiguration<CustomerSession>
{
    public void Configure(EntityTypeBuilder<CustomerSession> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => new { session.CustomerId, session.ExpiresAt });
        builder.HasOne(session => session.Customer).WithMany().HasForeignKey(session => session.CustomerId);
    }
}
