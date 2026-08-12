using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Events;

public record DiscussionReportedEvent(
    string ReportId,
    string TeacherId,
    string LectureId,
    string TargetType,
    string ReporterAccountId,
    string Reason
) : INotification;
