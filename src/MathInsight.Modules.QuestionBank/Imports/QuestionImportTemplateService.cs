using ClosedXML.Excel;
using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Imports;

public interface IQuestionImportTemplateService
{
    Task<QuestionImportTemplateResponse> CreateAsync(CancellationToken cancellationToken);
}

public sealed class QuestionImportTemplateService : IQuestionImportTemplateService
{
    private readonly QuestionBankDbContext _context;

    public QuestionImportTemplateService(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<QuestionImportTemplateResponse> CreateAsync(CancellationToken cancellationToken)
    {
        var topics = await _context.TagTopics
            .AsNoTracking()
            .Where(topic => topic.IsActive)
            .OrderBy(topic => topic.Grade)
            .ThenBy(topic => topic.DisplayOrder)
            .Select(topic => new { topic.TagName, topic.Grade })
            .ToListAsync(cancellationToken);
        var difficulties = await _context.TagDifficulties
            .AsNoTracking()
            .Where(difficulty => difficulty.IsActive)
            .OrderBy(difficulty => difficulty.LevelValue)
            .Select(difficulty => new { difficulty.LevelValue, difficulty.DifficultyName })
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        AddMeta(workbook);
        AddInstructions(workbook);
        AddInputSheet(workbook, "Questions", QuestionImportConstants.QuestionHeaders);
        AddInputSheet(workbook, "Answers", QuestionImportConstants.AnswerHeaders);
        AddInputSheet(workbook, "Parts", QuestionImportConstants.PartHeaders);
        AddInputSheet(workbook, "Topics", QuestionImportConstants.TopicHeaders);
        AddCatalogs(workbook, topics, difficulties);

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new QuestionImportTemplateResponse(
            stream.ToArray(),
            "MathInsight_Question_Import_v1.xlsx",
            QuestionImportConstants.ExcelContentType);
    }

    private static void AddMeta(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("_Meta");
        worksheet.Cell(1, 1).Value = "TemplateVersion";
        worksheet.Cell(1, 2).Value = QuestionImportConstants.TemplateVersion;
        worksheet.Hide();
    }

    private static void AddInstructions(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("Instructions");
        worksheet.Cell(1, 1).Value = "MathInsight Excel import template v1";
        worksheet.Cell(3, 1).Value = "Use QuestionKey to connect Questions, Answers, Parts, and Topics.";
        worksheet.Cell(4, 1).Value = "Use only the documented enum values. Do not use Excel formulas in input sheets.";
        worksheet.Cell(5, 1).Value = "COMPOSITE questions use Parts and must not have Answers.";
        worksheet.Cell(6, 1).Value = "Questions use active topic names and active difficulty level values from Catalogs.";
        worksheet.Column(1).Width = 120;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
    }

    private static void AddInputSheet(XLWorkbook workbook, string name, IReadOnlyList<string> headers)
    {
        var worksheet = workbook.Worksheets.Add(name);
        for (var index = 0; index < headers.Count; index++)
            worksheet.Cell(1, index + 1).Value = headers[index];

        var headerRange = worksheet.Range(1, 1, 1, headers.Count);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().Width = 18;
        worksheet.Column(2).Width = 42;
    }

    private static void AddCatalogs(
        XLWorkbook workbook,
        IReadOnlyList<dynamic> topics,
        IReadOnlyList<dynamic> difficulties)
    {
        var worksheet = workbook.Worksheets.Add("Catalogs");
        worksheet.Cell(1, 1).Value = "TopicName";
        worksheet.Cell(1, 2).Value = "Grade";
        worksheet.Cell(1, 4).Value = "DifficultyLevel";
        worksheet.Cell(1, 5).Value = "DifficultyName";
        worksheet.Range(1, 1, 1, 5).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 5).Style.Fill.BackgroundColor = XLColor.LightBlue;

        for (var index = 0; index < topics.Count; index++)
        {
            worksheet.Cell(index + 2, 1).Value = topics[index].TagName;
            worksheet.Cell(index + 2, 2).Value = topics[index].Grade;
        }

        for (var index = 0; index < difficulties.Count; index++)
        {
            worksheet.Cell(index + 2, 4).Value = difficulties[index].LevelValue;
            worksheet.Cell(index + 2, 5).Value = difficulties[index].DifficultyName;
        }

        worksheet.Columns().AdjustToContents();
    }
}
