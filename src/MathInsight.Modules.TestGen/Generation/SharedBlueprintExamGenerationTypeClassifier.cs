namespace MathInsight.Modules.TestGen.Generation;

public static class SharedBlueprintExamGenerationTypeClassifier
{
    public static bool TryClassify(IEnumerable<string?> selectionReasons, out string generationType)
    {
        var reasons = selectionReasons.ToList();
        if (reasons.Count == 0 || reasons.Any(reason =>
                !string.Equals(reason, GeneratedTestValues.FixedExamReason, StringComparison.Ordinal) &&
                !string.Equals(reason, GeneratedTestValues.BlueprintNormalReason, StringComparison.Ordinal)))
        {
            generationType = string.Empty;
            return false;
        }

        var isFixed = reasons.All(reason => string.Equals(
            reason,
            GeneratedTestValues.FixedExamReason,
            StringComparison.Ordinal));
        var isRandom = reasons.All(reason => string.Equals(
            reason,
            GeneratedTestValues.BlueprintNormalReason,
            StringComparison.Ordinal));
        generationType = isFixed
            ? GeneratedTestValues.FixedGenerationType
            : isRandom
                ? GeneratedTestValues.RandomGenerationType
                : string.Empty;
        return generationType.Length > 0;
    }
}
