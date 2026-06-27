using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.Login;

public record LoginCommand(string UsernameOrEmail, string Password) : IRequest<LoginResponse?>;

