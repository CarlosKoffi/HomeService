using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class ProviderServiceTests
{
    [Fact]
    public void SyncCompanyServices_AddsReactivatesAndDeactivatesServices()
    {
        var provider = new ProviderProfile(
            Guid.NewGuid(),
            "Awa",
            "Konate",
            "+2250700000000",
            null,
            new DateOnly(1995, 1, 1),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            4,
            null,
            null,
            5);
        var firstServiceId = Guid.NewGuid();
        var secondServiceId = Guid.NewGuid();

        provider.SyncCompanyServices(
        [
            (firstServiceId, ExperienceLevel.Confirmed, 4, ProviderServicePriceTier.Normal),
            (secondServiceId, ExperienceLevel.Junior, 1, ProviderServicePriceTier.Normal)
        ]);
        provider.SyncCompanyServices(
        [
            (secondServiceId, ExperienceLevel.Expert, 6, ProviderServicePriceTier.Premium)
        ]);
        provider.SyncCompanyServices(
        [
            (firstServiceId, ExperienceLevel.Confirmed, 5, ProviderServicePriceTier.Normal),
            (secondServiceId, ExperienceLevel.Expert, 6, ProviderServicePriceTier.Premium)
        ]);

        Assert.Equal(2, provider.Services.Count);
        Assert.All(provider.Services, service => Assert.True(service.IsActive));
        Assert.Contains(provider.Services, service => service.ServiceId == firstServiceId && service.YearsOfExperience == 5);
        Assert.Contains(provider.Services, service => service.ServiceId == secondServiceId && service.PriceTier == ProviderServicePriceTier.Premium);
    }

    [Fact]
    public void SyncPrestations_AddsRequestedPrestationsOnlyOnce()
    {
        var providerService = new ProviderService(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExperienceLevel.Confirmed,
            4);
        var firstPrestationId = Guid.NewGuid();
        var secondPrestationId = Guid.NewGuid();

        providerService.SyncPrestations([firstPrestationId, secondPrestationId, firstPrestationId]);

        Assert.Equal(2, providerService.Prestations.Count);
        Assert.All(providerService.Prestations, prestation => Assert.True(prestation.IsActive));
    }

    [Fact]
    public void SyncPrestations_DeactivatesRemovedPrestationsAndReactivatesExistingOnes()
    {
        var providerService = new ProviderService(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExperienceLevel.Confirmed,
            4);
        var firstPrestationId = Guid.NewGuid();
        var secondPrestationId = Guid.NewGuid();

        providerService.SyncPrestations([firstPrestationId, secondPrestationId]);
        providerService.SyncPrestations([secondPrestationId]);
        providerService.SyncPrestations([firstPrestationId, secondPrestationId]);

        Assert.Equal(2, providerService.Prestations.Count);
        Assert.All(providerService.Prestations, prestation => Assert.True(prestation.IsActive));
    }
}
