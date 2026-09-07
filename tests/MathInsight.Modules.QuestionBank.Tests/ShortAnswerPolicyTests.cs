using MathInsight.Shared.Questions;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class ShortAnswerPolicyTests
{
    [Theory]
    [InlineData("pi", "π")]
    [InlineData(" PI ", "π")]
    [InlineData("sqrt(2)", "√(2)")]
    [InlineData("  Hà   Nội  ", "hà nội")]
    public void TryNormalize_NormalizesSupportedAliasesAndWhitespace(string input, string expected)
    {
        Assert.True(ShortAnswerPolicy.TryNormalize(input, out var normalized));

        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("π", "pi")]
    [InlineData("√(2)", "sqrt(2)")]
    [InlineData("1.5", "1,50")]
    [InlineData("Hà Nội", "  hà   nội ")]
    public void AreEquivalent_MatchesCanonicalTextAndNumbers(string expected, string actual)
        => Assert.True(ShortAnswerPolicy.AreEquivalent(expected, actual));

    [Theory]
    [InlineData("2", "√(4)")]
    [InlineData("ab", "a b")]
    [InlineData("1e3", "1000")]
    public void AreEquivalent_DoesNotEvaluateExpressionsOrAcceptInvalidNumericNotation(string expected, string actual)
        => Assert.False(ShortAnswerPolicy.AreEquivalent(expected, actual));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("line1\nline2")]
    [InlineData("a\u200Bb")]
    public void TryNormalize_RejectsEmptyAndInvisibleFormatting(string? input)
        => Assert.False(ShortAnswerPolicy.TryNormalize(input, out _));

    [Fact]
    public void TryNormalize_RejectsOverlengthUnicodeInputBeforeNormalization()
        => Assert.False(ShortAnswerPolicy.TryNormalize(new string('a', ShortAnswerPolicy.MaximumLength + 1), out _));
}
