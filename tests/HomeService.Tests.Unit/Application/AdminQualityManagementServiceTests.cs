using HomeService.Application.Admin;
using HomeService.Application.Quality;
using HomeService.Contracts.Admin;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminQualityManagementServiceTests
{
    [Fact]
    public async Task Adding_controls_generates_unique_codes_and_allows_deletion()
    {
        await using var db = CreateDbContext();
        var serviceEntity = new Service("Ménage", null, null);
        db.Services.Add(serviceEntity);
        await db.SaveChangesAsync();
        var sut = CreateService(db);
        var template = await sut.CreateTemplateAsync(
            new CreateAdminQualityChecklistTemplateRequest(serviceEntity.Id, null, "Contrôle ménage", null, false),
            CancellationToken.None);

        var first = await sut.AddItemAsync(template.Id, new CreateAdminQualityChecklistItemRequest(
            null, "Vérifier la zone", null, "DuringMission", "Confirmation", true, false, null, 10), CancellationToken.None);
        var second = await sut.AddItemAsync(template.Id, new CreateAdminQualityChecklistItemRequest(
            null, "Vérifier la zone", null, "BeforeCompletion", "Photo", true, false, null, 20), CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Code, second.Code);

        var result = await sut.DeleteItemAsync(first.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Deleted);
        Assert.False(await db.QualityChecklistItems.AnyAsync(item => item.Id == first.Id));
        Assert.True(await db.QualityChecklistItems.AnyAsync(item => item.Id == second.Id));
    }

    [Fact]
    public async Task A_control_can_be_attached_to_and_removed_from_a_service_option()
    {
        await using var db = CreateDbContext();
        var serviceEntity = new Service("Blanchisserie", null, null);
        var prestation = serviceEntity.AddPrestation("Lavage", null, 10);
        var option = prestation.AddOption("Repassage", null, 10, 0, 0, false);
        db.Services.Add(serviceEntity);
        await db.SaveChangesAsync();
        var sut = CreateService(db);
        var template = await sut.CreateTemplateAsync(
            new CreateAdminQualityChecklistTemplateRequest(serviceEntity.Id, prestation.Id, "Contrôle lavage", null, false),
            CancellationToken.None);
        var created = await sut.AddItemAsync(template.Id, new CreateAdminQualityChecklistItemRequest(
            null, "Contrôler le repassage", null, "BeforeCompletion", "YesNo", true, false, option.Id, 10), CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(option.Id, created.ServiceOptionId);

        var updated = await sut.UpdateItemAsync(created.Id, new UpdateAdminQualityChecklistItemRequest(
            "Contrôler la finition", null, "BeforeCompletion", "ShortText", true, false, 10, true, null), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Null(updated.ServiceOptionId);
        Assert.Equal("ShortText", updated.ResponseType);
    }

    [Fact]
    public async Task Creating_a_new_active_version_deactivates_the_previous_one()
    {
        await using var db = CreateDbContext();
        var serviceEntity = new Service("Climatisation", null, null);
        db.Services.Add(serviceEntity);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var first = await sut.CreateTemplateAsync(
            new CreateAdminQualityChecklistTemplateRequest(serviceEntity.Id, null, "Version initiale", null, false), CancellationToken.None);
        var second = await sut.CreateTemplateAsync(
            new CreateAdminQualityChecklistTemplateRequest(serviceEntity.Id, null, "Nouvelle version", null, false), CancellationToken.None);
        var templates = await sut.ListTemplatesAsync(CancellationToken.None);

        Assert.False(templates.Single(item => item.Id == first.Id).IsActive);
        Assert.True(templates.Single(item => item.Id == second.Id).IsActive);
        Assert.Equal(2, second.Version);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"admin-quality-{Guid.NewGuid():N}")
            .Options;
        return new HomeServiceDbContext(options);
    }

    private static AdminQualityManagementService CreateService(HomeServiceDbContext db) =>
        new(db, new QualityScoringService(db));
}
