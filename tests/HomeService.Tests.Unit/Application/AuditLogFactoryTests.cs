using HomeService.Application.Auditing;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class AuditLogFactoryTests
{
    [Fact]
    public void Create_BuildsAuditEntryWithActorAndRequestContext()
    {
        var actorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var entityId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var context = new AuditRequestContext("196.47.0.10", "Mobile Safari", "trace-123");

        var entry = AuditLogFactory.Create(
            AuditActor.Company(actorId, "CI Home Service"),
            "CompanyEmployeeCreated",
            "ProviderProfile",
            entityId,
            "Employe ajoute",
            context);

        Assert.Equal(AuditActorType.Company, entry.ActorType);
        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal("CI Home Service", entry.ActorDisplayName);
        Assert.Equal("CompanyEmployeeCreated", entry.Action);
        Assert.Equal("ProviderProfile", entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal("196.47.0.10", entry.IpAddress);
        Assert.Equal("Mobile Safari", entry.UserAgent);
        Assert.Equal("trace-123", entry.CorrelationId);
    }

    [Fact]
    public void Create_RedactsSensitiveFieldsFromSnapshots()
    {
        var entry = AuditLogFactory.Create(
            AuditActor.Admin(),
            "CompanyActivationLinkGenerated",
            "CompanyApplication",
            Guid.NewGuid(),
            "Lien genere",
            null,
            after: new
            {
                ActivationLink = "https://secret-link",
                TokenHash = "hash",
                Email = "direction@entreprise.ci",
                Nested = new { Password = "Secret123" }
            });

        Assert.NotNull(entry.AfterJson);
        Assert.DoesNotContain("https://secret-link", entry.AfterJson);
        Assert.DoesNotContain("Secret123", entry.AfterJson);
        Assert.Contains("\"activationLink\":\"***\"", entry.AfterJson);
        Assert.Contains("\"tokenHash\":\"***\"", entry.AfterJson);
        Assert.Contains("\"password\":\"***\"", entry.AfterJson);
        Assert.Contains("direction@entreprise.ci", entry.AfterJson);
    }
}
