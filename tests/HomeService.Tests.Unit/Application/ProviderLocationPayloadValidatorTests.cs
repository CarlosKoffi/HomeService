using HomeService.Application.ProviderPortal;
using HomeService.Contracts.ProviderPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderLocationPayloadValidatorTests
{
    [Fact]
    public void Validate_WhenPayloadIsComplete_ReturnsNoError()
    {
        var request = new ProviderLocationVerificationRequest(5.348850m, -4.003150m, 25);

        var error = ProviderLocationPayloadValidator.Validate(request);

        Assert.Null(error);
    }

    [Theory]
    [InlineData(null, -4.003150, 25)]
    [InlineData(5.348850, null, 25)]
    [InlineData(91.0, -4.003150, 25)]
    [InlineData(5.348850, -181.0, 25)]
    [InlineData(5.348850, -4.003150, null)]
    [InlineData(5.348850, -4.003150, 0)]
    [InlineData(5.348850, -4.003150, 151)]
    public void Validate_WhenPayloadIsUnsafe_ReturnsError(double? latitude, double? longitude, int? accuracyMeters)
    {
        var request = new ProviderLocationVerificationRequest(
            latitude is null ? null : Convert.ToDecimal(latitude.Value),
            longitude is null ? null : Convert.ToDecimal(longitude.Value),
            accuracyMeters);

        var error = ProviderLocationPayloadValidator.Validate(request);

        Assert.NotNull(error);
    }
}
