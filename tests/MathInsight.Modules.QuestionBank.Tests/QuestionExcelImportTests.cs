using ClosedXML.Excel;
using System.IO.Compression;
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

    [Theory]
    [InlineData("MULTIPLE_SELECT")]
    [InlineData("TRUE_FALSE")]
    [InlineData("SHORT_ANSWER")]
    [InlineData("COMPOSITE")]
    public async Task Preview_ValidSupportedQuestionTypes_ReturnsNormalizedDraft(string questionType)
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await SeedTaxonomyAsync(database);
        var handler = new PreviewQuestionImportCommandHandler(
            new QuestionImportWorkbookParser(),
            new QuestionImportValidationService(database.Context));

        var result = await handler.Handle(
            new PreviewQuestionImportCommand(CreateWorkbookFile(BuildValidWorkbook(questionType))),
            CancellationToken.None);

        var item = Assert.Single(result.Value!.Items);
        Assert.True(result.IsSuccess);
        Assert.True(item.IsValid);
        Assert.NotNull(item.Draft);
        Assert.Equal(questionType, item.Draft!.QuestionType);
    }

    [Fact]
    public void Parser_UnsupportedTemplateVersion_ThrowsTemplateVersionError()
    {
        using var workbook = BuildValidWorkbook();
        workbook.Worksheet("_Meta").Cell(1, 2).Value = "2";
        using var stream = CreateWorkbookStream(workbook);

        var exception = Assert.Throws<QuestionImportException>(() => new QuestionImportWorkbookParser().Parse(stream));

        Assert.Equal("QUESTION_IMPORT_TEMPLATE_VERSION_UNSUPPORTED", exception.Error.Code);
    }

    [Fact]
    public void Parser_MissingRequiredSheet_ThrowsTemplateInvalidError()
    {
        using var workbook = BuildValidWorkbook();
        workbook.Worksheets.Delete("Topics");
        using var stream = CreateWorkbookStream(workbook);

        var exception = Assert.Throws<QuestionImportException>(() => new QuestionImportWorkbookParser().Parse(stream));

        Assert.Equal("QUESTION_IMPORT_TEMPLATE_INVALID", exception.Error.Code);
    }

    [Fact]
    public void Parser_InvalidRequiredHeader_ThrowsTemplateInvalidError()
    {
        using var workbook = BuildValidWorkbook();
        workbook.Worksheet("Questions").Cell(1, 2).Value = "Stem";
        using var stream = CreateWorkbookStream(workbook);

        var exception = Assert.Throws<QuestionImportException>(() => new QuestionImportWorkbookParser().Parse(stream));

        Assert.Equal("QUESTION_IMPORT_TEMPLATE_INVALID", exception.Error.Code);
    }

    [Fact]
    public void Parser_InputSheetExceedsRowLimit_ThrowsLimitExceededError()
    {
        using var workbook = BuildValidWorkbook();
        workbook.Worksheet("Questions").Cell(5002, 1).Value = "Q-LIMIT";
        using var stream = CreateWorkbookStream(workbook);

        var exception = Assert.Throws<QuestionImportException>(() => new QuestionImportWorkbookParser().Parse(stream));

        Assert.Equal("QUESTION_IMPORT_LIMIT_EXCEEDED", exception.Error.Code);
    }

    [Fact]
    public void Parser_ArchiveWithTooManyEntries_ThrowsLimitExceededError()
    {
        const int maxArchiveEntries = 200;
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var index = 0; index <= maxArchiveEntries; index++)
                archive.CreateEntry($"entry-{index}.xml");
        }

        stream.Position = 0;
        var exception = Assert.Throws<QuestionImportException>(() => new QuestionImportWorkbookParser().Parse(stream));

        Assert.Equal("QUESTION_IMPORT_LIMIT_EXCEEDED", exception.Error.Code);
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
    public async Task Confirm_InactiveDifficulty_ReturnsValidationErrorsWithoutWrites()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await SeedTaxonomyAsync(database, difficultyIsActive: false);
        var handler = new ConfirmQuestionImportCommandHandler(
            database.Context,
            new QuestionImportValidationService(database.Context));

        var result = await handler.Handle(new ConfirmQuestionImportCommand(
            new ConfirmQuestionImportRequest
            {
                ImportId = "import-inactive-difficulty",
                Items = [new ConfirmQuestionImportItemRequest { QuestionKey = "Q001", Draft = CreateSingleChoiceDraft() }]
            },
            "expert-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsValid);
        Assert.Equal(0, await database.Context.Questions.CountAsync());
    }

    [Fact]
    public async Task Confirm_TopicWithDifferentGrade_ReturnsValidationErrorsWithoutWrites()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await SeedTaxonomyAsync(database, topicGrade: 11);
        var handler = new ConfirmQuestionImportCommandHandler(
            database.Context,
            new QuestionImportValidationService(database.Context));

        var result = await handler.Handle(new ConfirmQuestionImportCommand(
            new ConfirmQuestionImportRequest
            {
                ImportId = "import-topic-grade-mismatch",
                Items = [new ConfirmQuestionImportItemRequest { QuestionKey = "Q001", Draft = CreateSingleChoiceDraft() }]
            },
            "expert-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsValid);
        Assert.Contains(result.Value.Errors, error => error.Code == "QUESTION_TOPIC_NOT_FOUND");
        Assert.Equal(0, await database.Context.Questions.CountAsync());
    }

    [Fact]
    public async Task Confirm_MoreThanOneHundredItems_ReturnsLimitError()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var handler = new ConfirmQuestionImportCommandHandler(
            database.Context,
            new QuestionImportValidationService(database.Context));

        var result = await handler.Handle(new ConfirmQuestionImportCommand(
            new ConfirmQuestionImportRequest
            {
                ImportId = "import-over-limit",
                Items = Enumerable.Range(1, 101)
                    .Select(index => new ConfirmQuestionImportItemRequest
                    {
                        QuestionKey = $"Q{index:000}",
                        Draft = CreateSingleChoiceDraft()
                    })
                    .ToList()
            },
            "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("QUESTION_IMPORT_LIMIT_EXCEEDED", result.Error!.Code);
        Assert.Equal(0, await database.Context.Questions.CountAsync());
    }

    [Fact]
    public async Task Confirm_TamperedDraft_ReturnsValidationErrorsWithoutWrites()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await SeedTaxonomyAsync(database);
        var draft = CreateSingleChoiceDraft();
        draft.Answers = [];
        var handler = new ConfirmQuestionImportCommandHandler(
            database.Context,
            new QuestionImportValidationService(database.Context));

        var result = await handler.Handle(new ConfirmQuestionImportCommand(
            new ConfirmQuestionImportRequest
            {
                ImportId = "import-tampered",
                Items = [new ConfirmQuestionImportItemRequest { QuestionKey = "Q001", Draft = draft }]
            },
            "expert-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsValid);
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

    private static async Task SeedTaxonomyAsync(
        QuestionBankInMemoryContext database,
        bool topicIsActive = true,
        bool difficultyIsActive = true,
        int topicGrade = 10)
    {
        database.Context.TagDifficulties.Add(new TagDifficulty
        {
            DifficultyId = "difficulty-1",
            DifficultyName = "Easy",
            LevelValue = 1,
            DisplayOrder = 1,
            IsActive = difficultyIsActive
        });
        database.Context.TagTopics.Add(new TagTopic
        {
            TagId = "topic-1",
            TagName = "Functions",
            Grade = topicGrade,
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

    private static XLWorkbook BuildValidWorkbook(string questionType = "SINGLE_CHOICE")
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
        questions.Cell(2, 4).Value = questionType;
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

        ConfigureQuestionType(workbook, questionType);
        workbook.Worksheets.Add("Catalogs");
        return workbook;
    }

    private static void ConfigureQuestionType(XLWorkbook workbook, string questionType)
    {
        var answers = workbook.Worksheet("Answers");
        if (questionType == "MULTIPLE_SELECT")
        {
            answers.Cell(3, 3).Value = true;
            return;
        }

        if (questionType is "SINGLE_CHOICE" or "TRUE_FALSE")
            return;

        answers.Range("A2:C3").Clear(XLClearOptions.Contents);
        if (questionType == "SHORT_ANSWER")
        {
            answers.Cell(2, 1).Value = "Q001";
            answers.Cell(2, 2).Value = "2";
            answers.Cell(2, 3).Value = true;
            return;
        }

        if (questionType == "COMPOSITE")
        {
            var parts = workbook.Worksheet("Parts");
            parts.Cell(2, 1).Value = "Q001";
            parts.Cell(2, 2).Value = 1;
            parts.Cell(2, 3).Value = "a";
            parts.Cell(2, 4).Value = "Statement";
            parts.Cell(2, 5).Value = "TRUE_FALSE";
            parts.Cell(2, 6).Value = true;
            parts.Cell(2, 11).Value = 1;
        }
    }

    private static void AddSheet(XLWorkbook workbook, string name, IReadOnlyList<string> headers)
    {
        var worksheet = workbook.Worksheets.Add(name);
        for (var index = 0; index < headers.Count; index++)
            worksheet.Cell(1, index + 1).Value = headers[index];
    }

    private static IFormFile CreateWorkbookFile(XLWorkbook workbook)
    {
        var stream = CreateWorkbookStream(workbook);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "questions.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    private static MemoryStream CreateWorkbookStream(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
