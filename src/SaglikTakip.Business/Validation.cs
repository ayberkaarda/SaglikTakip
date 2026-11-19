using System.Globalization;

namespace SaglikTakip.Business;

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

        return MaxLength(trimmed, fieldName, maxLength);
    }

    /// <summary>Trims the value; blank input becomes <see langword="null"/>.</summary>
    public static string? Optional(string? value, string fieldName, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : MaxLength(trimmed, fieldName, maxLength);
    }

    /// <summary>Ensures <paramref name="minimum"/> &lt;= value &lt;= <paramref name="maximum"/>.</summary>
    public static T Range<T>(T value, string fieldName, T minimum, T maximum)
        where T : IComparable<T>
    {
        if (value.CompareTo(minimum) < 0 || value.CompareTo(maximum) > 0)
        {
            throw new BusinessRuleException(string.Format(
                CultureInfo.CurrentCulture,
                "{0} {1} ile {2} arasında olmalıdır.",
                fieldName,
                minimum,
                maximum));
        }

        return value;
    }

    private static string MaxLength(string value, string fieldName, int maxLength)
    {
        if (value.Length > maxLength)
        {
            throw new BusinessRuleException(
                string.Format(CultureInfo.CurrentCulture, "{0} en fazla {1} karakter olabilir.", fieldName, maxLength));
        }

        return value;
    }
}
