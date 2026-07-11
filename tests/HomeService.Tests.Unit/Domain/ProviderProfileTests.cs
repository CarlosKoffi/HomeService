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

    [Fact]
    public void SelfRegisteredProvider_StartsAsInterimCandidateWithoutCompany()
    {
        var provider = CreateSelfRegisteredProvider();

        Assert.Null(provider.CompanyId);
        Assert.Equal(ProviderStatus.InterimCandidate, provider.Status);
        Assert.Equal(ProviderEmploymentType.TemporaryWorker, provider.EmploymentType);
        Assert.Equal(ProviderRegistrationSource.SelfRegistration, provider.RegistrationSource);
    }

    [Fact]
    public void SyncCandidateServices_StoresDeclaredServicesWithoutMakingThemAssignable()
    {
        var provider = CreateSelfRegisteredProvider();
        var serviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        provider.SyncCandidateServices([(serviceId, ExperienceLevel.Senior, 6)]);

        var candidateService = Assert.Single(provider.CandidateServices);
        Assert.Equal(serviceId, candidateService.ServiceId);
        Assert.Equal(ExperienceLevel.Senior, candidateService.ExperienceLevel);
        Assert.Empty(provider.Services);
    }

    [Fact]
    public void AttachToCompanyAsTemporaryWorker_ApprovesProviderAndAllowsCompanyServices()
    {
        var provider = CreateSelfRegisteredProvider();
        var companyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var serviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        provider.SyncCandidateServices([(serviceId, ExperienceLevel.Senior, 6)]);

        provider.AttachToCompanyAsTemporaryWorker(companyId);
        provider.SyncCompanyServices(provider.CandidateServices.Select(service => (
            service.ServiceId,
            service.ExperienceLevel,
            service.YearsOfExperience,
            ProviderServicePriceTier.Normal)));

        Assert.Equal(companyId, provider.CompanyId);
        Assert.Equal(ProviderStatus.Approved, provider.Status);
        Assert.Equal(ProviderEmploymentType.TemporaryWorker, provider.EmploymentType);
        Assert.Single(provider.Services);
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

    private static ProviderProfile CreateSelfRegisteredProvider()
    {
        return new ProviderProfile(
            "Awa",
            "Kone",
            "+2250701020304",
            new DateOnly(1995, 1, 12),
            "Cocody",
            ProviderGender.Female,
            4,
            5.348850m,
            -4.003150m,
            5);
    }
}
