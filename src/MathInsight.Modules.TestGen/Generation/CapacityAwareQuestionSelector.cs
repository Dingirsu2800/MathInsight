namespace MathInsight.Modules.TestGen.Generation;

public sealed class CapacityAwareQuestionSelector : IBlueprintExamQuestionSelector
{
    private readonly IGenerationRandomizer _randomizer;

    public CapacityAwareQuestionSelector(IGenerationRandomizer randomizer)
    {
        _randomizer = randomizer;
    }

    public BlueprintExamSelection Select(
        IReadOnlyList<BlueprintExamRequirement> requirements,
        IReadOnlyList<BlueprintExamCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var requiredCount = requirements.Sum(requirement => requirement.Quantity);
        if (requiredCount <= 0 || candidates.Count < requiredCount)
            return new BlueprintExamSelection(false, Array.Empty<BlueprintExamAssignment>());

        var shuffledCandidates = candidates.ToList();
        _randomizer.Shuffle(shuffledCandidates);

        var source = 0;
        var firstCandidateNode = 1;
        var firstRequirementNode = firstCandidateNode + shuffledCandidates.Count;
        var sink = firstRequirementNode + requirements.Count;
        var network = new FlowNetwork(sink + 1);
        var assignmentEdges = new List<AssignmentEdge>();

        for (var candidateIndex = 0; candidateIndex < shuffledCandidates.Count; candidateIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidateNode = firstCandidateNode + candidateIndex;
            network.AddEdge(source, candidateNode, 1);

            var matchingRequirements = Enumerable.Range(0, requirements.Count)
                .Where(requirementIndex => Matches(
                    shuffledCandidates[candidateIndex],
                    requirements[requirementIndex]))
                .ToList();
            _randomizer.Shuffle(matchingRequirements);

            foreach (var requirementIndex in matchingRequirements)
            {
                var edge = network.AddEdge(
                    candidateNode,
                    firstRequirementNode + requirementIndex,
                    1);
                assignmentEdges.Add(new AssignmentEdge(candidateIndex, requirementIndex, edge));
            }
        }

        for (var requirementIndex = 0; requirementIndex < requirements.Count; requirementIndex++)
        {
            network.AddEdge(
                firstRequirementNode + requirementIndex,
                sink,
                requirements[requirementIndex].Quantity);
        }

        var assignedCount = network.GetMaximumFlow(source, sink, cancellationToken);
        if (assignedCount != requiredCount)
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

    private static bool Matches(
        BlueprintExamCandidate candidate,
        BlueprintExamRequirement requirement)
        => string.Equals(candidate.DifficultyId, requirement.DifficultyId, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(candidate.QuestionType, requirement.QuestionType, StringComparison.OrdinalIgnoreCase) &&
           candidate.SupportedScoringRules.Contains(requirement.ScoringRule) &&
           candidate.TagIds.Contains(requirement.TagId);

    private sealed record AssignmentEdge(
        int CandidateIndex,
        int RequirementIndex,
        FlowEdge Edge);

    private sealed class FlowNetwork
    {
        private readonly List<FlowEdge>[] _graph;
        private int[] _levels = Array.Empty<int>();
        private int[] _nextEdges = Array.Empty<int>();

        public FlowNetwork(int nodeCount)
        {
            _graph = Enumerable.Range(0, nodeCount)
                .Select(_ => new List<FlowEdge>())
                .ToArray();
        }

        public FlowEdge AddEdge(int from, int to, int capacity)
        {
            var forward = new FlowEdge(to, _graph[to].Count, capacity);
            var reverse = new FlowEdge(from, _graph[from].Count, 0);
            _graph[from].Add(forward);
            _graph[to].Add(reverse);
            return forward;
        }

        public int GetMaximumFlow(int source, int sink, CancellationToken cancellationToken)
        {
            var flow = 0;
            while (BuildLevels(source, sink, cancellationToken))
            {
                _nextEdges = new int[_graph.Length];
                int pushed;
                while ((pushed = PushFlow(source, sink, int.MaxValue, cancellationToken)) > 0)
                    flow += pushed;
            }

            return flow;
        }

        private bool BuildLevels(int source, int sink, CancellationToken cancellationToken)
        {
            _levels = Enumerable.Repeat(-1, _graph.Length).ToArray();
            var queue = new Queue<int>();
            _levels[source] = 0;
            queue.Enqueue(source);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var node = queue.Dequeue();
                foreach (var edge in _graph[node])
                {
                    if (edge.Capacity <= 0 || _levels[edge.To] >= 0)
                        continue;

                    _levels[edge.To] = _levels[node] + 1;
                    queue.Enqueue(edge.To);
                }
            }

            return _levels[sink] >= 0;
        }

        private int PushFlow(
            int node,
            int sink,
            int available,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node == sink)
                return available;

            for (; _nextEdges[node] < _graph[node].Count; _nextEdges[node]++)
            {
                var edge = _graph[node][_nextEdges[node]];
                if (edge.Capacity <= 0 || _levels[edge.To] != _levels[node] + 1)
                    continue;

                var pushed = PushFlow(
                    edge.To,
                    sink,
                    Math.Min(available, edge.Capacity),
                    cancellationToken);
                if (pushed <= 0)
                    continue;

                edge.Capacity -= pushed;
                _graph[edge.To][edge.ReverseIndex].Capacity += pushed;
                return pushed;
            }

            return 0;
        }
    }

    private sealed class FlowEdge
    {
        public FlowEdge(int to, int reverseIndex, int capacity)
        {
            To = to;
            ReverseIndex = reverseIndex;
            Capacity = capacity;
        }

        public int To { get; }
        public int ReverseIndex { get; }
        public int Capacity { get; set; }
    }
}
