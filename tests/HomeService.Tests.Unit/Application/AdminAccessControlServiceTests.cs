using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.Security;
using HomeService.Contracts.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminAccessControlServiceTests
{
    [Fact]
    public async Task CreateRoleAsync_WhenValid_PersistsRoleAndAudit()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        var result = await sut.CreateRoleAsync(
            new CreateAdminRoleRequest("Support", "Gere les demandes clients"),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "role-create"),
            CancellationToken.None);

        Assert.Equal(AdminAccessControlStatus.Ok, result.Status);
        Assert.Contains(result.Snapshot!.Roles, role => role.Name == "Support");
        var role = await db.AdminRoles.SingleAsync(entity => entity.Name == "Support");
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminRoleCreated", audit.Action);
        Assert.Equal(nameof(AdminRole), audit.EntityType);
        Assert.Equal(role.Id, audit.EntityId);
        Assert.Equal("role-create", audit.CorrelationId);
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_WhenValid_ReplacesPermissionsAndAudits()
    {
        await using var db = CreateDbContext();
        var role = new AdminRole("Ops", "Operations");
        var module = new AdminModule(AdminModuleKey.Missions, "Missions", "Suivi missions", 1);
        db.AdminRoles.Add(role);
        db.AdminModules.Add(module);
        db.AdminRolePermissions.Add(new AdminRolePermission(role.Id, module.Id, AdminPermissionAction.View));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.UpdateRolePermissionsAsync(
            role.Id,
            new UpdateAdminRolePermissionsRequest(
            [
                new AdminPermissionAssignmentRequest(module.Id, nameof(AdminPermissionAction.Edit)),
                new AdminPermissionAssignmentRequest(module.Id, nameof(AdminPermissionAction.Approve))
            ]),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "role-permissions"),
            CancellationToken.None);

        Assert.Equal(AdminAccessControlStatus.Ok, result.Status);
        var actions = await db.AdminRolePermissions
            .Where(permission => permission.RoleId == role.Id)
            .Select(permission => permission.Action)
            .OrderBy(action => action)
            .ToListAsync();
        Assert.Equal([AdminPermissionAction.Edit, AdminPermissionAction.Approve], actions);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminRolePermissionsUpdated", audit.Action);
        Assert.Equal(role.Id, audit.EntityId);
        Assert.Equal("role-permissions", audit.CorrelationId);
    }

    [Fact]
    public async Task CreateAdminUserAsync_WhenValid_PersistsRolesAndAudit()
    {
        await using var db = CreateDbContext();
        var role = new AdminRole("Back office", "Back office");
        db.AdminRoles.Add(role);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.CreateAdminUserAsync(
            new CreateAdminUserRequest("Awa Kone", "AWA@WELE.CI", false, [role.Id]),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "admin-create"),
            CancellationToken.None);

        Assert.Equal(AdminAccessControlStatus.Ok, result.Status);
        var admin = await db.AdminUsers.SingleAsync(user => user.Email == "awa@wele.ci");
        Assert.True(await db.AdminUserRoles.AnyAsync(userRole => userRole.AdminUserId == admin.Id && userRole.RoleId == role.Id));
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminUserInvited", audit.Action);
        Assert.Equal(admin.Id, audit.EntityId);
        Assert.Equal("admin-create", audit.CorrelationId);
    }

    [Fact]
    public async Task CreateAdminInvitationAsync_WhenValid_ReturnsTokenAndStoresExpiration()
    {
        await using var db = CreateDbContext();
        var role = new AdminRole("Support", "Support");
        db.AdminRoles.Add(role);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.CreateAdminInvitationAsync(
            new CreateAdminUserRequest(string.Empty, "support@wele.ci", false, [role.Id]),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "admin-invite"),
            CancellationToken.None);

        Assert.Equal(AdminAccessControlStatus.Ok, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Invitation!.Token));
        Assert.Equal("support@wele.ci", result.Invitation.Email);
        var admin = await db.AdminUsers.SingleAsync(user => user.Email == "support@wele.ci");
        Assert.NotNull(admin.InvitationTokenHash);
        Assert.True(admin.InvitationExpiresAt > DateTimeOffset.UtcNow.AddHours(5));
        Assert.Contains("Lien d'invitation admin genere", (await db.AuditLogEntries.OrderBy(entry => entry.CreatedAt).LastAsync()).Summary);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WhenValid_SetsPasswordAndClearsToken()
    {
        await using var db = CreateDbContext();
        var role = new AdminRole("Support", "Support");
        db.AdminRoles.Add(role);
        await db.SaveChangesAsync();
        var sut = CreateService(db);
        var invitation = await sut.CreateAdminInvitationAsync(
            new CreateAdminUserRequest("Support", "support@wele.ci", false, [role.Id]),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "admin-invite"),
            CancellationToken.None);

        var result = await sut.AcceptInvitationAsync(
            invitation.Invitation!.Token,
            new AcceptAdminInvitationRequest("support@wele.ci", "Password123", "Password123"),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "admin-accept"),
            CancellationToken.None);

        Assert.Equal(AdminAccessControlStatus.Ok, result.Status);
        var admin = await db.AdminUsers.SingleAsync(user => user.Email == "support@wele.ci");
        Assert.Null(admin.InvitationTokenHash);
        Assert.Null(admin.InvitationExpiresAt);
        Assert.NotNull(admin.InvitationAcceptedAt);
        Assert.True(Sha256PasswordHasher.Verify("Password123", admin.PasswordHash!));
    }

    [Fact]
    public async Task AcceptInvitationAsync_WhenEmailDoesNotMatch_ReturnsValidationFailed()
    {
        await using var db = CreateDbContext();
        var role = new AdminRole("Support", "Support");
        db.AdminRoles.Add(role);
        await db.SaveChangesAsync();
        var sut = CreateService(db);
        var invitation = await sut.CreateAdminInvitationAsync(
            new CreateAdminUserRequest("Support", "support@wele.ci", false, [role.Id]),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "admin-invite"),
            CancellationToken.None);

        var result = await sut.AcceptInvitationAsync(
            invitation.Invitation!.Token,
            new AcceptAdminInvitationRequest("other@wele.ci", "Password123", "Password123"),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "admin-accept"),
            CancellationToken.None);

        Assert.Equal(AdminAccessControlStatus.ValidationFailed, result.Status);
        var admin = await db.AdminUsers.SingleAsync(user => user.Email == "support@wele.ci");
        Assert.NotNull(admin.InvitationTokenHash);
        Assert.Null(admin.PasswordHash);
    }

    [Fact]
    public async Task DeactivateAdminUserAsync_WhenValid_PersistsAndAudits()
    {
        await using var db = CreateDbContext();
        var admin = new AdminUser("Awa Kone", "awa@wele.ci");
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.DeactivateAdminUserAsync(
            admin.Id,
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "admin-deactivate"),
            CancellationToken.None);

        Assert.Equal(AdminAccessControlStatus.Ok, result.Status);
        Assert.False((await db.AdminUsers.SingleAsync(user => user.Id == admin.Id)).IsActive);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminUserDeactivated", audit.Action);
        Assert.Equal(admin.Id, audit.EntityId);
        Assert.Equal("admin-deactivate", audit.CorrelationId);
    }

    [Fact]
    public async Task ReactivateAdminUserAsync_WhenValid_PersistsAndAudits()
    {
        await using var db = CreateDbContext();
        var admin = new AdminUser("Awa Kone", "awa@wele.ci");
        admin.Deactivate();
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.ReactivateAdminUserAsync(
            admin.Id,
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "admin-reactivate"),
            CancellationToken.None);

        Assert.Equal(AdminAccessControlStatus.Ok, result.Status);
        Assert.True((await db.AdminUsers.SingleAsync(user => user.Id == admin.Id)).IsActive);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminUserReactivated", audit.Action);
        Assert.Equal(admin.Id, audit.EntityId);
        Assert.Equal("admin-reactivate", audit.CorrelationId);
    }

    private static AdminAccessControlService CreateService(HomeServiceDbContext db)
    {
        return new AdminAccessControlService(db, new AdminQueryService(db));
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
