using HomeService.Application.Contact;
using HomeService.Application.Auditing;
using HomeService.Contracts.Contact;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ContactRequestServiceTests
{
    [Fact]
    public async Task SubmitAsync_RejectsInvalidContactRequest()
    {
        await using var db = CreateDbContext();
        var service = new ContactRequestService(db);

        var result = await service.SubmitAsync(
            new SubmitContactRequest("Unknown", "", null, "1", "invalid", "", "Court"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(db.ContactRequests);
    }

    [Fact]
    public async Task SubmitAsync_CreatesContactRequest()
    {
        await using var db = CreateDbContext();
        var service = new ContactRequestService(db);

        var result = await service.SubmitAsync(
            new SubmitContactRequest(
                "CompanyLanding",
                "Awa Konate",
                "CI Services",
                "+2250700000000",
                "awa@example.ci",
                "Devenir partenaire",
                "Je souhaite parler a votre equipe pour inscrire mon entreprise."),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(db.ContactRequests);
        Assert.Equal("Awa Konate", request.FullName);
        Assert.Equal("CI Services", request.CompanyName);
    }

    [Fact]
    public async Task AdminActions_UpdateContactRequestStatusAndNote()
    {
        await using var db = CreateDbContext();
        var service = new ContactRequestService(db);
        var created = await service.SubmitAsync(
            new SubmitContactRequest(
                "ProviderLanding",
                "Malou Diallo",
                null,
                "+2250500000000",
                null,
                "Contact prestataire",
                "Je veux comprendre comment rejoindre une entreprise partenaire."),
            CancellationToken.None);

        var inProgress = await service.MarkInProgressAsync(
            created.Id!.Value,
            new UpdateContactRequestStatusRequest("Rappel prevu demain"),
            CancellationToken.None);
        var closed = await service.CloseAsync(
            created.Id.Value,
            new UpdateContactRequestStatusRequest("Contact traite"),
            CancellationToken.None);

        Assert.True(inProgress.IsSuccess);
        Assert.Equal("InProgress", inProgress.Response!.Status);
        Assert.True(closed.IsSuccess);
        Assert.Equal("Closed", closed.Response!.Status);
        Assert.Equal("Contact traite", closed.Response.AdminNote);
    }

    [Fact]
    public async Task AdminActions_WhenAuditActorIsProvided_CreateAuditLog()
    {
        await using var db = CreateDbContext();
        var service = new ContactRequestService(db);
        var created = await service.SubmitAsync(
            new SubmitContactRequest(
                "CompanyLanding",
                "Koffi Services",
                "Koffi Services",
                "+2250700000001",
                "contact@example.ci",
                "Partenariat",
                "Je souhaite parler avec l'equipe partenariat."),
            CancellationToken.None);

        var result = await service.MarkInProgressAsync(
            created.Id!.Value,
            new UpdateContactRequestStatusRequest("Traitement par l'equipe commerciale"),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "contact-audit"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var log = Assert.Single(db.AuditLogEntries);
        Assert.Equal("AdminContactRequestInProgress", log.Action);
        Assert.Equal(created.Id.Value, log.EntityId);
        Assert.Equal("contact-audit", log.CorrelationId);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
