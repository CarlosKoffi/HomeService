using HomeService.Application.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderMobileMissionDetailServiceTests
{
    [Fact]
    public async Task GetAsync_WhenAssignmentIsOffered_ReturnsCompactMissionDetailWithoutCustomerPhone()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        var sut = new ProviderMobileMissionDetailService(db);

        var result = await sut.GetAsync(scenario.Provider.Id, scenario.Assignment.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(scenario.Mission.MissionNumber, result.Response.MissionNumber);
        Assert.Equal("Plomberie", result.Response.ServiceName);
        Assert.Equal("Fuite evier", result.Response.PrestationName);
        Assert.Equal("Aya Kone", result.Response.CustomerDisplayName);
        Assert.False(result.Response.CanCallCustomer);
        Assert.Null(result.Response.CustomerPhoneNumber);
        Assert.True(result.Response.Actions.CanAccept);
        Assert.True(result.Response.Actions.CanRefuse);
        Assert.False(result.Response.Actions.CanStart);
        Assert.Single(result.Response.CustomerPhotos);
        Assert.Single(result.Response.RecentMessages);
    }

    [Fact]
    public async Task GetAsync_WhenCustomerConfirmed_RevealsCustomerPhoneAndStartAction()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        scenario.Assignment.Accept(5.348850m, -4.003150m, 25);
        scenario.Mission.MarkProviderAccepted(scenario.Provider.Id, scenario.Company.Id);
        scenario.Mission.ConfirmByCustomer(3_000, 0, 1_500, 0);
        await db.SaveChangesAsync();
        var sut = new ProviderMobileMissionDetailService(db);

        var result = await sut.GetAsync(scenario.Provider.Id, scenario.Assignment.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Response!.CanCallCustomer);
        Assert.Equal(scenario.Customer.PhoneNumber, result.Response.CustomerPhoneNumber);
        Assert.False(result.Response.Actions.CanAccept);
        Assert.True(result.Response.Actions.CanVerifyArrival);
        Assert.True(result.Response.Actions.CanStart);
    }

    [Fact]
    public async Task GetAsync_WhenAssignmentBelongsToAnotherProvider_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        var sut = new ProviderMobileMissionDetailService(db);

        var result = await sut.GetAsync(Guid.NewGuid(), scenario.Assignment.Id, CancellationToken.None);

        Assert.Equal(ProviderMobileMissionDetailResultStatus.NotFound, result.Status);
        Assert.Null(result.Response);
    }

    private static async Task<ProviderMissionDetailScenario> SeedScenarioAsync(HomeServiceDbContext db)
    {
        var company = new Company("Wele Services", "+2250701111111", "ops@wele.ci");
        company.Approve();
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var service = new Service("Plomberie", "Depannage eau", null);
        var prestation = service.AddPrestation("Fuite evier", null, 1, 5_000, 25_000);
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
        mission.SetServiceLocation("Cocody Angre", 5.348850m, -4.003150m, 250);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 20_000, 25_000, null, 3_000, "Joint a remplacer");

        var assignment = new ProviderMissionAssignment(
            mission.Id,
            provider.Id,
            company.Id,
            DateTimeOffset.UtcNow.AddMinutes(3));
        var photo = new MissionAttachment(
            mission.Id,
            MissionAttachmentType.CustomerPhoto,
            "evier.jpg",
            "client-missions/pending/evier.jpg",
            "image/jpeg",
            220_000,
            "Fuite visible");
        var conversation = new MissionConversation(mission.Id, provider.Id, company.Id, customer.Id);
        var message = new MissionMessage(
            conversation.Id,
            MissionMessageSenderType.Customer,
            customer.Id,
            "Le robinet sous l'evier fuit.",
            null,
            null);

        db.Companies.Add(company);
        db.Customers.Add(customer);
        db.Services.Add(service);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        db.ProviderMissionAssignments.Add(assignment);
        db.MissionAttachments.Add(photo);
        db.MissionConversations.Add(conversation);
        db.MissionMessages.Add(message);
        await db.SaveChangesAsync();

        return new ProviderMissionDetailScenario(company, customer, provider, mission, assignment);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private sealed record ProviderMissionDetailScenario(
        Company Company,
        CustomerProfile Customer,
        ProviderProfile Provider,
        Mission Mission,
        ProviderMissionAssignment Assignment);
}
