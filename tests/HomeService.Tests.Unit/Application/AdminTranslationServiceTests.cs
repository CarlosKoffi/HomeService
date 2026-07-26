using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Contracts.Localization;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

    [Fact]
    public async Task UpsertAsync_WhenTranslationIsNew_PersistsValueAndAudit()
    {
        await using var db = CreateDbContext();
        db.Languages.Add(new Language("fr", "Francais", true));
        db.Countries.Add(new Country("CI", "Cote d'Ivoire", "XOF", true));
        await db.SaveChangesAsync();
        var sut = new AdminTranslationService(db);

        var result = await sut.UpsertAsync(
            new UpsertAdminTranslationRequest(
                "company.home.hero.title",
                "CompanySite",
                "Titre hero",
                "fr",
                "Developpez votre activite"),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "translation-create"),
            CancellationToken.None);

        Assert.Equal(AdminTranslationStatus.Ok, result.Status);
        var key = await db.TranslationKeys.SingleAsync(item => item.Key == "company.home.hero.title");
        var value = await db.TranslationValues.SingleAsync(item => item.TranslationKeyId == key.Id);
        Assert.Equal("Developpez votre activite", value.Value);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminTranslationSaved", audit.Action);
        Assert.Equal(nameof(TranslationKey), audit.EntityType);
        Assert.Equal(key.Id, audit.EntityId);
        Assert.Equal("translation-create", audit.CorrelationId);
    }

    [Fact]
    public async Task UpsertAsync_WhenTranslationExists_UpdatesValueAndAudit()
    {
        await using var db = CreateDbContext();
        var language = new Language("fr", "Francais", true);
        var country = new Country("CI", "Cote d'Ivoire", "XOF", true);
        var key = new TranslationKey("company.home.cta", "Ancien CTA", "CompanySite");
        db.Languages.Add(language);
        db.Countries.Add(country);
        db.TranslationKeys.Add(key);
        db.TranslationValues.Add(new TranslationValue(key.Id, language.Id, country.Id, "Ancien texte"));
        await db.SaveChangesAsync();
        var sut = new AdminTranslationService(db);

        var result = await sut.UpsertAsync(
            new UpsertAdminTranslationRequest(
                "company.home.cta",
                "CompanySite",
                "CTA principal",
                "fr",
                "Devenir partenaire"),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "translation-update"),
            CancellationToken.None);

        Assert.Equal(AdminTranslationStatus.Ok, result.Status);
        var savedKey = await db.TranslationKeys.SingleAsync(item => item.Id == key.Id);
        var savedValue = await db.TranslationValues.SingleAsync(item => item.TranslationKeyId == key.Id);
        Assert.Equal("CTA principal", savedKey.Description);
        Assert.Equal("Devenir partenaire", savedValue.Value);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminTranslationSaved", audit.Action);
        Assert.Equal(key.Id, audit.EntityId);
        Assert.Equal("translation-update", audit.CorrelationId);
        Assert.Contains("Ancien texte", audit.BeforeJson);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
