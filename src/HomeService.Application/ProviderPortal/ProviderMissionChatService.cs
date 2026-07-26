using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Application.Notifications;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderMissionChatService(
    IAppDbContext db,
    MobilePushNotificationQueueService mobilePushNotifications)
{
    public async Task<ProviderMissionChatResult> ListAsync(
        Guid providerId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await GetAssignmentAsync(providerId, assignmentId, cancellationToken);
        if (assignment?.Mission is null)
        {
            return ProviderMissionChatResult.NotFound("Mission introuvable pour ce prestataire.");
        }

        var conversation = await GetOrCreateConversationAsync(assignment, cancellationToken);
        var messages = await GetMessagesAsync(conversation.Id, cancellationToken);
        return ProviderMissionChatResult.Ok(new ProviderMissionChatResponse(
            assignment.Id,
            assignment.MissionId,
            conversation.Id,
            messages));
    }

    public async Task<ProviderMissionChatResult> SendAsync(
        Guid providerId,
        Guid assignmentId,
        SendProviderMissionMessageRequest request,
        CancellationToken cancellationToken)
    {
        var assignment = await GetAssignmentAsync(providerId, assignmentId, cancellationToken);
        if (assignment?.Mission is null)
        {
            return ProviderMissionChatResult.NotFound("Mission introuvable pour ce prestataire.");
        }

        if (!CanSendMessage(assignment.Status))
        {
            return ProviderMissionChatResult.Invalid("Le chat n'est plus disponible pour cette affectation.");
        }

        var body = CleanBody(request.Body);
        if (body is null)
        {
            return ProviderMissionChatResult.Invalid("Le message ne peut pas etre vide.");
        }

        if (body.Length > 2000)
        {
            return ProviderMissionChatResult.Invalid("Le message ne peut pas depasser 2000 caracteres.");
        }

        var conversation = await GetOrCreateConversationAsync(assignment, cancellationToken);
        var message = new MissionMessage(
            conversation.Id,
            MissionMessageSenderType.Provider,
            providerId,
            body,
            Clean(request.AttachmentPath),
            Clean(request.AttachmentContentType));

        db.MissionMessages.Add(message);
        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Customer,
            assignment.Mission.CustomerId,
            $"Message prestataire - {assignment.Mission.MissionNumber}",
            body,
            nameof(MissionConversation),
            conversation.Id,
            JsonSerializer.Serialize(new
            {
                type = "mission_chat_message",
                missionId = assignment.MissionId,
                missionNumber = assignment.Mission.MissionNumber,
                assignmentId,
                conversationId = conversation.Id,
                senderType = MissionMessageSenderType.Provider.ToString()
            }),
            cancellationToken,
            saveChanges: false);

        await db.SaveChangesAsync(cancellationToken);

        return ProviderMissionChatResult.Created(new SendProviderMissionMessageResponse(
            assignment.Id,
            assignment.MissionId,
            conversation.Id,
            message.Id,
            message.CreatedAt,
            "Message envoye."));
    }

    private async Task<ProviderMissionAssignment?> GetAssignmentAsync(
        Guid providerId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        return await db.ProviderMissionAssignments
            .Include(assignment => assignment.Mission)
            .FirstOrDefaultAsync(assignment => assignment.Id == assignmentId && assignment.ProviderId == providerId, cancellationToken);
    }

    private async Task<MissionConversation> GetOrCreateConversationAsync(
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        var conversation = await db.MissionConversations
            .FirstOrDefaultAsync(item => item.MissionId == assignment.MissionId, cancellationToken);
        if (conversation is not null)
        {
            return conversation;
        }

        conversation = new MissionConversation(
            assignment.MissionId,
            assignment.ProviderId,
            assignment.CompanyId,
            assignment.Mission!.CustomerId);
        db.MissionConversations.Add(conversation);
        return conversation;
    }

    private async Task<IReadOnlyList<ProviderMobileMissionMessageResponse>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        return await db.MissionMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new ProviderMobileMissionMessageResponse(
                message.Id,
                message.SenderType.ToString(),
                message.Body,
                message.AttachmentPath,
                message.AttachmentContentType,
                message.CreatedAt,
                message.ReadAt))
            .ToListAsync(cancellationToken);
    }

    private static bool CanSendMessage(ProviderMissionAssignmentStatus status)
    {
        return status is ProviderMissionAssignmentStatus.Offered
            or ProviderMissionAssignmentStatus.Accepted
            or ProviderMissionAssignmentStatus.Started;
    }

    private static string? CleanBody(string? value)
    {
        return Clean(value);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record ProviderMissionChatResult(
    ProviderMissionChatResultStatus Status,
    ProviderMissionChatResponse? ChatResponse,
    SendProviderMissionMessageResponse? SendResponse,
    string Message)
{
    public bool IsSuccess => Status is ProviderMissionChatResultStatus.Success or ProviderMissionChatResultStatus.Created;

    public static ProviderMissionChatResult Ok(ProviderMissionChatResponse response)
    {
        return new ProviderMissionChatResult(ProviderMissionChatResultStatus.Success, response, null, string.Empty);
    }

    public static ProviderMissionChatResult Created(SendProviderMissionMessageResponse response)
    {
        return new ProviderMissionChatResult(ProviderMissionChatResultStatus.Created, null, response, string.Empty);
    }

    public static ProviderMissionChatResult Invalid(string message)
    {
        return new ProviderMissionChatResult(ProviderMissionChatResultStatus.Invalid, null, null, message);
    }

    public static ProviderMissionChatResult NotFound(string message)
    {
        return new ProviderMissionChatResult(ProviderMissionChatResultStatus.NotFound, null, null, message);
    }
}

public enum ProviderMissionChatResultStatus
{
    Success = 0,
    Created = 1,
    Invalid = 2,
    NotFound = 3
}
