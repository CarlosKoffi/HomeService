using HomeService.Application.Admin;
using HomeService.Application.Security;
using HomeService.Contracts.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminAuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WhenSuperAdminIsValid_CreatesSession()
    {
        await using var db = CreateDbContext();
        var admin = new AdminUser("Super Admin", "super@wele.ci", true);
        admin.AcceptInvitation(Sha256PasswordHasher.Hash("Password123"), DateTimeOffset.UtcNow);
        db.AdminUsers.Add(admin);
        db.AdminModules.Add(new AdminModule(AdminModuleKey.CompanyManagement, "Entreprises", "Gestion entreprises", 1));
        await db.SaveChangesAsync();
        var sut = new AdminAuthService(db);

        var result = await sut.LoginAsync(new AdminLoginRequest("SUPER@WELE.CI", "Password123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.False(string.IsNullOrWhiteSpace(result.Response.Token));
        Assert.True(result.Response.User.IsSuperAdmin);
        Assert.True(await db.AdminSessions.AnyAsync(session => session.AdminUserId == admin.Id));
    }

    [Fact]
    public async Task CanAccessAsync_WhenSuperAdmin_CanAccessEveryModule()
    {
        await using var db = CreateDbContext();
        var admin = new AdminUser("Super Admin", "super@wele.ci", true);
        admin.AcceptInvitation(Sha256PasswordHasher.Hash("Password123"), DateTimeOffset.UtcNow);
        db.AdminUsers.Add(admin);
        db.AdminModules.Add(new AdminModule(AdminModuleKey.AdminAccess, "Acces", "Roles", 1));
        await db.SaveChangesAsync();
        var sut = new AdminAuthService(db);
        var login = await sut.LoginAsync(new AdminLoginRequest("super@wele.ci", "Password123"), CancellationToken.None);

        var canManageRoles = await sut.CanAccessAsync(
            login.Response!.Token,
            AdminModuleKey.AdminAccess,
            AdminPermissionAction.ManageRoles,
            CancellationToken.None);

        Assert.True(canManageRoles);
    }

    [Fact]
    public async Task LoginAsync_WhenSuperAdmin_ReturnsEveryPermissionForEveryActiveModule()
    {
        await using var db = CreateDbContext();
        var admin = new AdminUser("Super Admin", "super@wele.ci", true);
        admin.AcceptInvitation(Sha256PasswordHasher.Hash("Password123"), DateTimeOffset.UtcNow);
        db.AdminUsers.Add(admin);
        foreach (var moduleKey in Enum.GetValues<AdminModuleKey>())
        {
            db.AdminModules.Add(new AdminModule(moduleKey, moduleKey.ToString(), moduleKey.ToString(), (int)moduleKey));
        }

        await db.SaveChangesAsync();
        var sut = new AdminAuthService(db);

        var result = await sut.LoginAsync(new AdminLoginRequest("super@wele.ci", "Password123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var permissions = result.Response!.User.Permissions;
        foreach (var moduleKey in Enum.GetNames<AdminModuleKey>())
        {
            foreach (var action in Enum.GetNames<AdminPermissionAction>())
            {
                Assert.Contains(
                    permissions,
                    permission => permission.ModuleKey == moduleKey && permission.Action == action);
            }
        }
    }

    [Fact]
    public async Task CanAccessAsync_WhenRoleDoesNotContainPermission_ReturnsFalse()
    {
        await using var db = CreateDbContext();
        var module = new AdminModule(AdminModuleKey.Missions, "Missions", "Missions", 1);
        var role = new AdminRole("Lecture missions", "Lecture missions");
        var admin = new AdminUser("Ops", "ops@wele.ci");
        admin.AcceptInvitation(Sha256PasswordHasher.Hash("Password123"), DateTimeOffset.UtcNow);
        db.AdminModules.Add(module);
        db.AdminRoles.Add(role);
        db.AdminUsers.Add(admin);
        db.AdminUserRoles.Add(new AdminUserRole(admin.Id, role.Id));
        db.AdminRolePermissions.Add(new AdminRolePermission(role.Id, module.Id, AdminPermissionAction.View));
        await db.SaveChangesAsync();
        var sut = new AdminAuthService(db);
        var login = await sut.LoginAsync(new AdminLoginRequest("ops@wele.ci", "Password123"), CancellationToken.None);

        var canEdit = await sut.CanAccessAsync(
            login.Response!.Token,
            AdminModuleKey.Missions,
            AdminPermissionAction.Edit,
            CancellationToken.None);

        Assert.False(canEdit);
    }

    [Fact]
    public async Task CanAccessAsync_WhenRoleContainsPermission_ReturnsTrue()
    {
        await using var db = CreateDbContext();
        var module = new AdminModule(AdminModuleKey.Missions, "Missions", "Missions", 1);
        var role = new AdminRole("Lecture missions", "Lecture missions");
        var admin = new AdminUser("Ops", "ops@wele.ci");
        admin.AcceptInvitation(Sha256PasswordHasher.Hash("Password123"), DateTimeOffset.UtcNow);
        db.AdminModules.Add(module);
        db.AdminRoles.Add(role);
        db.AdminUsers.Add(admin);
        db.AdminUserRoles.Add(new AdminUserRole(admin.Id, role.Id));
        db.AdminRolePermissions.Add(new AdminRolePermission(role.Id, module.Id, AdminPermissionAction.View));
        await db.SaveChangesAsync();
        var sut = new AdminAuthService(db);
        var login = await sut.LoginAsync(new AdminLoginRequest("ops@wele.ci", "Password123"), CancellationToken.None);

        var canView = await sut.CanAccessAsync(
            login.Response!.Token,
            AdminModuleKey.Missions,
            AdminPermissionAction.View,
            CancellationToken.None);

        Assert.True(canView);
    }

    [Fact]
    public void PromoteToSuperAdmin_ActivatesAndUnlocksAllPermissionsFlag()
    {
        var admin = new AdminUser("Ops", "ops@wele.ci");
        admin.Deactivate();

        admin.PromoteToSuperAdmin();

        Assert.True(admin.IsSuperAdmin);
        Assert.True(admin.IsActive);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
