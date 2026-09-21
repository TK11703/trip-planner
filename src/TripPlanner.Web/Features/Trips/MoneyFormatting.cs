using System.Globalization;

namespace TripPlanner.Web.Features.Trips;

/// <summary>
/// Formats estimated amounts as US dollars. Trip amounts are always US dollars, so the
/// format is pinned here rather than read from the ambient culture, which resolves to the
/// invariant culture (and its generic <c>\u00A4</c> currency sign) in a container with no locale.
/// </summary>
public static class MoneyFormatting
{
    private static readonly NumberFormatInfo UsDollars = CreateUsDollars();

    private static NumberFormatInfo CreateUsDollars()
    {
        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.CurrencySymbol = "$";
        format.CurrencyDecimalSeparator = ".";
        format.CurrencyGroupSeparator = ",";
        format.CurrencyDecimalDigits = 2;
        format.CurrencyPositivePattern = 0;
        format.CurrencyNegativePattern = 1;
        return NumberFormatInfo.ReadOnly(format);
    }

    /// <summary>Formats an amount as <c>$1,234.50</c>.</summary>
    public static string ToDisplayAmount(this decimal amount) => amount.ToString("C", UsDollars);
}
