using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Application.Notifications;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyMissionChatService(
    IAppDbContext db,
    MobilePushNotificationQueueService mobilePushNotifications)
{
    public async Task<CompanyMissionChatResult> ListAsync(
        Guid companyId,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(companyId, missionId, cancellationToken);
        if (mission is null)
        {
            return CompanyMissionChatResult.NotFound("Mission introuvable pour cette entreprise.");
        }

        var conversation = await GetOrCreateConversationAsync(mission, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return CompanyMissionChatResult.Ok(await BuildResponseAsync(mission, conversation.Id, cancellationToken));
    }

    public async Task<CompanyMissionChatResult> SendAsync(
        Guid companyId,
        Guid missionId,
        SendCompanyMissionMessageRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(companyId, missionId, cancellationToken);
        if (mission is null)
        {
            return CompanyMissionChatResult.NotFound("Mission introuvable pour cette entreprise.");
        }

        if (mission.Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Resolved)
        {
            return CompanyMissionChatResult.Invalid("Le chat n'est plus disponible pour cette mission.");
        }

        var body = Clean(request.Body);
        if (body is null)
        {
            return CompanyMissionChatResult.Invalid("Le message ne peut pas etre vide.");
        }

        if (body.Length > 2000)
        {
            return CompanyMissionChatResult.Invalid("Le message ne peut pas depasser 2000 caracteres.");
        }

        var conversation = await GetOrCreateConversationAsync(mission, cancellationToken);
        var message = new MissionMessage(
            conversation.Id,
            MissionMessageSenderType.Company,
            companyId,
            body,
            Clean(request.AttachmentPath),
            Clean(request.AttachmentContentType));
        db.MissionMessages.Add(message);

        await QueuePushAsync(
            MobileDeviceOwnerType.Customer,
            mission.CustomerId,
            mission,
            conversation.Id,
            body,
            cancellationToken);
        if (mission.ProviderId.HasValue)
        {
            await QueuePushAsync(
                MobileDeviceOwnerType.Provider,
                mission.ProviderId.Value,
                mission,
                conversation.Id,
                body,
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return CompanyMissionChatResult.Created(new SendCompanyMissionMessageResponse(
            mission.Id,
            conversation.Id,
            message.Id,
            message.CreatedAt,
            "Message envoye."));
    }

    private async Task<Mission?> GetMissionAsync(Guid companyId, Guid missionId, CancellationToken cancellationToken)
        => await db.Missions
            .FirstOrDefaultAsync(mission => mission.Id == missionId && mission.CompanyId == companyId, cancellationToken);

    private async Task<MissionConversation> GetOrCreateConversationAsync(Mission mission, CancellationToken cancellationToken)
    {
        var conversation = await db.MissionConversations
            .FirstOrDefaultAsync(item => item.MissionId == mission.Id, cancellationToken);
        if (conversation is not null)
        {
            conversation.SynchronizeParticipants(mission.ProviderId, mission.CompanyId, mission.CustomerId);
            return conversation;
        }

        conversation = new MissionConversation(mission.Id, mission.ProviderId, mission.CompanyId, mission.CustomerId);
        db.MissionConversations.Add(conversation);
        return conversation;
    }

    private async Task<CompanyMissionChatResponse> BuildResponseAsync(
        Mission mission,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var context = await (
                from service in db.Services.AsNoTracking()
                where service.Id == mission.ServiceId
                join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
                from provider in providerJoin.DefaultIfEmpty()
                select new
                {
                    ServiceName = service.Name,
                    CustomerName = customer.FirstName + " " + customer.LastName,
                    ProviderName = provider == null ? null : provider.FirstName + " " + provider.LastName
                })
            .FirstAsync(cancellationToken);

        var messages = await db.MissionMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new CompanyMissionMessageResponse(
                message.Id,
                message.SenderType.ToString(),
                message.Body,
                message.AttachmentPath,
                message.AttachmentContentType,
                message.CreatedAt,
                message.ReadAt))
            .ToListAsync(cancellationToken);

        return new CompanyMissionChatResponse(
            mission.Id,
            mission.MissionNumber,
            context.ServiceName,
            context.CustomerName,
            context.ProviderName,
            conversationId,
            messages);
    }

    private async Task QueuePushAsync(
        MobileDeviceOwnerType ownerType,
        Guid ownerId,
        Mission mission,
        Guid conversationId,
        string body,
        CancellationToken cancellationToken)
    {
        await mobilePushNotifications.QueueForOwnerAsync(
            ownerType,
            ownerId,
            $"Message entreprise - {mission.MissionNumber}",
            body,
            nameof(MissionConversation),
            conversationId,
            JsonSerializer.Serialize(new
            {
                type = "mission_chat_message",
                missionId = mission.Id,
                mission.MissionNumber,
                conversationId,
                senderType = MissionMessageSenderType.Company.ToString()
            }),
            cancellationToken,
            saveChanges: false);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CompanyMissionChatResult(
    CompanyMissionChatResultStatus Status,
    CompanyMissionChatResponse? ChatResponse,
    SendCompanyMissionMessageResponse? SendResponse,
    string Message)
{
    public bool IsSuccess => Status is CompanyMissionChatResultStatus.Success or CompanyMissionChatResultStatus.Created;

    public static CompanyMissionChatResult Ok(CompanyMissionChatResponse response)
        => new(CompanyMissionChatResultStatus.Success, response, null, string.Empty);

    public static CompanyMissionChatResult Created(SendCompanyMissionMessageResponse response)
        => new(CompanyMissionChatResultStatus.Created, null, response, string.Empty);

    public static CompanyMissionChatResult Invalid(string message)
        => new(CompanyMissionChatResultStatus.Invalid, null, null, message);

    public static CompanyMissionChatResult NotFound(string message)
        => new(CompanyMissionChatResultStatus.NotFound, null, null, message);
}

public enum CompanyMissionChatResultStatus
{
    Success = 0,
    Created = 1,
    Invalid = 2,
    NotFound = 3
}
