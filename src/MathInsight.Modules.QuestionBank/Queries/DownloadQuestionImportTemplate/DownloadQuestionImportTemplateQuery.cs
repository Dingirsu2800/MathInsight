using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.DownloadQuestionImportTemplate;

public sealed record DownloadQuestionImportTemplateQuery : IRequest<Result<QuestionImportTemplateResponse>>;
