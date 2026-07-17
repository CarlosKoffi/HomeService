namespace HomeService.Contracts.Admin;

public sealed record AdminAccessSnapshotResponse(
    IReadOnlyList<AdminRoleSummaryResponse> Roles,
    IReadOnlyList<AdminModuleSummaryResponse> Modules,
    IReadOnlyList<AdminUserSummaryResponse> Admins);

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
    string ModuleName,
    string Action);

public sealed record AdminUserSummaryResponse(
    Guid Id,
    string FullName,
    string Email,
    bool IsSuperAdmin,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles);

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
