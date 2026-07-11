using HomeService.Application.Admin;

namespace HomeService.Tests.Unit.Application;

public sealed class ReviewNoteValidatorTests
{
    [Fact]
    public void GetRequired_WhenValueIsPresent_TrimsValue()
    {
        var result = ReviewNoteValidator.GetRequired("  Piece illisible  ", "Note obligatoire");

        Assert.Equal("Piece illisible", result.Value);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetRequired_WhenValueIsMissing_ReturnsRequiredMessage(string? value)
    {
        var result = ReviewNoteValidator.GetRequired(value, "Note obligatoire");

        Assert.Null(result.Value);
        Assert.Equal("Note obligatoire", result.ErrorMessage);
    }

    [Fact]
    public void GetRequired_WhenValueIsTooLong_ReturnsMaxLengthError()
    {
        var result = ReviewNoteValidator.GetRequired(new string('a', 6), "Note obligatoire", maxLength: 5);

        Assert.Null(result.Value);
        Assert.Contains("5", result.ErrorMessage);
    }
}
