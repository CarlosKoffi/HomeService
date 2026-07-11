using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionMessageConfiguration : IEntityTypeConfiguration<MissionMessage>
{
    public void Configure(EntityTypeBuilder<MissionMessage> builder)
    {
        builder.HasKey(message => message.Id);
        builder.Property(message => message.SenderType).HasConversion<string>().HasMaxLength(32);
        builder.Property(message => message.Body).HasMaxLength(2000).IsRequired();
        builder.Property(message => message.AttachmentPath).HasMaxLength(640);
        builder.Property(message => message.AttachmentContentType).HasMaxLength(120);
        builder.HasIndex(message => new { message.ConversationId, message.CreatedAt });
    }
}
