namespace HomeService.Application.Admin;

public static class ReviewNoteValidator
{
    public static (string? Value, string? ErrorMessage) GetRequired(string? value, string requiredMessage, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, requiredMessage);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            return (null, $"La note ne peut pas depasser {maxLength} caracteres.");
        }

        return (trimmed, null);
    }
}
