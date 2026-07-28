namespace MathInsight.Shared.Scoring;

public static class ScoringRules
{
    public const string AllOrNothing = "AllOrNothing";
    public const string TieredTrueFalse = "TieredTrueFalse";
    public const string WeightedParts = "WeightedParts";

    public static bool IsSupported(string? value) => value is
        AllOrNothing or TieredTrueFalse or WeightedParts;
}

public static class ScoringPolicies
{
    public const string BlueprintBudget = "BlueprintBudget";
    public const string NormalizedWeight = "NormalizedWeight";
}

public sealed record WeightedScoreItem(string Id, decimal Weight, int StableOrder);

public static class ScoringAllocator
{
    public static IReadOnlyDictionary<string, decimal> Allocate(
        decimal totalPoints,
        IReadOnlyCollection<WeightedScoreItem> items)
    {
        if (totalPoints <= 0m)
            throw new ArgumentOutOfRangeException(nameof(totalPoints));
        if (items.Count == 0 || items.Any(item => item.Weight <= 0m))
            throw new ArgumentException("At least one positive weighted item is required.", nameof(items));
        if (items.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
            throw new ArgumentException("Weighted item identifiers must be unique.", nameof(items));

        var totalCents = decimal.ToInt32(decimal.Round(totalPoints * 100m, 0, MidpointRounding.AwayFromZero));
        var totalWeight = items.Sum(item => item.Weight);
        var allocations = items
            .Select(item =>
            {
                var exactCents = totalCents * item.Weight / totalWeight;
                var floorCents = decimal.ToInt32(decimal.Floor(exactCents));
                return new Allocation(item, floorCents, exactCents - floorCents);
            })
            .ToList();

        var remaining = totalCents - allocations.Sum(item => item.Cents);
        foreach (var allocation in allocations
                     .OrderByDescending(item => item.Remainder)
                     .ThenBy(item => item.Item.StableOrder)
                     .Take(remaining))
        {
            allocation.Cents++;
        }

        return allocations.ToDictionary(
            item => item.Item.Id,
            item => item.Cents / 100m,
            StringComparer.OrdinalIgnoreCase);
    }

    private sealed class Allocation(WeightedScoreItem item, int cents, decimal remainder)
    {
        public WeightedScoreItem Item { get; } = item;
        public int Cents { get; set; } = cents;
        public decimal Remainder { get; } = remainder;
    }
}
