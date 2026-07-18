using HomeService.Application.CompanyPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyInterimCandidateReviewResultTests
{
    [Fact]
    public void Ok_ReturnsSuccess()
    {
        var result = CompanyInterimCandidateReviewResult.Ok();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsNotFound);
        Assert.False(result.IsBlocked);
        Assert.Null(result.Message);
    }

    [Fact]
    public void NotFound_ReturnsNotFound()
    {
        var result = CompanyInterimCandidateReviewResult.NotFound();

        Assert.False(result.IsSuccess);
        Assert.True(result.IsNotFound);
        Assert.False(result.IsBlocked);
        Assert.Equal("Demande d'interim introuvable.", result.Message);
    }

    [Fact]
    public void Blocked_ReturnsBusinessBlock()
    {
        var result = CompanyInterimCandidateReviewResult.Blocked("Action desactivee.");

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotFound);
        Assert.True(result.IsBlocked);
        Assert.Equal("Action desactivee.", result.Message);
    }
}
