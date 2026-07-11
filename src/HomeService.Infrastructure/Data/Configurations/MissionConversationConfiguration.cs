using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeService.Infrastructure.Data.Configurations;

public sealed class MissionConversationConfiguration : IEntityTypeConfiguration<MissionConversation>
{
    public void Configure(EntityTypeBuilder<MissionConversation> builder)
    {
        builder.HasKey(conversation => conversation.Id);
        builder.HasOne(conversation => conversation.Mission)
            .WithMany()
            .HasForeignKey(conversation => conversation.MissionId);
        builder.HasOne(conversation => conversation.Provider)
            .WithMany()
            .HasForeignKey(conversation => conversation.ProviderId);
        builder.HasOne(conversation => conversation.Company)
            .WithMany()
            .HasForeignKey(conversation => conversation.CompanyId);
        builder.HasOne(conversation => conversation.Customer)
            .WithMany()
            .HasForeignKey(conversation => conversation.CustomerId);
        builder.HasMany(conversation => conversation.Messages)
            .WithOne(message => message.Conversation)
            .HasForeignKey(message => message.ConversationId);
        builder.HasIndex(conversation => conversation.MissionId).IsUnique();
    }
}
