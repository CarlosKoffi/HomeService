using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.Security;
using HomeService.Contracts.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace HomeService.Application.Admin;

public sealed class AdminAccessControlService(IAppDbContext db, AdminQueryService queryService)
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(6);

    public async Task<AdminAccessControlResult> CreateRoleAsync(CreateAdminRoleRequest request, CancellationToken cancellationToken)
        => await CreateRoleAsync(request, null, null, cancellationToken);

    public async Task<AdminAccessControlResult> CreateRoleAsync(
        CreateAdminRoleRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
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

        var role = new AdminRole(name, description);
        db.AdminRoles.Add(role);
        AddAuditLog(
            actor,
            auditContext,
            "AdminRoleCreated",
            nameof(AdminRole),
            role.Id,
            $"Role admin cree: {name}.",
            after: request);

        return await SaveAndSnapshotAsync(cancellationToken);
    }

    public async Task<AdminInvitationResult> CreateAdminInvitationAsync(
        CreateAdminUserRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? BuildDefaultFullName(request.Email)
            : request.FullName.Trim();
        var normalizedRequest = request with { FullName = fullName };
        var createResult = await CreateAdminUserAsync(normalizedRequest, actor, auditContext, cancellationToken);
        if (createResult.Status != AdminAccessControlStatus.Ok || createResult.Snapshot is null)
        {
            return AdminInvitationResult.FromAccessResult(createResult);
        }

        var admin = await db.AdminUsers.SingleAsync(user => user.Email == normalizedRequest.Email.Trim().ToLowerInvariant(), cancellationToken);
        var token = GenerateInvitationToken();
        var expiresAt = DateTimeOffset.UtcNow.Add(InvitationLifetime);
        admin.SetInvitation(HashToken(token), expiresAt);
        AddAuditLog(
            actor,
            auditContext,
            "AdminInvitationGenerated",
            nameof(AdminUser),
            admin.Id,
            $"Lien d'invitation admin genere pour {admin.Email}.",
            after: new { admin.Email, expiresAt });

        await db.SaveChangesAsync(cancellationToken);
        var snapshot = await queryService.GetAccessSnapshotAsync(cancellationToken);
        var message = $"Bonjour,\n\nVous avez ete invite a rejoindre l'espace admin Wele. Ouvrez le lien ci-dessous, renseignez votre email puis creez votre mot de passe.\n\nLien valable jusqu'au {expiresAt:dd/MM/yyyy HH:mm} UTC.";
        return AdminInvitationResult.Invited(snapshot, token, admin.Email, expiresAt, message);
    }

    public async Task<AdminInvitationDetailResponse?> GetInvitationAsync(string token, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(token);
        var admin = await db.AdminUsers.FirstOrDefaultAsync(user => user.InvitationTokenHash == tokenHash, cancellationToken);
        if (admin is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        return new AdminInvitationDetailResponse(
            admin.Email,
            admin.FullName,
            admin.InvitationExpiresAt ?? now,
            admin.InvitationExpiresAt <= now,
            admin.InvitationAcceptedAt is not null);
    }

    public async Task<AdminAccessControlResult> AcceptInvitationAsync(
        string token,
        AcceptAdminInvitationRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var validationMessage = ValidatePassword(request.Password, request.ConfirmPassword);
        if (validationMessage is not null)
        {
            return AdminAccessControlResult.ValidationFailed(validationMessage);
        }

        var tokenHash = HashToken(token);
        var admin = await db.AdminUsers.FirstOrDefaultAsync(user => user.InvitationTokenHash == tokenHash, cancellationToken);
        if (admin is null)
        {
            return AdminAccessControlResult.NotFound("Le lien d'invitation est introuvable ou deja utilise.");
        }

        if (admin.InvitationExpiresAt is null || admin.InvitationExpiresAt <= DateTimeOffset.UtcNow)
        {
            return AdminAccessControlResult.ValidationFailed("Le lien d'invitation a expire. Demandez un nouveau lien.");
        }

        admin.AcceptInvitation(Sha256PasswordHasher.Hash(request.Password), DateTimeOffset.UtcNow);
        AddAuditLog(
            actor,
            auditContext,
            "AdminInvitationAccepted",
            nameof(AdminUser),
            admin.Id,
            $"Invitation admin acceptee par {admin.Email}.",
            after: new { admin.Email });

        return await SaveAndSnapshotAsync(cancellationToken);
    }

    public async Task<AdminAccessControlResult> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateAdminRolePermissionsRequest request,
        CancellationToken cancellationToken)
        => await UpdateRolePermissionsAsync(roleId, request, null, null, cancellationToken);

    public async Task<AdminAccessControlResult> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateAdminRolePermissionsRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
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
        var before = existingPermissions
            .Select(permission => new { permission.ModuleId, permission.Action })
            .ToList();

        db.AdminRolePermissions.RemoveRange(existingPermissions);
        db.AdminRolePermissions.AddRange(parsedPermissions
            .GroupBy(permission => new { permission.ModuleId, permission.Action })
            .Select(group => group.First()));
        AddAuditLog(
            actor,
            auditContext,
            "AdminRolePermissionsUpdated",
            nameof(AdminRole),
            roleId,
            "Permissions du role admin modifiees.",
            before,
            request);

        return await SaveAndSnapshotAsync(cancellationToken);
    }

    public async Task<AdminAccessControlResult> CreateAdminUserAsync(CreateAdminUserRequest request, CancellationToken cancellationToken)
        => await CreateAdminUserAsync(request, null, null, cancellationToken);

    public async Task<AdminAccessControlResult> CreateAdminUserAsync(
        CreateAdminUserRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
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

        AddAuditLog(
            actor,
            auditContext,
            "AdminUserInvited",
            nameof(AdminUser),
            admin.Id,
            $"Admin invite: {email}.",
            after: request);

        return await SaveAndSnapshotAsync(cancellationToken);
    }

    public async Task<AdminAccessControlResult> UpdateAdminUserRolesAsync(
        Guid adminUserId,
        UpdateAdminUserRolesRequest request,
        CancellationToken cancellationToken)
        => await UpdateAdminUserRolesAsync(adminUserId, request, null, null, cancellationToken);

    public async Task<AdminAccessControlResult> UpdateAdminUserRolesAsync(
        Guid adminUserId,
        UpdateAdminUserRolesRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
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
        var before = existingRoles.Select(role => role.RoleId).ToList();

        db.AdminUserRoles.RemoveRange(existingRoles);
        db.AdminUserRoles.AddRange(validRoleIds.Distinct().Select(roleId => new AdminUserRole(adminUserId, roleId)));
        AddAuditLog(
            actor,
            auditContext,
            "AdminUserRolesUpdated",
            nameof(AdminUser),
            adminUserId,
            "Roles de l'admin modifies.",
            before,
            request);

        return await SaveAndSnapshotAsync(cancellationToken);
    }

    public async Task<AdminAccessControlResult> DeactivateAdminUserAsync(Guid adminUserId, CancellationToken cancellationToken)
        => await DeactivateAdminUserAsync(adminUserId, null, null, cancellationToken);

    public async Task<AdminAccessControlResult> DeactivateAdminUserAsync(
        Guid adminUserId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(user => user.Id == adminUserId, cancellationToken);
        if (admin is null)
        {
            return AdminAccessControlResult.NotFound("L'admin n'existe plus.");
        }

        var before = new { admin.Id, admin.Email, admin.IsActive };
        admin.Deactivate();
        AddAuditLog(
            actor,
            auditContext,
            "AdminUserDeactivated",
            nameof(AdminUser),
            adminUserId,
            "Admin desactive.",
            before,
            new { admin.Id, admin.Email, admin.IsActive });

        return await SaveAndSnapshotAsync(cancellationToken);
    }

    public async Task<AdminAccessControlResult> ReactivateAdminUserAsync(Guid adminUserId, CancellationToken cancellationToken)
        => await ReactivateAdminUserAsync(adminUserId, null, null, cancellationToken);

    public async Task<AdminAccessControlResult> ReactivateAdminUserAsync(
        Guid adminUserId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(user => user.Id == adminUserId, cancellationToken);
        if (admin is null)
        {
            return AdminAccessControlResult.NotFound("L'admin n'existe plus.");
        }

        var before = new { admin.Id, admin.Email, admin.IsActive };
        admin.Reactivate();
        AddAuditLog(
            actor,
            auditContext,
            "AdminUserReactivated",
            nameof(AdminUser),
            adminUserId,
            "Admin reactive.",
            before,
            new { admin.Id, admin.Email, admin.IsActive });

        return await SaveAndSnapshotAsync(cancellationToken);
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

    private async Task<AdminAccessControlResult> SaveAndSnapshotAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        return await SnapshotAsync(cancellationToken);
    }

    private static string GenerateInvitationToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
    }

    private static string BuildDefaultFullName(string email)
    {
        var localPart = email.Split('@', 2)[0].Replace('.', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(localPart) ? "Nouvel admin" : localPart;
    }

    private static string? ValidatePassword(string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return "Le mot de passe doit contenir au moins 8 caracteres.";
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return "Les deux mots de passe ne correspondent pas.";
        }

        return null;
    }

    private void AddAuditLog(
        AuditActor? actor,
        AuditRequestContext? auditContext,
        string action,
        string entityType,
        Guid? entityId,
        string summary,
        object? before = null,
        object? after = null)
    {
        if (actor is null)
        {
            return;
        }

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            action,
            entityType,
            entityId,
            summary,
            auditContext,
            before,
            after));
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

public sealed record AdminInvitationResult(
    AdminAccessControlStatus Status,
    AdminInvitationResponse? Invitation,
    string? Message)
{
    public static AdminInvitationResult Invited(
        AdminAccessSnapshotResponse snapshot,
        string token,
        string email,
        DateTimeOffset expiresAt,
        string message)
        => new(AdminAccessControlStatus.Ok, new AdminInvitationResponse(snapshot, token, email, expiresAt, message), null);

    public static AdminInvitationResult FromAccessResult(AdminAccessControlResult result)
        => new(result.Status, null, result.Message);
}

public enum AdminAccessControlStatus
{
    Ok,
    NotFound,
    ValidationFailed
}
