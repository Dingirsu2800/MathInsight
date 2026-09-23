using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.TestGen.Blueprints;

public static class BlueprintPolicies
{
    public static string? NormalizeScoringRule(string? value)
        => value?.Trim().ToUpperInvariant() switch
        {
            "ALLORNOTHING" => ScoringRules.AllOrNothing,
            "TIEREDTRUEFALSE" => ScoringRules.TieredTrueFalse,
            "WEIGHTEDPARTS" => ScoringRules.WeightedParts,
            _ => null
        };

    public static bool IsValidPair(string? questionType, string? scoringRule)
        => questionType switch
        {
            BlueprintQuestionTypes.SingleChoice or
            BlueprintQuestionTypes.MultipleChoice or
            BlueprintQuestionTypes.TrueFalse or
            BlueprintQuestionTypes.ShortAnswer => scoringRule == ScoringRules.AllOrNothing,
            BlueprintQuestionTypes.Composite => scoringRule is ScoringRules.TieredTrueFalse or ScoringRules.WeightedParts,
            _ => false
        };

    public static bool TryResolveEffectivePolicy(
        string sectionQuestionType,
        string? sectionScoringRule,
        string? detailQuestionType,
        string? detailScoringRule,
        out string effectiveQuestionType,
        out string effectiveScoringRule)
    {
        effectiveQuestionType = string.Empty;
        effectiveScoringRule = string.Empty;

        var normalizedSectionType = BlueprintQuestionTypes.Normalize(sectionQuestionType);
        var normalizedSectionRule = NormalizeScoringRule(sectionScoringRule);
        var normalizedDetailType = BlueprintQuestionTypes.Normalize(detailQuestionType);
        var normalizedDetailRule = NormalizeScoringRule(detailScoringRule);

        if (normalizedSectionType == BlueprintQuestionTypes.Mixed)
        {
            if (normalizedSectionRule is not null ||
                !BlueprintQuestionTypes.IsActualQuestionType(normalizedDetailType) ||
                !IsValidPair(normalizedDetailType, normalizedDetailRule))
            {
                return false;
            }

            effectiveQuestionType = normalizedDetailType!;
            effectiveScoringRule = normalizedDetailRule!;
            return true;
        }

        if (!BlueprintQuestionTypes.IsActualQuestionType(normalizedSectionType) ||
            normalizedDetailType is not null ||
            normalizedDetailRule is not null ||
            !IsValidPair(normalizedSectionType, normalizedSectionRule))
        {
            return false;
        }

        effectiveQuestionType = normalizedSectionType!;
        effectiveScoringRule = normalizedSectionRule!;
        return true;
    }

    public static bool TryResolveEffectivePolicy(
        BlueprintSection section,
        BlueprintDetail detail,
        out string effectiveQuestionType,
        out string effectiveScoringRule)
        => TryResolveEffectivePolicy(
            section.QuestionType,
            section.ScoringRule,
            detail.QuestionType,
            detail.ScoringRule,
            out effectiveQuestionType,
            out effectiveScoringRule);
}
