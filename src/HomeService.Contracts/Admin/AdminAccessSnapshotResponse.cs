namespace HomeService.Contracts.Admin;

public sealed record AdminAccessSnapshotResponse(
    IReadOnlyList<AdminRoleSummaryResponse> Roles,
    IReadOnlyList<AdminModuleSummaryResponse> Modules,
    IReadOnlyList<AdminUserSummaryResponse> Admins);

public sealed record AdminInvitationResponse(
    AdminAccessSnapshotResponse Snapshot,
    string Token,
    string Email,
    DateTimeOffset ExpiresAt,
    string Message);

public sealed record AdminInvitationDetailResponse(
    string Email,
    string FullName,
    DateTimeOffset ExpiresAt,
    bool IsExpired,
    bool IsAccepted);

public sealed record AdminLoginRequest(
    string Email,
    string Password,
    string? MfaCode = null);

public sealed record AdminLoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    AdminCurrentUserResponse User);

public sealed record AdminCurrentUserResponse(
    Guid Id,
    string FullName,
    string Email,
    bool IsSuperAdmin,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<AdminPermissionSummaryResponse> Permissions,
    bool MfaEnabled = false,
    bool MfaEnrollmentRequired = false);

public sealed record AdminRoleSummaryResponse(
    Guid Id,
    string Name,
    string Description,
    bool IsSystemRole,
    bool IsActive,
    IReadOnlyList<AdminPermissionSummaryResponse> Permissions);

public sealed record AdminModuleSummaryResponse(
    Guid Id,
    string Key,
    string Name,
    string Description,
    int DisplayOrder,
    bool IsActive);

public sealed record AdminPermissionSummaryResponse(
    Guid ModuleId,
    string ModuleKey,
    string ModuleName,
    string Action);

public sealed record AdminUserSummaryResponse(
    Guid Id,
    string FullName,
    string Email,
    bool IsSuperAdmin,
    bool IsActive,
    bool HasActivatedAccess,
    DateTimeOffset? InvitationExpiresAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles,
    bool MfaEnabled = false,
    bool MfaEnrollmentRequired = false)
{
    public AdminUserSummaryResponse(
        Guid id,
        string fullName,
        string email,
        bool isSuperAdmin,
        bool isActive,
        bool hasActivatedAccess,
        DateTimeOffset? invitationExpiresAt,
        DateTimeOffset? lastLoginAt,
        IReadOnlyList<string> roles)
        : this(
            id,
            fullName,
            email,
            isSuperAdmin,
            isActive,
            hasActivatedAccess,
            invitationExpiresAt,
            lastLoginAt,
            roles,
            false,
            false)
    {
    }
}

public sealed record CreateAdminRoleRequest(
    string Name,
    string Description);

public sealed record UpdateAdminRolePermissionsRequest(
    IReadOnlyList<AdminPermissionAssignmentRequest> Permissions);

public sealed record AdminPermissionAssignmentRequest(
    Guid ModuleId,
    string Action);

public sealed record CreateAdminUserRequest(
    string FullName,
    string Email,
    bool IsSuperAdmin,
    IReadOnlyList<Guid> RoleIds);

public sealed record UpdateAdminUserRolesRequest(
    IReadOnlyList<Guid> RoleIds);

public sealed record UpdateAdminUserProfileRequest(
    string FullName,
    string Email);

public sealed record AcceptAdminInvitationRequest(
    string Email,
    string Password,
    string ConfirmPassword);
