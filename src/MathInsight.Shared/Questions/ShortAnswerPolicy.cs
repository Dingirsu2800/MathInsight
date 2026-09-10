using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MathInsight.Shared.Questions;

/// <summary>
/// Normalizes text-based short answers without attempting symbolic mathematics.
/// </summary>
public static partial class ShortAnswerPolicy
{
    public const int MaximumLength = 100;

    private static readonly Regex PiAlias = CreatePiAliasRegex();
    private static readonly Regex RepeatedSpaces = CreateRepeatedSpacesRegex();

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input) ||
            input.EnumerateRunes().Count() > MaximumLength ||
            ContainsForbiddenCharacters(input))
            return false;

        var value = input.Normalize(NormalizationForm.FormKC).Trim();
        if (value.Length == 0 || ContainsForbiddenCharacters(value))
            return false;

        value = NormalizeSqrtAliases(value);
        value = PiAlias.Replace(value, "π");
        value = RepeatedSpaces.Replace(value, " ").Trim();

        if (value.Length == 0 || value.EnumerateRunes().Count() > MaximumLength)
            return false;

        normalized = value.ToLowerInvariant();
        return true;
    }

    public static bool AreEquivalent(string? expected, string? actual)
    {
        if (!TryNormalize(expected, out var normalizedExpected) ||
            !TryNormalize(actual, out var normalizedActual))
        {
            return false;
        }

        if (NumericShortAnswer.TryParse(normalizedExpected, out var expectedNumber) &&
            NumericShortAnswer.TryParse(normalizedActual, out var actualNumber))
        {
            return expectedNumber == actualNumber;
        }

        return string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal);
    }

    private static bool ContainsForbiddenCharacters(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate)
                return true;
        }

        return false;
    }

    private static string NormalizeSqrtAliases(string value)
    {
        var output = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length;)
        {
            if (StartsWithSqrtAlias(value, index, out var openingParenthesis))
            {
                var closingParenthesis = FindMatchingParenthesis(value, openingParenthesis);
                if (closingParenthesis >= 0)
                {
                    output.Append('√');
                    output.Append(value, openingParenthesis, closingParenthesis - openingParenthesis + 1);
                    index = closingParenthesis + 1;
                    continue;
                }
            }

            output.Append(value[index]);
            index++;
        }

        return output.ToString();
    }

    private static bool StartsWithSqrtAlias(string value, int index, out int openingParenthesis)
    {
        openingParenthesis = -1;
        const string alias = "sqrt";
        if (index + alias.Length >= value.Length ||
            !value.AsSpan(index, alias.Length).Equals(alias, StringComparison.OrdinalIgnoreCase) ||
            (index > 0 && IsIdentifierCharacter(value[index - 1])) ||
            value[index + alias.Length] != '(')
        {
            return false;
        }

        openingParenthesis = index + alias.Length;
        return true;
    }

    private static int FindMatchingParenthesis(string value, int openingParenthesis)
    {
        var depth = 0;
        for (var index = openingParenthesis; index < value.Length; index++)
        {
            if (value[index] == '(') depth++;
            if (value[index] == ')' && --depth == 0) return index;
        }

        return -1;
    }

    private static bool IsIdentifierCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

    [GeneratedRegex(@"(?<![\p{L}\p{N}_])pi(?![\p{L}\p{N}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CreatePiAliasRegex();

    [GeneratedRegex(@"[ \t]+", RegexOptions.CultureInvariant)]
    private static partial Regex CreateRepeatedSpacesRegex();
}
