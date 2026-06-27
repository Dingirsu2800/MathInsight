using MathInsight.Modules.Identity_Access.Entities;

namespace MathInsight.Modules.Identity_Access.Services;

public interface ITokenService
{
    string CreateAccessToken(Account account, out DateTime expiresAt);
}

