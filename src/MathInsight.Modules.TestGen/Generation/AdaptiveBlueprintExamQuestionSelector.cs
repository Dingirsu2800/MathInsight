namespace MathInsight.Modules.TestGen.Generation;

public sealed class AdaptiveBlueprintExamQuestionSelector : IAdaptiveBlueprintExamQuestionSelector
{
    private readonly IGenerationRandomizer _randomizer;

    public AdaptiveBlueprintExamQuestionSelector(IGenerationRandomizer randomizer)
    {
        _randomizer = randomizer;
    }

    public BlueprintExamSelection Select(
        IReadOnlyList<BlueprintExamRequirement> requirements,
        IReadOnlyDictionary<string, AdaptiveBlueprintDetailPlan> plansByDetailId,
        IReadOnlyList<BlueprintExamCandidate> candidates,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requiredCount = requirements.Sum(requirement => requirement.Quantity);
        if (requiredCount <= 0)
            return new BlueprintExamSelection(false, Array.Empty<BlueprintExamAssignment>());

        var shuffledCandidates = candidates
            .GroupBy(candidate => candidate.QuestionId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        _randomizer.Shuffle(shuffledCandidates);

        var source = 0;
        var firstCandidateNode = 1;
        var firstRequirementNode = firstCandidateNode + shuffledCandidates.Count;
        var sink = firstRequirementNode + requirements.Count;
        var network = new MinCostFlowNetwork(sink + 1);
        var assignmentEdges = new List<AssignmentEdge>();

        for (var candidateIndex = 0; candidateIndex < shuffledCandidates.Count; candidateIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidateNode = firstCandidateNode + candidateIndex;
            network.AddEdge(source, candidateNode, 1, 0);

            var matchingRequirements = Enumerable.Range(0, requirements.Count)
                .Where(requirementIndex => TryGetCost(
                    shuffledCandidates[candidateIndex],
                    requirements[requirementIndex],
                    plansByDetailId,
                    out _))
                .ToList();
            _randomizer.Shuffle(matchingRequirements);

            foreach (var requirementIndex in matchingRequirements)
            {
                var requirement = requirements[requirementIndex];
                _ = TryGetCost(
                    shuffledCandidates[candidateIndex],
                    requirement,
                    plansByDetailId,
                    out var cost);
                var edge = network.AddEdge(
                    candidateNode,
                    firstRequirementNode + requirementIndex,
                    1,
                    cost);
                assignmentEdges.Add(new AssignmentEdge(candidateIndex, requirementIndex, edge));
            }
        }

        for (var requirementIndex = 0; requirementIndex < requirements.Count; requirementIndex++)
        {
            network.AddEdge(
                firstRequirementNode + requirementIndex,
                sink,
                requirements[requirementIndex].Quantity,
                0);
        }

        var flow = network.GetMinCostMaxFlow(source, sink, cancellationToken);
        if (flow.Flow != requiredCount)
            return new BlueprintExamSelection(false, Array.Empty<BlueprintExamAssignment>());

        var assignments = assignmentEdges
            .Where(item => item.Edge.Capacity == 0)
            .Select(item =>
            {
                var requirement = requirements[item.RequirementIndex];
                return new BlueprintExamAssignment(
                    shuffledCandidates[item.CandidateIndex].QuestionId,
                    requirement.BlueprintDetailId,
                    requirement.SectionOrder,
                    requirement.DetailOrder,
                    item.CandidateIndex);
            })
            .OrderBy(item => item.SectionOrder)
            .ThenBy(item => item.DetailOrder)
            .ThenBy(item => item.CandidateOrder)
            .ToList();

        return new BlueprintExamSelection(true, assignments);
    }

    private static bool TryGetCost(
        BlueprintExamCandidate candidate,
        BlueprintExamRequirement requirement,
        IReadOnlyDictionary<string, AdaptiveBlueprintDetailPlan> plansByDetailId,
        out int cost)
    {
        cost = 0;
        if (!plansByDetailId.TryGetValue(requirement.BlueprintDetailId, out var plan) ||
            !MatchesShape(candidate, requirement))
        {
            return false;
        }

        if (string.Equals(candidate.DifficultyId, plan.PreferredDifficultyId, StringComparison.OrdinalIgnoreCase))
        {
            cost = 0;
            return true;
        }

        if (string.Equals(candidate.DifficultyId, plan.OriginalDifficultyId, StringComparison.OrdinalIgnoreCase))
        {
            cost = 1;
            return true;
        }

        return false;
    }

    private static bool MatchesShape(
        BlueprintExamCandidate candidate,
        BlueprintExamRequirement requirement)
        => string.Equals(candidate.QuestionType, requirement.QuestionType, StringComparison.OrdinalIgnoreCase) &&
           candidate.SupportedScoringRules.Contains(requirement.ScoringRule) &&
           (requirement.PartCountPerQuestion is null || candidate.PartCount == requirement.PartCountPerQuestion) &&
           candidate.TagIds.Contains(requirement.TagId);

    private sealed record AssignmentEdge(
        int CandidateIndex,
        int RequirementIndex,
        FlowEdge Edge);

    private sealed class MinCostFlowNetwork
    {
        private readonly List<FlowEdge>[] _graph;

        public MinCostFlowNetwork(int nodeCount)
        {
            _graph = Enumerable.Range(0, nodeCount)
                .Select(_ => new List<FlowEdge>())
                .ToArray();
        }

        public FlowEdge AddEdge(int from, int to, int capacity, int cost)
        {
            var forward = new FlowEdge(to, _graph[to].Count, capacity, cost);
            var reverse = new FlowEdge(from, _graph[from].Count, 0, -cost);
            _graph[from].Add(forward);
            _graph[to].Add(reverse);
            return forward;
        }

        public (int Flow, int Cost) GetMinCostMaxFlow(
            int source,
            int sink,
            CancellationToken cancellationToken)
        {
            var flow = 0;
            var cost = 0;
            while (TryFindShortestPath(source, sink, cancellationToken, out var distances, out var previousNodes, out var previousEdges))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pushed = int.MaxValue;
                for (var node = sink; node != source; node = previousNodes[node])
                {
                    if (node < 0)
                    {
                        pushed = 0;
                        break;
                    }

                    pushed = Math.Min(pushed, _graph[previousNodes[node]][previousEdges[node]].Capacity);
                }

                if (pushed <= 0)
                    break;

                for (var node = sink; node != source; node = previousNodes[node])
                {
                    var edge = _graph[previousNodes[node]][previousEdges[node]];
                    edge.Capacity -= pushed;
                    _graph[node][edge.ReverseIndex].Capacity += pushed;
                }

                flow += pushed;
                cost += distances[sink] * pushed;
            }

            return (flow, cost);
        }

        private bool TryFindShortestPath(
            int source,
            int sink,
            CancellationToken cancellationToken,
            out int[] distances,
            out int[] previousNodes,
            out int[] previousEdges)
        {
            distances = Enumerable.Repeat(int.MaxValue, _graph.Length).ToArray();
            previousNodes = Enumerable.Repeat(-1, _graph.Length).ToArray();
            previousEdges = Enumerable.Repeat(-1, _graph.Length).ToArray();
            distances[source] = 0;

            for (var iteration = 0; iteration < _graph.Length - 1; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var changed = false;
                for (var node = 0; node < _graph.Length; node++)
                {
                    if (distances[node] == int.MaxValue)
                        continue;

                    for (var edgeIndex = 0; edgeIndex < _graph[node].Count; edgeIndex++)
                    {
                        var edge = _graph[node][edgeIndex];
                        if (edge.Capacity <= 0)
                            continue;

                        var nextDistance = distances[node] + edge.Cost;
                        if (nextDistance >= distances[edge.To])
                            continue;

                        distances[edge.To] = nextDistance;
                        previousNodes[edge.To] = node;
                        previousEdges[edge.To] = edgeIndex;
                        changed = true;
                    }
                }

                if (!changed)
                    break;
            }

            return distances[sink] != int.MaxValue;
        }
    }

    private sealed class FlowEdge
    {
        public FlowEdge(int to, int reverseIndex, int capacity, int cost)
        {
            To = to;
            ReverseIndex = reverseIndex;
            Capacity = capacity;
            Cost = cost;
        }

        public int To { get; }
        public int ReverseIndex { get; }
        public int Capacity { get; set; }
        public int Cost { get; }
    }
}
