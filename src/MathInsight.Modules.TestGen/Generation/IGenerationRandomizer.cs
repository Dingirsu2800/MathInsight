namespace MathInsight.Modules.TestGen.Generation;

public interface IGenerationRandomizer
{
    void Shuffle<T>(IList<T> values);
}

public sealed class SystemGenerationRandomizer : IGenerationRandomizer
{
    public void Shuffle<T>(IList<T> values)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}
