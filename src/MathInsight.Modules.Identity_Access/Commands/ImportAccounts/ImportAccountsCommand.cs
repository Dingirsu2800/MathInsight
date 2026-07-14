using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.ImportAccounts;

public sealed record ImportAccountsCommand(byte[] FileContent)
    : IRequest<Result<ImportAccountsResponse>>;
