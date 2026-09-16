namespace MathInsight.Modules.TestGen.Generation;

public static class BlueprintExamCandidateMatcher
{
    public static bool Matches(
        BlueprintExamCandidate candidate,
        BlueprintExamRequirement requirement)
        => string.Equals(candidate.DifficultyId, requirement.DifficultyId, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(candidate.QuestionType, requirement.QuestionType, StringComparison.OrdinalIgnoreCase) &&
           candidate.SupportedScoringRules.Contains(requirement.ScoringRule) &&
           candidate.TagIds.Contains(requirement.TagId);
}
