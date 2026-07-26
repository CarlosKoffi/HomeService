using HomeService.Application.Clients;
using HomeService.Application.Missions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMissionRequestServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_CreatesMissionAndDispatchOffers()
    {
        await using var db = CreateDbContext();
        var service = new Service("Jardinage", "Entretien exterieur", createdByCompanyId: null);
        db.Services.Add(service);

        for (var index = 0; index < 4; index++)
        {
            var company = new Company($"Entreprise {index}", $"+22507000000{index}", $"ops{index}@wele.ci");
            company.Approve();
            company.UpdateMissionDispatchSettings(index, acceptsUrgentMissions: index < 2);

            var provider = new ProviderProfile(
                company.Id,
                $"Awa{index}",
                "Kone",
                $"+2250102030{index}",
                $"awa{index}@wele.ci",
                new DateOnly(1995, 1, 10),
                "Cocody",
                ProviderGender.Female,
                ProviderEmploymentType.CompanyEmployee,
                4,
                null,
                null,
                5);
            provider.Approve();
            provider.SyncCompanyServices([(service.Id, ExperienceLevel.Confirmed, 4, ProviderServicePriceTier.Normal)]);

            db.Companies.Add(company);
            db.Providers.Add(provider);
        }

        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.CreateAsync(ValidRequest(service.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(3, result.Response.CandidateCompanyCount);
        Assert.Equal(MissionStatus.Offered.ToString(), result.Response.Status);
        Assert.Equal(1, await db.Missions.CountAsync());
        Assert.Equal(3, await db.MissionDispatchOffers.CountAsync());
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Empty(db.MissionAttachments);
    }

    [Fact]
    public async Task CreateAsync_WhenCustomerPhotosAreProvided_AttachesThemToMission()
    {
        await using var db = CreateDbContext();
        var service = new Service("Plomberie", "Depannage eau", createdByCompanyId: null);
        db.Services.Add(service);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var request = ValidRequest(service.Id) with
        {
            Photos =
            [
                new ClientMissionPhotoRequest(
                    "evier.jpg",
                    "missions/pending/evier.jpg",
                    "image/jpeg",
                    245_000,
                    "Photo de la fuite")
            ]
        };

        var result = await sut.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var attachment = await db.MissionAttachments.SingleAsync();
        Assert.Equal(MissionAttachmentType.CustomerPhoto, attachment.AttachmentType);
        Assert.Equal(result.Response!.MissionId, attachment.MissionId);
        Assert.Equal("evier.jpg", attachment.OriginalFileName);
        Assert.Equal("missions/pending/evier.jpg", attachment.StoragePath);
        Assert.Equal("Photo de la fuite", attachment.Caption);
    }

    [Fact]
    public async Task CreateAsync_WhenCashPaymentIsRequested_IsRejected()
    {
        await using var db = CreateDbContext();
        var service = new Service("Menage", null, createdByCompanyId: null);
        db.Services.Add(service);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var request = ValidRequest(service.Id) with { PaymentMethod = PaymentMethod.Cash.ToString() };

        var result = await sut.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("cash", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(db.Missions);
    }

    [Fact]
    public async Task CreateAsync_WhenPrestationDoesNotBelongToService_IsRejected()
    {
        await using var db = CreateDbContext();
        var service = new Service("Blanchisserie", null, createdByCompanyId: null);
        var otherService = new Service("Jardinage", null, createdByCompanyId: null);
        var prestation = otherService.AddPrestation("Tondre le gazon", null, 1, 2_000, 5_000);
        db.Services.AddRange(service, otherService);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var request = ValidRequest(service.Id) with { ServicePrestationId = prestation.Id };

        var result = await sut.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("prestation", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(db.Missions);
    }

    [Fact]
    public async Task CreateAsync_WhenAttachmentIsNotAnImage_IsRejected()
    {
        await using var db = CreateDbContext();
        var service = new Service("Electricite", null, createdByCompanyId: null);
        db.Services.Add(service);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var request = ValidRequest(service.Id) with
        {
            Photos =
            [
                new ClientMissionPhotoRequest(
                    "detail.pdf",
                    "missions/pending/detail.pdf",
                    "application/pdf",
                    120_000,
                    null)
            ]
        };

        var result = await sut.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("images", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(db.MissionAttachments);
    }

    private static ClientMissionRequestService CreateService(HomeServiceDbContext db)
    {
        return new ClientMissionRequestService(
            db,
            new MissionDispatchService(db, new MissionDispatchScoringService()));
    }

    private static CreateClientMissionRequest ValidRequest(Guid serviceId)
    {
        return new CreateClientMissionRequest(
            "Awa",
            "Kone",
            "+2250700000001",
            serviceId,
            null,
            MissionMode.Instant.ToString(),
            PaymentMethod.MobileMoney.ToString(),
            null,
            90,
            "Besoin d'un jardinier aujourd'hui",
            "Cocody Angre",
            5.348850m,
            -4.003150m,
            true,
            true);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
