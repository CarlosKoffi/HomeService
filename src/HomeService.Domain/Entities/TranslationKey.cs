using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class TranslationKey : AuditableEntity
{
    private readonly List<TranslationValue> _values = [];

    private TranslationKey()
    {
    }

    public TranslationKey(string key, string description, string scope)
    {
        Key = key.Trim();
        Description = description.Trim();
        Scope = scope.Trim();
    }

    public string Key { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Scope { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<TranslationValue> Values => _values;
}
