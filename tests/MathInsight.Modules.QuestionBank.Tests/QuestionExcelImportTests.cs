using ClosedXML.Excel;
using MathInsight.Modules.QuestionBank.Commands.ConfirmQuestionImport;
using MathInsight.Modules.QuestionBank.Commands.PreviewQuestionImport;
using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Imports;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class QuestionExcelImportTests
{
    [Fact]
    public async Task Preview_ValidSingleChoice_ReturnsNormalizedDraftWithoutWritingDatabase()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await SeedTaxonomyAsync(database);
        var handler = new PreviewQuestionImportCommandHandler(
            new QuestionImportWorkbookParser(),
            new QuestionImportValidationService(database.Context));

        var result = await handler.Handle(
            new PreviewQuestionImportCommand(CreateWorkbookFile(BuildValidWorkbook())),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.True(item.IsValid);
        Assert.NotNull(item.Draft);
        Assert.Equal("difficulty-1", item.Draft!.DifficultyId);
        Assert.Equal("topic-1", Assert.Single(item.Draft.Topics).TagId);
        Assert.Equal(0, await database.Context.Questions.CountAsync());
    }

    [Fact]
    public async Task Preview_FormulaInInputCell_ReturnsRowError()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await SeedTaxonomyAsync(database);
        using var workbook = BuildValidWorkbook();
        workbook.Worksheet("Questions").Cell(2, 2).FormulaA1 = "1+1";
        var handler = new PreviewQuestionImportCommandHandler(
            new QuestionImportWorkbookParser(),
            new QuestionImportValidationService(database.Context));

        var result = await handler.Handle(
            new PreviewQuestionImportCommand(CreateWorkbookFile(workbook)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.False(item.IsValid);
        Assert.Contains(item.Errors, error => error.Column == "QuestionContent");
    }

    [Fact]
    public async Task Confirm_ValidBatch_CreatesQuestionGraph()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await SeedTaxonomyAsync(database);
        var draft = CreateSingleChoiceDraft();
        var handler = new ConfirmQuestionImportCommandHandler(
            database.Context,
            new QuestionImportValidationService(database.Context));

        var result = await handler.Handle(new ConfirmQuestionImportCommand(
            new ConfirmQuestionImportRequest
            {
                ImportId = "import-1",
                Items = [new ConfirmQuestionImportItemRequest { QuestionKey = "Q001", Draft = draft }]
            },
            "expert-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsValid);
        Assert.Equal(1, result.Value.CreatedCount);
        var question = await database.Context.Questions
            .Include(item => item.Answers)
            .Include(item => item.QuestionTopics)
            .SingleAsync();
        Assert.Equal(2, question.Answers.Count);
        Assert.Single(question.QuestionTopics);
    }

    [Fact]
    public async Task Confirm_InactiveTopic_ReturnsValidationErrorsWithoutWrites()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await SeedTaxonomyAsync(database, topicIsActive: false);
        var handler = new ConfirmQuestionImportCommandHandler(
            database.Context,
            new QuestionImportValidationService(database.Context));

        var result = await handler.Handle(new ConfirmQuestionImportCommand(
            new ConfirmQuestionImportRequest
            {
                ImportId = "import-2",
                Items = [new ConfirmQuestionImportItemRequest { QuestionKey = "Q001", Draft = CreateSingleChoiceDraft() }]
            },
            "expert-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsValid);
        Assert.Equal("QUESTION_IMPORT_VALIDATION_FAILED", result.Value.Code);
        Assert.Equal(0, await database.Context.Questions.CountAsync());
    }

    [Fact]
    public async Task Template_ContainsRequiredSheetsAndVersion()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await SeedTaxonomyAsync(database);
        var service = new QuestionImportTemplateService(database.Context);

        var template = await service.CreateAsync(CancellationToken.None);
        await using var stream = new MemoryStream(template.Content);
        using var workbook = new XLWorkbook(stream);

        Assert.True(workbook.Worksheets.Contains("_Meta"));
        Assert.True(workbook.Worksheets.Contains("Instructions"));
        Assert.True(workbook.Worksheets.Contains("Questions"));
        Assert.True(workbook.Worksheets.Contains("Answers"));
        Assert.True(workbook.Worksheets.Contains("Parts"));
        Assert.True(workbook.Worksheets.Contains("Topics"));
        Assert.True(workbook.Worksheets.Contains("Catalogs"));
        Assert.Equal("1", workbook.Worksheet("_Meta").Cell(1, 2).GetString());
    }

    private static async Task SeedTaxonomyAsync(QuestionBankInMemoryContext database, bool topicIsActive = true)
    {
        database.Context.TagDifficulties.Add(new TagDifficulty
        {
            DifficultyId = "difficulty-1",
            DifficultyName = "Easy",
            LevelValue = 1,
            DisplayOrder = 1,
            IsActive = true
        });
        database.Context.TagTopics.Add(new TagTopic
        {
            TagId = "topic-1",
            TagName = "Functions",
            Grade = 10,
            DisplayOrder = 1,
            IsActive = topicIsActive
        });
        await database.Context.SaveChangesAsync();
    }

    private static CreateQuestionRequest CreateSingleChoiceDraft() => new()
    {
        QuestionContent = "What is 1 + 1?",
        SolutionContent = "2",
        DifficultyId = "difficulty-1",
        Grade = 10,
        QuestionType = "SINGLE_CHOICE",
        DefaultPoint = 1m,
        Topics = [new CreateQuestionTopicRequest("topic-1", true)],
        Answers =
        [
            new CreateAnswerRequest { AnswerContent = "2", IsCorrect = true },
            new CreateAnswerRequest { AnswerContent = "3", IsCorrect = false }
        ]
    };

    private static XLWorkbook BuildValidWorkbook()
    {
        var workbook = new XLWorkbook();
        workbook.Worksheets.Add("_Meta").Cell(1, 1).Value = "TemplateVersion";
        workbook.Worksheet("_Meta").Cell(1, 2).Value = "1";
        workbook.Worksheets.Add("Instructions");

        AddSheet(workbook, "Questions", ["QuestionKey", "QuestionContent", "SolutionContent", "QuestionType", "Grade", "DifficultyLevel", "DefaultPoint", "PictureUrl"]);
        var questions = workbook.Worksheet("Questions");
        questions.Cell(2, 1).Value = "Q001";
        questions.Cell(2, 2).Value = "What is 1 + 1?";
        questions.Cell(2, 3).Value = "2";
        questions.Cell(2, 4).Value = "SINGLE_CHOICE";
        questions.Cell(2, 5).Value = 10;
        questions.Cell(2, 6).Value = 1;
        questions.Cell(2, 7).Value = 1;

        AddSheet(workbook, "Answers", ["QuestionKey", "AnswerContent", "IsCorrect"]);
        var answers = workbook.Worksheet("Answers");
        answers.Cell(2, 1).Value = "Q001";
        answers.Cell(2, 2).Value = "2";
        answers.Cell(2, 3).Value = true;
        answers.Cell(3, 1).Value = "Q001";
        answers.Cell(3, 2).Value = "3";
        answers.Cell(3, 3).Value = false;

        AddSheet(workbook, "Parts", ["QuestionKey", "PartOrder", "PartLabel", "PartContent", "PartType", "CorrectBoolean", "CorrectText", "CorrectNumeric", "NumericTolerance", "Explanation", "DefaultPoint"]);
        AddSheet(workbook, "Topics", ["QuestionKey", "TopicName", "IsPrimary"]);
        var topics = workbook.Worksheet("Topics");
        topics.Cell(2, 1).Value = "Q001";
        topics.Cell(2, 2).Value = "Functions";
        topics.Cell(2, 3).Value = true;

        workbook.Worksheets.Add("Catalogs");
        return workbook;
    }

    private static void AddSheet(XLWorkbook workbook, string name, IReadOnlyList<string> headers)
    {
        var worksheet = workbook.Worksheets.Add(name);
        for (var index = 0; index < headers.Count; index++)
            worksheet.Cell(1, index + 1).Value = headers[index];
    }

    private static IFormFile CreateWorkbookFile(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "questions.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }
}
