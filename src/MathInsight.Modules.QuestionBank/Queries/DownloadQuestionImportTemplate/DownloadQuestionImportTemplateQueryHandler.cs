using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Modules.QuestionBank.Imports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.DownloadQuestionImportTemplate;

public sealed class DownloadQuestionImportTemplateQueryHandler
    : IRequestHandler<DownloadQuestionImportTemplateQuery, Result<QuestionImportTemplateResponse>>
{
    private readonly IQuestionImportTemplateService _templateService;

    public DownloadQuestionImportTemplateQueryHandler(IQuestionImportTemplateService templateService)
    {
        _templateService = templateService;
    }

    public async Task<Result<QuestionImportTemplateResponse>> Handle(
        DownloadQuestionImportTemplateQuery request,
        CancellationToken cancellationToken) =>
        Result<QuestionImportTemplateResponse>.Success(
            await _templateService.CreateAsync(cancellationToken));
}
