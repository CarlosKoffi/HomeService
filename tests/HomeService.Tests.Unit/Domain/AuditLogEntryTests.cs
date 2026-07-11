using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class AuditLogEntryTests
{
    [Fact]
    public void Constructor_TrimsTextFields()
    {
        var entry = new AuditLogEntry(
            AuditActorType.Admin,
            null,
            " admin ",
            " Action ",
            " Entity ",
            Guid.NewGuid(),
            " summary ",
            null,
            null,
            " 127.0.0.1 ",
            " browser ",
            " trace ");

        Assert.Equal("admin", entry.ActorDisplayName);
        Assert.Equal("Action", entry.Action);
        Assert.Equal("Entity", entry.EntityType);
        Assert.Equal("summary", entry.Summary);
        Assert.Equal("127.0.0.1", entry.IpAddress);
        Assert.Equal("browser", entry.UserAgent);
        Assert.Equal("trace", entry.CorrelationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenActionIsMissing_Throws(string action)
    {
        Assert.Throws<ArgumentException>(() => new AuditLogEntry(
            AuditActorType.Admin,
            null,
            null,
            action,
            "Entity",
            null,
            null,
            null,
            null,
            null,
            null,
            null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenEntityTypeIsMissing_Throws(string entityType)
    {
        Assert.Throws<ArgumentException>(() => new AuditLogEntry(
            AuditActorType.Admin,
            null,
            null,
            "Action",
            entityType,
            null,
            null,
            null,
            null,
            null,
            null,
            null));
    }
}
