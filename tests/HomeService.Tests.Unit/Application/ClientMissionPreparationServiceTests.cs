using HomeService.Application.Clients;
using HomeService.Application.Missions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMissionPreparationServiceTests
{
    [Fact]
    public async Task PrepareAsync_WithPrestation_ReturnsMobileReadyPriceAndMedia()
    {
        await using var db = CreateDbContext();
        var service = new Service("Blanchisserie", "Linge", createdByCompanyId: null);
        service.UpdateMedia("/assets/services/blanchisserie.svg", null);
        var prestation = service.AddPrestation("Repassage", "Chemises et pantalons", 1, 3_000, 5_000, "XOF");
        prestation.UpdateIllustration("/catalog/prestations/repassage.jpg");
        db.Services.Add(service);
        db.MissionWorkflowSettings.Add(new MissionWorkflowSetting(
            MissionWorkflowSettingsResolver.UrgentCompanyOfferResponseMinutes,
            "Delai urgence",
            "Temps de reponse entreprise en urgence.",
            "minutes",
            4,
            1,
            60,
            1));
        db.MissionWorkflowSettings.Add(new MissionWorkflowSetting(
            MissionWorkflowSettingsResolver.CompanyProviderAssignmentMinutes,
            "Delai affectation",
            "Temps pour affecter un prestataire.",
            "minutes",
            8,
            1,
            120,
            2));
        db.MissionWorkflowSettings.Add(new MissionWorkflowSetting(
            MissionWorkflowSettingsResolver.UrgentMissionsEnabled,
            "Demandes urgentes",
            "Autorise les demandes urgentes.",
            "boolean",
            1,
            0,
            1,
            3));
        await db.SaveChangesAsync();
        var sut = new ClientMissionPreparationService(db);

        var result = await sut.PrepareAsync(
            new PrepareClientMissionRequest(service.Id, prestation.Id, "Instant", IsUrgent: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("Blanchisserie - Repassage", result.Response.DisplayName);
        Assert.Equal("/assets/services/blanchisserie.svg", result.Response.IconUrl);
        Assert.Equal("/catalog/prestations/repassage.jpg", result.Response.ImageUrl);
        Assert.Equal(3_000, result.Response.StartingPriceAmount);
        Assert.Equal(5_000, result.Response.MaximumPriceAmount);
        Assert.Equal(4, result.Response.CompanyResponseMinutes);
        Assert.Equal(8, result.Response.CompanyAssignmentMinutes);
        Assert.True(result.Response.IsUrgent);
        Assert.True(result.Response.UrgentOptionEnabled);
        Assert.Equal("MobileMoney", result.Response.RecommendedPaymentMethod);
        Assert.All(result.Response.PaymentOptions, option => Assert.True(option.IsAvailable));
    }

    [Fact]
    public async Task PrepareAsync_WhenUrgencyIsDisabled_KeepsInstantMissionNormal()
    {
        await using var db = CreateDbContext();
        var service = new Service("Plomberie", null, createdByCompanyId: null);
        db.Services.Add(service);
        db.MissionWorkflowSettings.Add(new MissionWorkflowSetting(
            MissionWorkflowSettingsResolver.CompanyOfferResponseMinutes,
            "Delai normal",
            "Temps de reponse normal.",
            "minutes",
            12,
            1,
            120,
            1));
        db.MissionWorkflowSettings.Add(new MissionWorkflowSetting(
            MissionWorkflowSettingsResolver.UrgentMissionsEnabled,
            "Demandes urgentes",
            "Autorise les demandes urgentes.",
            "boolean",
            0,
            0,
            1,
            2));
        await db.SaveChangesAsync();

        var result = await new ClientMissionPreparationService(db).PrepareAsync(
            new PrepareClientMissionRequest(service.Id, null, "Instant", IsUrgent: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.False(result.Response.IsUrgent);
        Assert.False(result.Response.UrgentOptionEnabled);
        Assert.Equal(12, result.Response.CompanyResponseMinutes);
    }

    [Fact]
    public async Task PrepareAsync_WhenPrestationBelongsToAnotherService_IsRejected()
    {
        await using var db = CreateDbContext();
        var service = new Service("Blanchisserie", null, createdByCompanyId: null);
        var otherService = new Service("Jardinage", null, createdByCompanyId: null);
        var prestation = otherService.AddPrestation("Tondre gazon", null, 1, 2_000, 4_500, "XOF");
        db.Services.AddRange(service, otherService);
        await db.SaveChangesAsync();
        var sut = new ClientMissionPreparationService(db);

        var result = await sut.PrepareAsync(
            new PrepareClientMissionRequest(service.Id, prestation.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotFound);
        Assert.Contains("prestation", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
