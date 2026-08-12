using System.Security.Cryptography;
using System.Text;
using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Contracts.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminAuthService(IAppDbContext db, AdminMfaService? mfaService = null)
{
    private const int SessionHours = 8;

    public async Task<AdminAuthResult> LoginAsync(AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AdminAuthResult.Unauthorized("Email ou mot de passe incorrect.");
        }

        var admin = await db.AdminUsers
            .Include(user => user.Roles)
                .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

        if (admin is null
            || !admin.IsActive
            || string.IsNullOrWhiteSpace(admin.PasswordHash)
            || !Sha256PasswordHasher.Verify(request.Password, admin.PasswordHash))
        {
            return AdminAuthResult.Unauthorized("Email ou mot de passe incorrect.");
        }

        if (Sha256PasswordHasher.NeedsRehash(admin.PasswordHash))
        {
            admin.SetPasswordHash(Sha256PasswordHasher.Hash(request.Password));
        }

        if (admin.IsMfaEnabled)
        {
            if (mfaService is null)
            {
                return AdminAuthResult.Unauthorized("Le service Authenticator est indisponible.");
            }

            if (string.IsNullOrWhiteSpace(request.MfaCode))
            {
                return AdminAuthResult.Unauthorized("Saisissez le code affiché dans Authenticator.");
            }

            var mfaVerification = await mfaService.VerifyAsync(admin.Id, request.MfaCode, cancellationToken);
            if (!mfaVerification.IsSuccess)
            {
                return AdminAuthResult.Unauthorized(mfaVerification.Message ?? "Code Authenticator incorrect.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var token = GenerateSessionToken();
        var expiresAt = now.AddHours(SessionHours);
        var session = new AdminSession(admin.Id, HashToken(token), expiresAt);
        admin.RecordLogin(now);
        db.AdminSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var currentUser = await BuildCurrentUserAsync(admin.Id, expiresAt, cancellationToken);
        return currentUser is null
            ? AdminAuthResult.Unauthorized("Session admin impossible a creer.")
            : AdminAuthResult.Success(new AdminLoginResponse(token, expiresAt, currentUser));
    }

    public async Task<AdminCurrentUserResponse?> GetCurrentUserAsync(string token, CancellationToken cancellationToken)
    {
        var session = await GetActiveSessionAsync(token, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        session.TouchSeen(now);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildCurrentUserAsync(session.AdminUserId, session.ExpiresAt, cancellationToken);
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(token);
        var session = await db.AdminSessions.FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.Revoke(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> CanAccessAsync(
        string token,
        AdminModuleKey moduleKey,
        AdminPermissionAction action,
        CancellationToken cancellationToken)
    {
        var session = await GetActiveSessionAsync(token, cancellationToken);
        if (session?.AdminUser is null || !session.AdminUser.IsActive)
        {
            return false;
        }

        if (session.AdminUser.IsSuperAdmin)
        {
            return true;
        }

        return await db.AdminUserRoles
            .AsNoTracking()
            .Where(userRole => userRole.AdminUserId == session.AdminUserId)
            .Join(
                db.AdminRolePermissions.AsNoTracking(),
                userRole => userRole.RoleId,
                permission => permission.RoleId,
                (_, permission) => permission)
            .AnyAsync(
                permission => permission.Action == action
                    && permission.Module != null
                    && permission.Module.Key == moduleKey
                    && permission.Module.IsActive,
                cancellationToken);
    }

    private async Task<AdminSession?> GetActiveSessionAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(token);
        return await db.AdminSessions
            .Include(session => session.AdminUser)
            .FirstOrDefaultAsync(
                session => session.TokenHash == tokenHash
                    && session.RevokedAt == null
                    && session.ExpiresAt > now
                    && session.AdminUser != null
                    && session.AdminUser.IsActive,
                cancellationToken);
    }

    private async Task<AdminCurrentUserResponse?> BuildCurrentUserAsync(
        Guid adminUserId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var admin = await db.AdminUsers
            .AsNoTracking()
            .Where(user => user.Id == adminUserId)
            .Select(user => new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.IsSuperAdmin,
                user.MfaEnabledAt,
                user.MfaSecretProtected,
                user.MfaEnrollmentRequired
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (admin is null)
        {
            return null;
        }

        var permissions = admin.IsSuperAdmin
            ? await GetAllPermissionsAsync(cancellationToken)
            : await GetUserPermissionsAsync(admin.Id, cancellationToken);

        return new AdminCurrentUserResponse(
            admin.Id,
            admin.FullName,
            admin.Email,
            admin.IsSuperAdmin,
            expiresAt,
            permissions,
            admin.MfaEnabledAt.HasValue && !string.IsNullOrWhiteSpace(admin.MfaSecretProtected),
            admin.MfaEnrollmentRequired);
    }

    private async Task<IReadOnlyList<AdminPermissionSummaryResponse>> GetAllPermissionsAsync(CancellationToken cancellationToken)
    {
        var modules = await db.AdminModules
            .AsNoTracking()
            .Where(module => module.IsActive)
            .OrderBy(module => module.DisplayOrder)
            .Select(module => new { module.Id, module.Key, module.Name })
            .ToListAsync(cancellationToken);

        return modules
            .SelectMany(module => Enum.GetNames<AdminPermissionAction>()
                .Select(action => new AdminPermissionSummaryResponse(module.Id, module.Key.ToString(), module.Name, action)))
            .ToList();
    }

    private async Task<IReadOnlyList<AdminPermissionSummaryResponse>> GetUserPermissionsAsync(
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        return await db.AdminUserRoles
            .AsNoTracking()
            .Where(userRole => userRole.AdminUserId == adminUserId)
            .Join(
                db.AdminRolePermissions.AsNoTracking(),
                userRole => userRole.RoleId,
                permission => permission.RoleId,
                (_, permission) => permission)
            .Where(permission => permission.Module != null && permission.Module.IsActive)
            .Select(permission => new AdminPermissionSummaryResponse(
                permission.ModuleId,
                permission.Module!.Key.ToString(),
                permission.Module!.Name,
                permission.Action.ToString()))
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static string GenerateSessionToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
    }
}

public sealed class AdminAuthResult
{
    private AdminAuthResult(bool isSuccess, AdminLoginResponse? response, string? message)
    {
        IsSuccess = isSuccess;
        Response = response;
        Message = message;
    }

    public bool IsSuccess { get; }
    public AdminLoginResponse? Response { get; }
    public string? Message { get; }

    public static AdminAuthResult Success(AdminLoginResponse response) => new(true, response, null);

    public static AdminAuthResult Unauthorized(string message) => new(false, null, message);
}
