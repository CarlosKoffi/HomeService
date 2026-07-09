using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class TranslationValue : AuditableEntity
{
    private TranslationValue()
    {
    }

    public TranslationValue(Guid translationKeyId, Guid languageId, Guid? countryId, string value)
    {
        TranslationKeyId = translationKeyId;
        LanguageId = languageId;
        CountryId = countryId;
        Value = value.Trim();
    }

    public Guid TranslationKeyId { get; private set; }
    public TranslationKey? TranslationKey { get; private set; }
    public Guid LanguageId { get; private set; }
    public Language? Language { get; private set; }
    public Guid? CountryId { get; private set; }
    public Country? Country { get; private set; }
    public string Value { get; private set; } = string.Empty;
}
