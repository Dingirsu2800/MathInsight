using MathInsight.Shared.Results;

namespace MathInsight.Modules.TestGen.Errors;

public static class BlueprintErrors
{
    public static readonly Error NotFound = new(
        "BLUEPRINT_NOT_FOUND",
        "Blueprint was not found.");

    public static readonly Error RequestInvalid = new(
        "BLUEPRINT_REQUEST_INVALID",
        "Blueprint request is invalid.");

    public static readonly Error MutationForbidden = new(
        "BLUEPRINT_MUTATION_FORBIDDEN",
        "You are not allowed to modify this blueprint.");

    public static readonly Error SelfReviewForbidden = new(
        "BLUEPRINT_SELF_REVIEW_FORBIDDEN",
        "You cannot review your own blueprint.");

    public static readonly Error StatusInvalid = new(
        "BLUEPRINT_STATUS_INVALID",
        "The action is invalid for the current blueprint status.");

    public static readonly Error StructureInvalid = new(
        "BLUEPRINT_STRUCTURE_INVALID",
        "Blueprint sections or details are invalid.");

    public static readonly Error TotalMismatch = new(
        "BLUEPRINT_TOTAL_MISMATCH",
        "Blueprint, section, or detail totals do not match.");

    public static readonly Error ReviewNoteRequired = new(
        "BLUEPRINT_REVIEW_NOTE_REQUIRED",
        "A review note is required when rejecting a blueprint.");

    public static readonly Error ReviewNoteTooLong = new(
        "BLUEPRINT_REVIEW_NOTE_TOO_LONG",
        "Review note must not exceed 2000 characters.");

    public static readonly Error TaxonomyInvalid = new(
        "BLUEPRINT_TAXONOMY_INVALID",
        "A topic or difficulty is missing, inactive, or invalid for the blueprint grade.");

    public static readonly Error InUse = new(
        "BLUEPRINT_IN_USE",
        "Blueprint is pending review or has historical references.");
}
