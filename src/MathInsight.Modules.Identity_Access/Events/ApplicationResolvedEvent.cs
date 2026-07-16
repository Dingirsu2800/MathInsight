using MediatR;

namespace MathInsight.Modules.Identity_Access.Events;

public sealed record ApplicationResolvedEvent(
    string ApplicationId,
    string TeacherId,
    bool Approved,
    string? ReviewComments) : INotification;
