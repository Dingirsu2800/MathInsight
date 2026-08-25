using MathInsight.Shared.Questions;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class NumericShortAnswerTests
{
    [Theory]
    [InlineData("12", "12")]
    [InlineData("-3", "-3")]
    [InlineData("1.5", "1.5")]
    [InlineData(" 1,5 ", "1.5")]
    public void TryParse_AcceptsFixedPointNumbers(string input, string normalized)
    {
        Assert.True(NumericShortAnswer.TryParse(input, out _));
        Assert.Equal(normalized, NumericShortAnswer.NormalizeOrNull(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-")]
    [InlineData(".")]
    [InlineData(",")]
    [InlineData(".5")]
    [InlineData("π")]
    [InlineData("A")]
    [InlineData("vô nghiệm")]
    [InlineData("1/2")]
    [InlineData("1e3")]
    [InlineData("1,2.3")]
    public void TryParse_RejectsNonFixedPointNumbers(string? input) =>
        Assert.False(NumericShortAnswer.TryParse(input, out _));

    [Fact]
    public void AreEquivalent_TreatsDecimalCommaAndDotAsSameValue() =>
        Assert.True(NumericShortAnswer.AreEquivalent("1.50", "1,5"));
}
