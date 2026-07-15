using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class ProviderServiceTests
{
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
