using HomeService.Application.Admin;
using HomeService.Domain.Entities;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminTranslationServiceTests
{
    [Fact]
    public void TranslationResultValidationFailed_CarriesBusinessMessage()
    {
        var result = AdminTranslationResult.ValidationFailed("Le texte traduit est obligatoire.");

        Assert.Equal(AdminTranslationStatus.ValidationFailed, result.Status);
        Assert.Equal("Le texte traduit est obligatoire.", result.Message);
    }

    [Fact]
    public void TranslationResultOk_HasNoBusinessMessage()
    {
        var result = AdminTranslationResult.Ok();

        Assert.Equal(AdminTranslationStatus.Ok, result.Status);
        Assert.Null(result.Message);
    }

    [Fact]
    public void TranslationKeyUpdate_TrimsEditableMetadata()
    {
        var key = new TranslationKey("company.home.title", "Ancien titre", "Company");

        key.Update(" Nouveau titre ", " Portal ");

        Assert.Equal("Nouveau titre", key.Description);
        Assert.Equal("Portal", key.Scope);
        Assert.NotNull(key.UpdatedAt);
    }

    [Fact]
    public void TranslationValueUpdate_TrimsValue()
    {
        var value = new TranslationValue(Guid.NewGuid(), Guid.NewGuid(), null, "Ancien texte");

        value.UpdateValue(" Nouveau texte ");

        Assert.Equal("Nouveau texte", value.Value);
        Assert.NotNull(value.UpdatedAt);
    }
}
