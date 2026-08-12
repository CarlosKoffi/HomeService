namespace HomeService.Contracts.Admin;

public sealed record AdminMfaStatusResponse(
    bool IsEnabled,
    DateTimeOffset? EnabledAt,
    int RemainingRecoveryCodes,
    bool EnrollmentInProgress,
    DateTimeOffset? EnrollmentExpiresAt);

public sealed record AdminMfaEnrollmentResponse(
    string ManualKey,
    string ProvisioningUri,
    DateTimeOffset ExpiresAt);

public sealed record AdminMfaCodeRequest(string Code);

public sealed record AdminMfaActivationResponse(
    AdminMfaStatusResponse Status,
    IReadOnlyList<string> RecoveryCodes,
    string Message);

public sealed record AdminFinancialActionRequest(
    string MfaCode,
    string? Reason = null,
    string? ProofReference = null);

public sealed record AdminFinancialActionResponse(
    bool Completed,
    bool AwaitingSecondApproval,
    int ApprovalsReceived,
    int ApprovalsRequired,
    string Message);
