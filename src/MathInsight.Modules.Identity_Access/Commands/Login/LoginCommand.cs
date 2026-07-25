using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.Login;

public record LoginCommand(string UsernameOrEmail, string Password) : IRequest<Result<LoginResponse>>;

