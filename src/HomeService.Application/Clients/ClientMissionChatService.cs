using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Application.Notifications;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionChatService(
    IAppDbContext db,
    MobilePushNotificationQueueService mobilePushNotifications)
{
    public async Task<ClientMissionChatResult> ListAsync(Guid missionId, string phoneNumber, CancellationToken cancellationToken)
    {
        var mission = await GetCustomerMissionAsync(missionId, phoneNumber, cancellationToken);
        if (mission is null)
        {
            return ClientMissionChatResult.NotFound("Mission introuvable pour ce client.");
        }

        var conversation = await GetOrCreateConversationAsync(mission, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var messages = await ListMessagesAsync(conversation.Id, cancellationToken);
        return ClientMissionChatResult.Ok(new ClientMissionChatResponse(
            mission.Id,
            mission.MissionNumber,
            await GetMissionLabelAsync(mission, cancellationToken),
            conversation.Id,
            messages));
    }

    public async Task<ClientMissionChatResult> SendAsync(Guid missionId, SendClientMissionMessageRequest request, CancellationToken cancellationToken)
    {
        var mission = await GetCustomerMissionAsync(missionId, request.PhoneNumber, cancellationToken);
        if (mission is null)
        {
            return ClientMissionChatResult.NotFound("Mission introuvable pour ce client.");
        }

        if (mission.Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Resolved)
        {
            return ClientMissionChatResult.Invalid("Le chat n'est plus disponible pour cette mission.");
        }

        var body = Clean(request.Body);
        if (body is null)
        {
            return ClientMissionChatResult.Invalid("Le message ne peut pas etre vide.");
        }

        if (body.Length > 2000)
        {
            return ClientMissionChatResult.Invalid("Le message ne peut pas depasser 2000 caracteres.");
        }

        var conversation = await GetOrCreateConversationAsync(mission, cancellationToken);
        var message = new MissionMessage(
            conversation.Id,
            MissionMessageSenderType.Customer,
            mission.CustomerId,
            body,
            Clean(request.AttachmentPath),
            Clean(request.AttachmentContentType));

        db.MissionMessages.Add(message);
        if (mission.ProviderId is not null)
        {
            await mobilePushNotifications.QueueForOwnerAsync(
                MobileDeviceOwnerType.Provider,
                mission.ProviderId.Value,
                $"Message client - {mission.MissionNumber}",
                body,
                nameof(MissionConversation),
                conversation.Id,
                JsonSerializer.Serialize(new
                {
                    type = "mission_chat_message",
                    missionId,
                    mission.MissionNumber,
                    conversationId = conversation.Id,
                    senderType = MissionMessageSenderType.Customer.ToString()
                }),
                cancellationToken,
                saveChanges: false);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ClientMissionChatResult.Created(new SendClientMissionMessageResponse(
            mission.Id,
            conversation.Id,
            message.Id,
            message.CreatedAt,
            "Message envoye."));
    }

    private async Task<Mission?> GetCustomerMissionAsync(Guid missionId, string phoneNumber, CancellationToken cancellationToken)
    {
        var phone = ClientAuthService.NormalizePhone(phoneNumber);
        return await db.Missions
            .Include(mission => mission.ServicePrestation)
            .Where(mission => mission.Id == missionId)
            .Join(
                db.Customers.Where(customer => customer.PhoneNumber == phone),
                mission => mission.CustomerId,
                customer => customer.Id,
                (mission, _) => mission)
            .FirstOrDefaultAsync(cancellationToken);
    }

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

    private async Task<string> GetMissionLabelAsync(Mission mission, CancellationToken cancellationToken)
    {
        var serviceName = await db.Services
            .AsNoTracking()
            .Where(service => service.Id == mission.ServiceId)
            .Select(service => service.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Service";

        return string.IsNullOrWhiteSpace(mission.ServicePrestation?.Name)
            ? serviceName
            : $"{serviceName} - {mission.ServicePrestation.Name}";
    }

    private async Task<IReadOnlyList<ClientMissionMessageResponse>> ListMessagesAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        return await db.MissionMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new ClientMissionMessageResponse(
                message.Id,
                message.SenderType.ToString(),
                message.Body,
                message.AttachmentPath,
                message.AttachmentContentType,
                message.CreatedAt,
                message.ReadAt))
            .ToListAsync(cancellationToken);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record ClientMissionChatResult(
    ClientMissionChatResultStatus Status,
    ClientMissionChatResponse? ChatResponse,
    SendClientMissionMessageResponse? SendResponse,
    string Message)
{
    public bool IsSuccess => Status is ClientMissionChatResultStatus.Success or ClientMissionChatResultStatus.Created;

    public static ClientMissionChatResult Ok(ClientMissionChatResponse response)
        => new(ClientMissionChatResultStatus.Success, response, null, string.Empty);

    public static ClientMissionChatResult Created(SendClientMissionMessageResponse response)
        => new(ClientMissionChatResultStatus.Created, null, response, string.Empty);

    public static ClientMissionChatResult Invalid(string message)
        => new(ClientMissionChatResultStatus.Invalid, null, null, message);

    public static ClientMissionChatResult NotFound(string message)
        => new(ClientMissionChatResultStatus.NotFound, null, null, message);
}

public enum ClientMissionChatResultStatus
{
    Success = 0,
    Created = 1,
    Invalid = 2,
    NotFound = 3
}
