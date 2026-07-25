export function getBlueprintActions(blueprint, currentAccountId) {
  if (!blueprint) return {};

  const isOwner =
    blueprint.expertId &&
    currentAccountId &&
    String(blueprint.expertId).toLowerCase() === String(currentAccountId).toLowerCase();

  const status = blueprint.status; // Draft, PendingReview, Approved, Rejected, Active, Deactivated

  return {
    canEdit: isOwner && (status === "Draft" || status === "Rejected"),
    canSubmit: isOwner && (status === "Draft" || status === "Rejected"),
    canDelete: isOwner && (status === "Draft" || status === "Rejected" || status === "Approved"),
    canDeactivate: isOwner && status === "Active",
    canClone: status !== "Deactivated",
    canReview: !isOwner && status === "PendingReview"
  };
}
