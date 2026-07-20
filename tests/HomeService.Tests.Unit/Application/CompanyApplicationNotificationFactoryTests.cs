using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyApplicationNotificationFactoryTests
{
    [Fact]
    public void CreateApplicantNotifications_WhenEmailAndPhoneExist_CreatesEmailAndWhatsAppMessages()
    {
        var application = CreateApplication();

        var messages = CompanyApplicationNotificationFactory.CreateApplicantNotifications(
            application,
            "Sujet",
            "Corps",
            includeEmail: true,
            includeWhatsApp: true,
            metadataJson: "{\"kind\":\"test\"}");

        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, message => message.Channel == NotificationChannel.Email && message.Recipient == application.Email);
        Assert.Contains(messages, message => message.Channel == NotificationChannel.WhatsApp && message.Recipient == application.PhoneNumber);
        Assert.All(messages, message =>
        {
            Assert.Equal("Sujet", message.Subject);
            Assert.Equal("Corps", message.Body);
            Assert.Equal(nameof(CompanyApplication), message.RelatedEntityType);
            Assert.Equal(application.Id, message.RelatedEntityId);
            Assert.Equal("{\"kind\":\"test\"}", message.MetadataJson);
        });
    }

    [Fact]
    public void CreateApplicantNotifications_WhenWhatsAppDisabled_CreatesEmailOnly()
    {
        var application = CreateApplication();

        var messages = CompanyApplicationNotificationFactory.CreateApplicantNotifications(
            application,
            "Sujet",
            "Corps",
            includeEmail: true,
            includeWhatsApp: false);

        var message = Assert.Single(messages);
        Assert.Equal(NotificationChannel.Email, message.Channel);
    }

    [Fact]
    public void Rejected_IncludesRefusalNote()
    {
        var application = CreateApplication();

        var messages = CompanyApplicationNotificationFactory.Rejected(application, "Document illisible", AllChannels());

        Assert.All(messages, message => Assert.Contains("Document illisible", message.Body));
        Assert.All(messages, message => Assert.Contains("refuse", message.Subject, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Reopened_IncludesReviewNote()
    {
        var application = CreateApplication();

        var messages = CompanyApplicationNotificationFactory.Reopened(application, "Nouvelle piece recue", AllChannels());

        Assert.All(messages, message => Assert.Contains("Nouvelle piece recue", message.Body));
        Assert.All(messages, message => Assert.Contains("reouvert", message.Subject, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MoreInformationRequested_IncludesRequestedDetail()
    {
        var application = CreateApplication();

        var messages = CompanyApplicationNotificationFactory.MoreInformationRequested(application, "Ajouter le DFE", AllChannels());

        Assert.All(messages, message => Assert.Contains("Ajouter le DFE", message.Body));
        Assert.All(messages, message => Assert.Contains("Complement", message.Subject, StringComparison.OrdinalIgnoreCase));
    }

    private static CompanyApplication CreateApplication()
    {
        return new CompanyApplication(
            "CI Home Service",
            null,
            "Abidjan",
            "Cocody",
            "John Pripri",
            "direction@entreprise.ci",
            "+2250701020304",
            "Menage",
            12);
    }

    private static NotificationDeliveryPreference AllChannels()
    {
        return new NotificationDeliveryPreference(
            PortalEnabled: true,
            MobileAppEnabled: false,
            EmailEnabled: true,
            WhatsAppEnabled: true);
    }
}
