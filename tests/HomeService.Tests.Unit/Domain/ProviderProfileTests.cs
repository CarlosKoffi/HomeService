using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class ProviderProfileTests
{
    [Fact]
    public void SetAvailability_WhenProviderIsNotApproved_Throws()
    {
        var provider = CreateProvider();

        Assert.Throws<InvalidOperationException>(() => provider.SetAvailability(true, 5.348850m, -4.003150m));
        Assert.False(provider.IsAvailable);
    }

    [Fact]
    public void SetAvailability_WhenProviderIsApproved_StoresLocation()
    {
        var provider = CreateProvider();
        provider.Approve();

        provider.SetAvailability(true, 5.348850m, -4.003150m);

        Assert.True(provider.IsAvailable);
        Assert.Equal(5.348850m, provider.CurrentLatitude);
        Assert.Equal(-4.003150m, provider.CurrentLongitude);
    }

    [Fact]
    public void SuspendByCompany_ClearsAvailability()
    {
        var provider = CreateApprovedAvailableProvider();

        provider.SuspendByCompany();

        Assert.Equal(ProviderStatus.SuspendedByCompany, provider.Status);
        Assert.False(provider.IsAvailable);
    }

    [Fact]
    public void Deactivate_ClearsAvailability()
    {
        var provider = CreateApprovedAvailableProvider();

        provider.Deactivate();

        Assert.Equal(ProviderStatus.Inactive, provider.Status);
        Assert.False(provider.IsAvailable);
    }

    [Fact]
    public void SyncCompanyServices_DeactivatesRemovedServicesAndUpdatesExisting()
    {
        var provider = CreateProvider();
        var serviceA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var serviceB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        provider.AddService(serviceA, ExperienceLevel.Junior);
        provider.AddService(serviceB, ExperienceLevel.Confirmed);

        provider.SyncCompanyServices([
            (serviceA, ExperienceLevel.Expert, 8, ProviderServicePriceTier.Premium)
        ]);

        var activeService = Assert.Single(provider.Services, service => service.IsActive);
        Assert.Equal(serviceA, activeService.ServiceId);
        Assert.Equal(ExperienceLevel.Expert, activeService.ExperienceLevel);
        Assert.Equal(8, activeService.YearsOfExperience);
        Assert.Equal(ProviderServicePriceTier.Premium, activeService.PriceTier);
        Assert.Contains(provider.Services, service => service.ServiceId == serviceB && !service.IsActive);
    }

    [Fact]
    public void SyncCompanyServices_DeduplicatesRequestedServicesByLastEntry()
    {
        var provider = CreateProvider();
        var serviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        provider.SyncCompanyServices([
            (serviceId, ExperienceLevel.Junior, 1, ProviderServicePriceTier.Normal),
            (serviceId, ExperienceLevel.Expert, 9, ProviderServicePriceTier.Premium)
        ]);

        var service = Assert.Single(provider.Services);
        Assert.Equal(ExperienceLevel.Expert, service.ExperienceLevel);
        Assert.Equal(9, service.YearsOfExperience);
        Assert.Equal(ProviderServicePriceTier.Premium, service.PriceTier);
    }

    private static ProviderProfile CreateApprovedAvailableProvider()
    {
        var provider = CreateProvider();
        provider.Approve();
        provider.SetAvailability(true, 5.348850m, -4.003150m);
        return provider;
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Awa",
            "Kone",
            "+2250701020304",
            new DateOnly(1995, 1, 12),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            4,
            5.348850m,
            -4.003150m,
            5);
    }
}
