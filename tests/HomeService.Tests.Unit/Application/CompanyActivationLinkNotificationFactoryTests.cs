using HomeService.Application.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyActivationLinkNotificationFactoryTests
{
    [Fact]
    public void Create_BuildsEmailAndWhatsappMessagesWithActivationLinkMetadata()
    {
        var application = new CompanyApplication(
            "CI Home Service",
            null,
            "Abidjan",
            null,
            "John PriPri",
            "direction@entreprise.ci",
            "+2250700000000",
            "Menage",
            12);

        var messages = CompanyActivationLinkNotificationFactory.Create(
            application,
            "https://company.kaza.ci/activate?token=abc",
            new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, message => message.Channel == NotificationChannel.Email && message.Recipient == "direction@entreprise.ci");
        Assert.Contains(messages, message => message.Channel == NotificationChannel.WhatsApp && message.Recipient == "+2250700000000");
        Assert.All(messages, message => Assert.Contains("activationLink", message.MetadataJson));
        Assert.All(messages, message => Assert.Contains("Votre portail entreprise est pret", message.Subject));
    }
}
