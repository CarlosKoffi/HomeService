namespace HomeService.Contracts.Admin;

public sealed record AdminMissionListResponse(
    IReadOnlyList<AdminMissionSummaryResponse> Items,
    AdminMissionStatsResponse Stats);

public sealed record AdminMissionStatsResponse(
    int TotalMissions,
    int OpenMissions,
    int ScheduledMissions,
    int CompletedMissions,
    int DisputedMissions);

public sealed record AdminMissionSummaryResponse(
    Guid Id,
    string MissionNumber,
    string ServiceName,
    string? CompanyName,
    string CustomerName,
    string CustomerPhoneNumber,
    string? ProviderName,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    DateTimeOffset? ScheduledFor,
    int? Amount,
    string Currency,
    string? ServiceAddress,
    DateTimeOffset CreatedAt);

public sealed record AdminMissionActionRequest(string? Note);

public sealed record AdminMissionDetailResponse(
    Guid Id,
    string MissionNumber,
    string ServiceName,
    string? CompanyName,
    Guid? CompanyId,
    string CustomerName,
    string CustomerPhoneNumber,
    string? ProviderName,
    Guid? ProviderId,
    string Status,
    string Mode,
    string PaymentStatus,
    string PaymentMethod,
    DateTimeOffset? ScheduledFor,
    int EstimatedDurationMinutes,
    int? ActualDurationMinutes,
    int? EstimatedTotalAmount,
    int? FinalTotalAmount,
    int? CompanyQuotedAmount,
    string? CompanyQuoteJustification,
    DateTimeOffset? CompanyQuotedAt,
    DateTimeOffset? CustomerQuoteAcceptedAt,
    int PlatformCommissionAmount,
    int TransportFeeAmount,
    int CancellationFeeAmount,
    string Currency,
    string? ServiceAddress,
    decimal? ServiceLatitude,
    decimal? ServiceLongitude,
    int ArrivalToleranceMeters,
    DateTimeOffset? ProviderAcceptedAt,
    DateTimeOffset? CustomerConfirmedAt,
    DateTimeOffset? ContactDetailsReleasedAt,
    bool CanRevealContactDetails,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AdminMissionAssignmentResponse> Assignments,
    IReadOnlyList<AdminMissionConversationMessageResponse> Messages);

public sealed record AdminMissionAssignmentResponse(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    Guid CompanyId,
    string CompanyName,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? RefusalReason,
    string? RefusalComment,
    string? CompletionNote,
    int? ArrivalDistanceMeters,
    int ArrivalToleranceMeters,
    string ArrivalVerificationStatus,
    DateTimeOffset? ArrivalVerifiedAt);

public sealed record AdminMissionConversationMessageResponse(
    Guid Id,
    string SenderType,
    Guid? SenderId,
    string Body,
    string? AttachmentContentType,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);
