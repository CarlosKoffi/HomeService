using HomeService.Application.Abstractions;
using HomeService.Contracts.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminAccessControlService(IAppDbContext db, AdminQueryService queryService)
{
    public async Task<AdminAccessControlResult> CreateRoleAsync(CreateAdminRoleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var description = request.Description.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return AdminAccessControlResult.ValidationFailed("Le nom du role est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return AdminAccessControlResult.ValidationFailed("La description du role est obligatoire.");
        }

        var exists = await db.AdminRoles.AnyAsync(role => role.Name.ToLower() == name.ToLower(), cancellationToken);
        if (exists)
        {
            return AdminAccessControlResult.ValidationFailed("Un role avec ce nom existe deja.");
        }

        db.AdminRoles.Add(new AdminRole(name, description));
        return await SnapshotAsync(cancellationToken);
    }

    public async Task<AdminAccessControlResult> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateAdminRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var roleExists = await db.AdminRoles.AnyAsync(role => role.Id == roleId, cancellationToken);
        if (!roleExists)
        {
            return AdminAccessControlResult.NotFound("Le role n'existe plus.");
        }

        var moduleIds = request.Permissions.Select(permission => permission.ModuleId).Distinct().ToList();
        var validModuleIds = await db.AdminModules
            .Where(module => moduleIds.Contains(module.Id))
            .Select(module => module.Id)
            .ToListAsync(cancellationToken);

        var parsedPermissions = new List<AdminRolePermission>();
        foreach (var permission in request.Permissions)
        {
            if (!validModuleIds.Contains(permission.ModuleId))
            {
                return AdminAccessControlResult.ValidationFailed("Un module selectionne n'existe plus.");
            }

            if (!Enum.TryParse<AdminPermissionAction>(permission.Action, true, out var action))
            {
                return AdminAccessControlResult.ValidationFailed($"Action admin inconnue: {permission.Action}.");
            }

            parsedPermissions.Add(new AdminRolePermission(roleId, permission.ModuleId, action));
        }

        var existingPermissions = await db.AdminRolePermissions
            .Where(permission => permission.RoleId == roleId)
            .ToListAsync(cancellationToken);

        db.AdminRolePermissions.RemoveRange(existingPermissions);
        db.AdminRolePermissions.AddRange(parsedPermissions
            .GroupBy(permission => new { permission.ModuleId, permission.Action })
            .Select(group => group.First()));

        return await SnapshotAsync(cancellationToken);
    }

    public async Task<AdminAccessControlResult> CreateAdminUserAsync(CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return AdminAccessControlResult.ValidationFailed("Le nom de l'admin est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return AdminAccessControlResult.ValidationFailed("Email admin invalide.");
        }

        var exists = await db.AdminUsers.AnyAsync(admin => admin.Email == email, cancellationToken);
        if (exists)
        {
            return AdminAccessControlResult.ValidationFailed("Un admin existe deja avec cet email.");
        }

        var validRoleIds = await GetValidRoleIdsAsync(request.RoleIds, cancellationToken);
        if (!request.IsSuperAdmin && validRoleIds.Count == 0)
        {
            return AdminAccessControlResult.ValidationFailed("Selectionnez au moins un role pour un admin non super admin.");
        }

        var admin = new AdminUser(fullName, email, request.IsSuperAdmin);
        db.AdminUsers.Add(admin);

        foreach (var roleId in validRoleIds)
        {
            db.AdminUserRoles.Add(new AdminUserRole(admin.Id, roleId));
        }

        return await SnapshotAsync(cancellationToken);
    }

    public async Task<AdminAccessControlResult> UpdateAdminUserRolesAsync(
        Guid adminUserId,
        UpdateAdminUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(user => user.Id == adminUserId, cancellationToken);
        if (admin is null)
        {
            return AdminAccessControlResult.NotFound("L'admin n'existe plus.");
        }

        var validRoleIds = await GetValidRoleIdsAsync(request.RoleIds, cancellationToken);
        if (!admin.IsSuperAdmin && validRoleIds.Count == 0)
        {
            return AdminAccessControlResult.ValidationFailed("Selectionnez au moins un role pour cet admin.");
        }

        var existingRoles = await db.AdminUserRoles
            .Where(role => role.AdminUserId == adminUserId)
            .ToListAsync(cancellationToken);

        db.AdminUserRoles.RemoveRange(existingRoles);
        db.AdminUserRoles.AddRange(validRoleIds.Distinct().Select(roleId => new AdminUserRole(adminUserId, roleId)));

        return await SnapshotAsync(cancellationToken);
    }

    public async Task<AdminAccessControlResult> DeactivateAdminUserAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(user => user.Id == adminUserId, cancellationToken);
        if (admin is null)
        {
            return AdminAccessControlResult.NotFound("L'admin n'existe plus.");
        }

        admin.Deactivate();
        return await SnapshotAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetValidRoleIdsAsync(IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        return await db.AdminRoles
            .Where(role => roleIds.Contains(role.Id) && role.IsActive)
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<AdminAccessControlResult> SnapshotAsync(CancellationToken cancellationToken)
    {
        return AdminAccessControlResult.Ok(await queryService.GetAccessSnapshotAsync(cancellationToken));
    }
}

public sealed record AdminAccessControlResult(
    AdminAccessControlStatus Status,
    AdminAccessSnapshotResponse? Snapshot,
    string? Message)
{
    public static AdminAccessControlResult Ok(AdminAccessSnapshotResponse snapshot)
        => new(AdminAccessControlStatus.Ok, snapshot, null);

    public static AdminAccessControlResult NotFound(string message)
        => new(AdminAccessControlStatus.NotFound, null, message);

    public static AdminAccessControlResult ValidationFailed(string message)
        => new(AdminAccessControlStatus.ValidationFailed, null, message);
}

public enum AdminAccessControlStatus
{
    Ok,
    NotFound,
    ValidationFailed
}
