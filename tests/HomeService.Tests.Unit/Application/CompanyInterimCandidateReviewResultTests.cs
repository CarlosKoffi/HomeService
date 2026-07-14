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
    }

    [Fact]
    public void NotFound_ReturnsNotFound()
    {
        var result = CompanyInterimCandidateReviewResult.NotFound();

        Assert.False(result.IsSuccess);
        Assert.True(result.IsNotFound);
    }
}
