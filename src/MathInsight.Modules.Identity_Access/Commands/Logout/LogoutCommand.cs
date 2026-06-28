using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.Logout;

public record LogoutCommand(string TokenId, DateTime ExpiresAtUtc) : IRequest;