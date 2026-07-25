using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public record ResolveModerationCommand(string ReportId, string ResolverAccountId, bool IsDismissed) : IRequest<bool>;
