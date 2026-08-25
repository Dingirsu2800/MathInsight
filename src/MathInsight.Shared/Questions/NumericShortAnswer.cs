using System.Globalization;
using System.Text.RegularExpressions;

namespace MathInsight.Shared.Questions;

public static class NumericShortAnswer
{
    public const int MaximumLength = 100;

    private static readonly Regex FixedPointPattern = new(
        @"^-?\d+(?:[.,]\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParse(string? input, out decimal value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        if (trimmed.Length > MaximumLength || !FixedPointPattern.IsMatch(trimmed))
            return false;

        return decimal.TryParse(
            trimmed.Replace(',', '.'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    public static bool AreEquivalent(string? left, string? right) =>
        TryParse(left, out var leftValue) &&
        TryParse(right, out var rightValue) &&
        leftValue == rightValue;

    public static string? NormalizeOrNull(string? input) =>
        TryParse(input, out var value)
            ? value.ToString(CultureInfo.InvariantCulture)
            : null;
}
