using MathInsight.Modules.Recommender.Services;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

/// <summary>
/// Unit tests for DifficultyMappingService (RCM-07).
/// Covers boundary conditions: 2.99→1, 3.00→2, 4.99→2, 5.00→3, 7.49→3, 7.50→4.
/// </summary>
public class DifficultyMappingServiceTests
{
    private readonly DifficultyMappingService _sut = new();

    [Theory]
    [InlineData(0.00,  1)]
    [InlineData(2.99,  1)]
    [InlineData(3.00,  2)]
    [InlineData(4.99,  2)]
    [InlineData(5.00,  3)]
    [InlineData(7.49,  3)]
    [InlineData(7.50,  4)]
    [InlineData(10.00, 4)]
    public void MapFromOfficialPoint_ReturnsCorrectLevel(decimal point, byte expectedLevel)
    {
        var result = _sut.MapFromOfficialPoint(point);
        Assert.Equal(expectedLevel, result);
    }

    [Theory]
    [InlineData(0.00,  true)]
    [InlineData(4.99,  true)]
    [InlineData(5.00,  false)]
    [InlineData(10.00, false)]
    public void IsWeak_ReturnsTrueOnlyBelow5(decimal point, bool expected)
    {
        Assert.Equal(expected, _sut.IsWeak(point));
    }

    [Theory]
    [InlineData(1, 4.99, true)]   // level 1 and weak → remedial
    [InlineData(1, 5.00, false)]  // level 1 but not weak → not remedial
    [InlineData(2, 4.99, false)]  // level 2 even if weak → not remedial
    [InlineData(4, 2.00, false)]  // level 4, weak → not remedial
    public void IsRemedial_ReturnsTrueOnlyForLevel1AndWeak(byte level, decimal point, bool expected)
    {
        Assert.Equal(expected, _sut.IsRemedial(level, point));
    }
}
