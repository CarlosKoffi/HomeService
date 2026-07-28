using HomeService.Application.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMissionStatusServiceTests
{
    [Fact]
    public async Task GetAsync_WhenPhoneMatches_ReturnsMissionStatusWithOffersAndPhotos()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedMissionAsync(db);
        var sut = new ClientMissionStatusService(db);

        var result = await sut.GetAsync(scenario.Mission.Id, scenario.Customer.PhoneNumber, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(scenario.Mission.MissionNumber, result.Response.MissionNumber);
        Assert.Equal("Plomberie", result.Response.ServiceName);
        Assert.Single(result.Response.CompanyOffers);
        Assert.Single(result.Response.Photos);
        Assert.False(result.Response.ContactDetailsReleased);
        Assert.Null(result.Response.AssignedCompany!.PhoneNumber);
        Assert.Null(result.Response.AssignedProvider!.PhoneNumber);
        Assert.Equal("provider-photo.jpg", result.Response.AssignedProvider.PhotoStoragePath);
        Assert.Equal(4, result.Response.AssignedProvider.AverageRating);
        Assert.Equal(1, result.Response.AssignedProvider.CompletedMissionCount);
        Assert.Null(result.Response.AssignedProvider.EstimatedArrivalMinutes);
        Assert.True(result.Response.Actions.CanAcceptQuote);
        Assert.False(result.Response.Actions.CanCallCompany);
        Assert.False(result.Response.Actions.CanCallProvider);
        Assert.True(result.Response.Actions.CanCancel);
        Assert.Equal(20_000, result.Response.CompanyQuotedAmount);
        Assert.Equal(3_000, result.Response.PartsEstimateAmount);
        Assert.Equal("Joint a remplacer", result.Response.PartsDescription);
        Assert.Equal("Votre technicien est affecte. Les informations utiles sont disponibles.", result.Response.Message);
    }

    [Fact]
    public async Task GetAsync_WhenQuoteSubmitted_ReturnsAcceptQuoteAction()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedMissionAsync(db);
        var sut = new ClientMissionStatusService(db);

        var result = await sut.GetAsync(scenario.Mission.Id, scenario.Customer.PhoneNumber, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Response!.Actions.CanAcceptQuote);
        Assert.Equal(20_000, result.Response.Actions.AmountToPayNow);
        Assert.Equal("AcceptQuote", result.Response.Actions.PrimaryAction);
    }

    [Fact]
    public async Task GetAsync_WhenAdditionalQuoteSubmitted_ReturnsPayableAdditionalQuote()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedMissionAsync(db);
        var additionalQuote = new MissionAdditionalQuote(
            scenario.Mission.Id,
            scenario.Provider.Id,
            scenario.Company.Id,
            "Le siphon doit etre remplace.",
            "missions/additional/siphon.jpg");
        additionalQuote.Submit(7_500, "XOF", "Remplacement du siphon et main d'oeuvre.");
        db.MissionAdditionalQuotes.Add(additionalQuote);
        await db.SaveChangesAsync();
        var sut = new ClientMissionStatusService(db);

        var result = await sut.GetAsync(scenario.Mission.Id, scenario.Customer.PhoneNumber, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var quote = Assert.Single(result.Response!.AdditionalQuotes);
        Assert.Equal(additionalQuote.Id, quote.QuoteId);
        Assert.Equal("Submitted", quote.Status);
        Assert.Equal(7_500, quote.Amount);
        Assert.Equal("XOF", quote.Currency);
        Assert.True(quote.CanPay);
        Assert.Equal("Le siphon doit etre remplace.", quote.Reason);
        Assert.Equal("missions/additional/siphon.jpg", quote.RequestedPhotoStoragePath);
        Assert.Equal("Remplacement du siphon et main d'oeuvre.", quote.CompanyDescription);
    }

    [Fact]
    public async Task GetAsync_WhenPhoneDoesNotMatch_ReturnsForbidden()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedMissionAsync(db);
        var sut = new ClientMissionStatusService(db);

        var result = await sut.GetAsync(scenario.Mission.Id, "+2250101010101", CancellationToken.None);

        Assert.Equal(ClientMissionStatusResultStatus.Forbidden, result.Status);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task GetAsync_WhenCustomerConfirmed_RevealsCompanyAndProviderContacts()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedMissionAsync(db);
        scenario.Mission.ConfirmByCustomer(3_000, 0, 1_500, 0);
        await db.SaveChangesAsync();
        var sut = new ClientMissionStatusService(db);

        var result = await sut.GetAsync(scenario.Mission.Id, " 225 07 00 00 00 00 ", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Response!.ContactDetailsReleased);
        Assert.Equal(scenario.Company.PhoneNumber, result.Response.AssignedCompany!.PhoneNumber);
        Assert.Equal(scenario.Company.Email, result.Response.AssignedCompany.Email);
        Assert.Equal(scenario.Provider.PhoneNumber, result.Response.AssignedProvider!.PhoneNumber);
        Assert.Equal("provider-photo.jpg", result.Response.AssignedProvider.PhotoStoragePath);
        Assert.Equal(4, result.Response.AssignedProvider.AverageRating);
        Assert.Equal(1, result.Response.AssignedProvider.CompletedMissionCount);
        Assert.True(result.Response.Actions.CanCallCompany);
        Assert.True(result.Response.Actions.CanCallProvider);
        Assert.Equal("CallProvider", result.Response.Actions.PrimaryAction);
    }

    [Fact]
    public async Task GetAsync_WhenMissionCompleted_ReturnsCompletionActions()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedMissionAsync(db);
        scenario.Mission.ConfirmByCustomer(3_000, 0, 1_500, 0);
        scenario.Mission.Start(scenario.Provider.Id, scenario.Company.Id);
        scenario.Mission.Complete(75);
        await db.SaveChangesAsync();
        var sut = new ClientMissionStatusService(db);

        var result = await sut.GetAsync(scenario.Mission.Id, scenario.Customer.PhoneNumber, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Response!.Actions.CanValidateCompletion);
        Assert.True(result.Response.Actions.CanRateMission);
        Assert.True(result.Response.Actions.CanOpenDispute);
        Assert.Equal("ValidateCompletion", result.Response.Actions.PrimaryAction);
    }

    private static async Task<ClientMissionStatusScenario> SeedMissionAsync(HomeServiceDbContext db)
    {
        var service = new Service("Plomberie", "Depannage eau", createdByCompanyId: null);
        var prestation = service.AddPrestation("Fuite evier", null, 1, 5_000, 25_000);
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var company = new Company("Wele Services", "+2250701111111", "ops@wele.ci");
        company.Approve();
        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Konate",
            "+2250702222222",
            "awa@wele.ci",
            new DateOnly(1994, 2, 3),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            5,
            5.348850m,
            -4.003150m,
            5);
        provider.Approve();

        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            null,
            90,
            prestation.Id,
            "Fuite sous evier",
            requiresCompanyQuote: true);
        mission.SetServiceLocation("Cocody Angre", 5.348850m, -4.003150m);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 20_000, 25_000, null, 3_000, "Joint a remplacer");
        mission.MarkProviderAccepted(provider.Id, company.Id);

        var offer = new MissionDispatchOffer(
            mission.Id,
            company.Id,
            1,
            92,
            "Priorite forte, peu de missions recentes.",
            DateTimeOffset.UtcNow.AddMinutes(5));
        var photo = new MissionAttachment(
            mission.Id,
            MissionAttachmentType.CustomerPhoto,
            "evier.jpg",
            "client-missions/pending/evier.jpg",
            "image/jpeg",
            220_000,
            "Fuite visible");
        var providerPhoto = new ProviderDocument(
            provider.Id,
            ProviderDocumentType.Photo,
            "awa.jpg",
            "provider-photo.jpg",
            "image/jpeg");
        var previousMission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Scheduled,
            PaymentMethod.MobileMoney,
            DateTimeOffset.UtcNow.AddDays(-2),
            90,
            prestation.Id,
            "Ancienne intervention",
            requiresCompanyQuote: true);
        previousMission.AssignWithCompanyQuote(provider.Id, company.Id, 18_000, 25_000, null);
        previousMission.MarkProviderAccepted(provider.Id, company.Id);
        previousMission.ConfirmByCustomer(2_700, 0, 1_500);
        previousMission.Start(provider.Id, company.Id);
        previousMission.Complete(90);
        previousMission.ValidateCompletionByCustomer();
        var previousReview = new MissionReview(
            previousMission.Id,
            customer.Id,
            company.Id,
            provider.Id,
            qualityRating: 4,
            punctualityRating: 4,
            politenessRating: 4,
            cleanlinessRating: 4,
            comment: "Bonne intervention precedente.");

        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Companies.Add(company);
        db.Providers.Add(provider);
        db.Missions.AddRange(mission, previousMission);
        db.MissionDispatchOffers.Add(offer);
        db.MissionAttachments.Add(photo);
        db.ProviderDocuments.Add(providerPhoto);
        db.MissionReviews.Add(previousReview);
        await db.SaveChangesAsync();

        return new ClientMissionStatusScenario(customer, company, provider, mission);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private sealed record ClientMissionStatusScenario(
        CustomerProfile Customer,
        Company Company,
        ProviderProfile Provider,
        Mission Mission);
}
