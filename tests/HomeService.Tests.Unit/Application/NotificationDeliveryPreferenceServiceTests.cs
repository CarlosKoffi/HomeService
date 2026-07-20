using HomeService.Application.Notifications;

namespace HomeService.Tests.Unit.Application;

public sealed class NotificationDeliveryPreferenceServiceTests
{
    [Theory]
    [InlineData("Company", true)]
    [InlineData("Mixed", true)]
    [InlineData("Provider", false)]
    [InlineData("Customer", false)]
    [InlineData("", true)]
    public void IsPortalAutomatic_ReturnsExpectedChannel(string audience, bool expected)
    {
        Assert.Equal(expected, NotificationDeliveryPreferenceService.IsPortalAutomatic(audience));
    }

    [Theory]
    [InlineData("Provider", true)]
    [InlineData("Customer", true)]
    [InlineData("Mixed", true)]
    [InlineData("", true)]
    [InlineData("Company", false)]
    public void IsMobileAppAutomatic_ReturnsExpectedChannel(string audience, bool expected)
    {
        Assert.Equal(expected, NotificationDeliveryPreferenceService.IsMobileAppAutomatic(audience));
    }
}
