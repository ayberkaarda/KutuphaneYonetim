using System.Globalization;

namespace KutuphaneYonetim.Business;

internal static class Validation
{
    /// <summary>Trims the value and ensures it is present and not longer than <paramref name="maxLength"/>.</summary>
    public static string Required(string? value, string fieldName, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new BusinessRuleException(
                string.Format(CultureInfo.CurrentCulture, "{0} alanı boş bırakılamaz.", fieldName));
        }

        if (trimmed.Length > maxLength)
        {
            throw new BusinessRuleException(
                string.Format(CultureInfo.CurrentCulture, "{0} en fazla {1} karakter olabilir.", fieldName, maxLength));
        }

        return trimmed;
    }
}
