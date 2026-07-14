using MediatR;

namespace MathInsight.Modules.Identity_Access.Events;

public sealed record AccountCreatedEvent(
    string AccountId,
    string Email,
    string FullName,
    string RoleName) : INotification;
