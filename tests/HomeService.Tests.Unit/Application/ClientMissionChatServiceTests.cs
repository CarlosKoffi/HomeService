using HomeService.Application.Clients;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMissionChatServiceTests
{
    [Fact]
    public async Task ListAsync_AssignedProviderWithPhoto_ReturnsProviderIdentity()
    {
        await using var db = CreateDbContext();
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var service = new Service("Plomberie", "Dépannage eau", createdByCompanyId: null);
        var company = new Company("Plomb Pro", "+2250701111111", "ops@plombpro.ci");
        company.Approve();
        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Konate",
            "+2250702222222",
            "awa@plombpro.ci",
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
            scheduledFor: null,
            estimatedDurationMinutes: 60,
            description: "Fuite sous évier",
            requiresCompanyQuote: true);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 20_000, 25_000, null);
        mission.MarkProviderAccepted(provider.Id, company.Id);
        var providerPhoto = new ProviderDocument(
            provider.Id,
            ProviderDocumentType.Photo,
            "awa.jpg",
            "providers/awa/photo.jpg",
            "image/jpeg");
        db.AddRange(customer, service, company, provider, mission, providerPhoto);
        await db.SaveChangesAsync();
        var sut = new ClientMissionChatService(
            db,
            new MobilePushNotificationQueueService(db),
            new MobileNavigationBadgeService(db));

        var result = await sut.ListAsync(mission.Id, customer.PhoneNumber, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ChatResponse);
        Assert.Equal("Awa Konate", result.ChatResponse.ProviderName);
        Assert.Equal(
            $"/api/client/missions/{mission.Id:D}/provider-photo",
            result.ChatResponse.ProviderPhotoUrl);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HomeServiceDbContext(options);
    }
}
