namespace MathInsight.Modules.TestGen.Blueprints;

public static class BlueprintQuestionTypes
{
    public const string SingleChoice = "SingleChoice";
    public const string MultipleChoice = "MultipleChoice";
    public const string TrueFalse = "TrueFalse";
    public const string ShortAnswer = "ShortAnswer";
    public const string Composite = "Composite";

    public static string? Normalize(string? questionType)
    {
        if (string.IsNullOrWhiteSpace(questionType))
            return null;

        return questionType.Trim().ToUpperInvariant() switch
        {
            "SINGLECHOICE" or "SINGLE_CHOICE" => SingleChoice,
            "MULTIPLECHOICE" or "MULTIPLE_CHOICE" or "MULTIPLE_SELECT" => MultipleChoice,
            "TRUEFALSE" or "TRUE_FALSE" => TrueFalse,
            "SHORTANSWER" or "SHORT_ANSWER" => ShortAnswer,
            "COMPOSITE" => Composite,
            _ => string.Empty
        };
    }
}
