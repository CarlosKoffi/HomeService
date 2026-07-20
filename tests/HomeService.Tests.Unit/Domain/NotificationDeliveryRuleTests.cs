using HomeService.Domain.Entities;

namespace HomeService.Tests.Unit.Domain;

public sealed class NotificationDeliveryRuleTests
{
    [Fact]
    public void Constructor_WhenDataIsValid_CreatesRule()
    {
        var rule = new NotificationDeliveryRule(
            "CompanyDocumentRejected",
            "Piece entreprise refusee",
            "Company",
            portalEnabled: true,
            mobileAppEnabled: false,
            emailEnabled: true,
            whatsAppEnabled: true);

        Assert.Equal("CompanyDocumentRejected", rule.EventKey);
        Assert.Equal("Company", rule.Audience);
        Assert.True(rule.PortalEnabled);
        Assert.False(rule.MobileAppEnabled);
        Assert.True(rule.EmailEnabled);
        Assert.True(rule.WhatsAppEnabled);
    }

    [Fact]
    public void Update_WhenDataIsValid_UpdatesChannels()
    {
        var rule = new NotificationDeliveryRule(
            "InterimCandidateReceived",
            "Nouvelle demande interimaire",
            "Company",
            portalEnabled: true,
            mobileAppEnabled: false,
            emailEnabled: false,
            whatsAppEnabled: false);

        rule.Update(
            "Demande interimaire",
            "Company",
            portalEnabled: true,
            mobileAppEnabled: false,
            emailEnabled: true,
            whatsAppEnabled: false);

        Assert.Equal("Demande interimaire", rule.Label);
        Assert.True(rule.PortalEnabled);
        Assert.False(rule.MobileAppEnabled);
        Assert.True(rule.EmailEnabled);
        Assert.False(rule.WhatsAppEnabled);
        Assert.NotNull(rule.UpdatedAt);
    }

    [Fact]
    public void Constructor_WhenEventKeyIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => new NotificationDeliveryRule(
            "",
            "Piece entreprise refusee",
            "Company",
            portalEnabled: true,
            mobileAppEnabled: false,
            emailEnabled: true,
            whatsAppEnabled: true));
    }
}
