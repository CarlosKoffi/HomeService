using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class ServiceTests
{
    [Fact]
    public void Constructor_WhenCreatedByPlatform_StartsApproved()
    {
        var service = new Service("Menage", null, null);

        Assert.Equal(ServiceStatus.Approved, service.Status);
        Assert.True(service.IsActive);
        Assert.Equal("menage", service.NormalizedName);
    }

    [Fact]
    public void Constructor_WhenCreatedByCompany_StartsPendingReview()
    {
        var service = new Service("Coiffure", null, Guid.NewGuid());

        Assert.Equal(ServiceStatus.PendingReview, service.Status);
    }

    [Fact]
    public void UpdatePricing_ClampsNormalAndPremiumAndDefaultsCurrency()
    {
        var service = new Service("Menage", null, null);

        service.UpdatePricing(-100, -50, "");

        Assert.Equal(0, service.NormalPriceAmount);
        Assert.Equal(0, service.PremiumPriceAmount);
        Assert.Equal(0, service.PriceMinAmount);
        Assert.Equal(0, service.PriceMaxAmount);
        Assert.Equal("XOF", service.Currency);
    }

    [Fact]
    public void UpdatePricing_WhenPremiumIsLowerThanNormal_UsesNormalAsPremium()
    {
        var service = new Service("Menage", null, null);

        service.UpdatePricing(1500, 1000, "xof");

        Assert.Equal(1500, service.NormalPriceAmount);
        Assert.Equal(1500, service.PremiumPriceAmount);
        Assert.Equal(1500, service.PriceMinAmount);
        Assert.Equal(1500, service.PriceMaxAmount);
        Assert.Equal("XOF", service.Currency);
    }

    [Fact]
    public void UpdatePriceRange_MirrorsLegacyPricingForCompatibility()
    {
        var service = new Service("Menage", null, null);

        service.UpdatePriceRange(3000, 5500, "xof");

        Assert.Equal(3000, service.PriceMinAmount);
        Assert.Equal(5500, service.PriceMaxAmount);
        Assert.Equal(3000, service.NormalPriceAmount);
        Assert.Equal(5500, service.PremiumPriceAmount);
        Assert.Equal("XOF", service.Currency);
    }

    [Fact]
    public void Constructor_UsesDefaultIcon()
    {
        var service = new Service("Menage", null, null);

        Assert.Equal("sparkles", service.IconName);
    }

    [Fact]
    public void UpdateIcon_NormalizesValueAndFallsBackToDefault()
    {
        var service = new Service("Jardinage", null, null);

        service.UpdateIcon(" Sprout ");

        Assert.Equal("sprout", service.IconName);

        service.UpdateIcon(" ");

        Assert.Equal("sparkles", service.IconName);
    }

    [Fact]
    public void UpdateAssignmentRequirements_WhenPortfolioIsDisabled_SetsMinimumToZero()
    {
        var service = new Service("Menage", null, null);

        service.UpdateAssignmentRequirements(false, 3, true, false, false, false);

        Assert.False(service.RequiresPortfolio);
        Assert.Equal(0, service.MinimumPortfolioItems);
    }

    [Fact]
    public void UpdateAssignmentRequirements_WhenPortfolioIsEnabled_RequiresAtLeastOneItem()
    {
        var service = new Service("Coiffure", null, null);

        service.UpdateAssignmentRequirements(true, 0, true, true, false, true);

        Assert.True(service.RequiresPortfolio);
        Assert.Equal(1, service.MinimumPortfolioItems);
        Assert.True(service.RequiresCompletionPhoto);
        Assert.True(service.RequiresBeforeAfterPhotos);
        Assert.True(service.RequiresAdminApprovalBeforeAssignment);
    }

    [Fact]
    public void Constructor_StartsWithoutPrestations()
    {
        var service = new Service("Electricite", null, null);

        Assert.Empty(service.Prestations);
    }

    [Fact]
    public void AddPrestation_AddsChildLinkedToService()
    {
        var service = new Service("Jardinage", null, null);

        var prestation = service.AddPrestation("Tondre le gazon", "Coupe simple.", 10);

        Assert.Single(service.Prestations);
        Assert.Equal(service.Id, prestation.ServiceId);
        Assert.Equal("Tondre le gazon", prestation.Name);
        Assert.Equal("tondre le gazon", prestation.NormalizedName);
        Assert.Equal(10, prestation.SortOrder);
        Assert.True(prestation.IsActive);
    }

    [Fact]
    public void AddPrestation_WhenNameAlreadyExists_UpdatesExistingPrestation()
    {
        var service = new Service("Jardinage", null, null);
        var first = service.AddPrestation("Tondre le gazon", "Coupe simple.", 10);

        var second = service.AddPrestation(" Tondre le gazon ", "Coupe complete.", 20);

        Assert.Same(first, second);
        Assert.Single(service.Prestations);
        Assert.Equal("Tondre le gazon", second.Name);
        Assert.Equal("Coupe complete.", second.Description);
        Assert.Equal(20, second.SortOrder);
    }

    [Fact]
    public void AddPrestation_StoresPrestationPricing()
    {
        var service = new Service("Jardinage", null, null);

        var prestation = service.AddPrestation("Tondre le gazon", "Coupe simple.", 10, 4500, 6500, "xof");

        Assert.Equal(4500, prestation.NormalPriceAmount);
        Assert.Equal(6500, prestation.PremiumPriceAmount);
        Assert.Equal(4500, prestation.PriceMinAmount);
        Assert.Equal(6500, prestation.PriceMaxAmount);
        Assert.Equal("XOF", prestation.Currency);
    }

    [Fact]
    public void AddPrestation_WhenNameAlreadyExists_UpdatesPricing()
    {
        var service = new Service("Jardinage", null, null);
        var first = service.AddPrestation("Tondre le gazon", "Coupe simple.", 10, 4500, 6500, "XOF");

        var second = service.AddPrestation("Tondre le gazon", "Coupe premium.", 20, 6000, 8000, "XOF");

        Assert.Same(first, second);
        Assert.Equal(6000, second.NormalPriceAmount);
        Assert.Equal(8000, second.PremiumPriceAmount);
        Assert.Equal(6000, second.PriceMinAmount);
        Assert.Equal(8000, second.PriceMaxAmount);
        Assert.Equal(20, second.SortOrder);
    }
}
