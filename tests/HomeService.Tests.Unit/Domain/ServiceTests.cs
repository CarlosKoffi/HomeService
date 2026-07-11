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
        Assert.Equal("XOF", service.Currency);
    }

    [Fact]
    public void UpdatePricing_WhenPremiumIsLowerThanNormal_UsesNormalAsPremium()
    {
        var service = new Service("Menage", null, null);

        service.UpdatePricing(1500, 1000, "xof");

        Assert.Equal(1500, service.NormalPriceAmount);
        Assert.Equal(1500, service.PremiumPriceAmount);
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
}
