using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Modules.TestGen.Queries.GetBlueprintExamOptions;
using MathInsight.Modules.TestGen.Tests;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class BlueprintExamGenerationTests
{
    private const string StudentId = "student-12";
    private const string BlueprintId = "blueprint-exam";
    private const string EasyDifficultyId = "difficulty-easy";
    private const string TopicA = "topic-a";
    private const string TopicB = "topic-b";

    [Fact]
    public async Task Options_ReturnOnlyApprovedOrActiveBlueprintsForStudentGrade()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentId, 12);
        AddBlueprint(testContext, "approved", BlueprintStatuses.Approved, grade: 12);
        AddBlueprint(testContext, "active", BlueprintStatuses.Active, grade: 12);
        AddBlueprint(testContext, "draft", BlueprintStatuses.Draft, grade: 12);
        AddBlueprint(testContext, "grade-11", BlueprintStatuses.Approved, grade: 11);
        await testContext.Context.SaveChangesAsync();

        var result = await new GetBlueprintExamOptionsQueryHandler(testContext.Context).Handle(
            new GetBlueprintExamOptionsQuery(StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyList<BlueprintExamOptionResponse>>(
            result.Value);
        Assert.Equal(["active", "approved"], items.Select(item => item.BlueprintId));
        Assert.All(items, item => Assert.Equal(12, item.Grade));
    }

    [Fact]
    public async Task Options_StudentWithoutUsableGrade_ReturnsStudentNotFound()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentId, null);
        await testContext.Context.SaveChangesAsync();

        var result = await new GetBlueprintExamOptionsQueryHandler(testContext.Context).Handle(
            new GetBlueprintExamOptionsQuery(StudentId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.StudentNotFound, result.Error);
    }

    [Fact]
    public async Task Generate_ValidBlueprint_PersistsAtomicBaselineAuditAndActivatesBlueprint()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentId, 12);
        var blueprint = AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Approved, grade: 12);
        AddQuestion(testContext, "question-a", TopicA);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var test = await testContext.Context.Tests
            .Include(item => item.Questions)
            .SingleAsync();
        var selected = Assert.Single(test.Questions);
        Assert.Equal(StudentId, test.GeneratedForStudentId);
        Assert.Equal(GeneratedTestValues.BlueprintExamMode, test.TestMode);
        Assert.Equal(GeneratedTestValues.SystemGenerator, test.GeneratedBy);
        Assert.Equal(GeneratedTestValues.ActiveStatus, test.TestStatus);
        Assert.Null(test.TestCode);
        Assert.Equal(blueprint.BlueprintName, test.TestName);
        Assert.Equal("question-a", selected.QuestionId);
        Assert.Equal(1, selected.QuestionOrder);
        Assert.Equal(blueprint.Sections.Single().Details.Single().BlueprintDetailId, selected.SourceBlueprintDetailId);
        Assert.Equal(GeneratedTestValues.BlueprintNormalReason, selected.SelectionReason);
        Assert.False(selected.IsAdaptiveSelected);
        Assert.Null(selected.RecommendedForTagId);
        Assert.Null(selected.RecommendedDifficultyId);
        Assert.Null(selected.PtagAtSelection);
        Assert.Null(selected.RuleVersion);
        Assert.Equal(BlueprintStatuses.Active, blueprint.Status);
    }

    [Fact]
    public async Task Generate_OverlappingMultiTopicPool_UsesCompleteGlobalAssignment()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentId, 12);
        var blueprint = AddBlueprint(
            testContext,
            BlueprintId,
            BlueprintStatuses.Approved,
            grade: 12,
            (TopicA, 1),
            (TopicB, 1));
        AddQuestion(testContext, "question-flex", TopicA, TopicB);
        AddQuestion(testContext, "question-a-only", TopicA);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var selected = await testContext.Context.TestQuestions
            .OrderBy(item => item.QuestionOrder)
            .ToListAsync();
        Assert.Equal(2, selected.Count);
        Assert.Equal(2, selected.Select(item => item.QuestionId).Distinct().Count());

        var details = blueprint.Sections.Single().Details.ToDictionary(item => item.TagId);
        Assert.Contains(selected, item =>
            item.QuestionId == "question-a-only" &&
            item.SourceBlueprintDetailId == details[TopicA].BlueprintDetailId);
        Assert.Contains(selected, item =>
            item.QuestionId == "question-flex" &&
            item.SourceBlueprintDetailId == details[TopicB].BlueprintDetailId);
    }

    [Fact]
    public async Task Generate_IgnoresCandidatesWithWrongShape()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentId, 12);
        AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Approved, grade: 12);
        AddQuestion(testContext, "valid", TopicA);
        AddQuestion(testContext, "inactive", TopicA, isActive: false);
        AddQuestion(testContext, "reported", TopicA, status: "Reported");
        AddQuestion(testContext, "wrong-grade", TopicA, grade: 11);
        AddQuestion(testContext, "wrong-type", TopicA, questionType: BlueprintQuestionTypes.TrueFalse);
        AddQuestion(testContext, "wrong-difficulty", TopicA, difficultyId: "difficulty-hard");
        AddQuestion(testContext, "wrong-topic", TopicB);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("valid", (await testContext.Context.TestQuestions.SingleAsync()).QuestionId);
    }

    [Fact]
    public async Task Generate_InsufficientPool_WritesNothingAndKeepsApprovedStatus()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentId, 12);
        var blueprint = AddBlueprint(
            testContext,
            BlueprintId,
            BlueprintStatuses.Approved,
            grade: 12,
            (TopicA, 2));
        AddQuestion(testContext, "only-question", TopicA);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.InsufficientQuestions, result.Error);
        Assert.Empty(await testContext.Context.Tests.ToListAsync());
        Assert.Empty(await testContext.Context.TestQuestions.ToListAsync());
        Assert.Equal(BlueprintStatuses.Approved, blueprint.Status);
    }

    [Theory]
    [InlineData("student", "TEST_GENERATION_STUDENT_NOT_FOUND")]
    [InlineData("blueprint", "TEST_GENERATION_BLUEPRINT_NOT_FOUND")]
    public async Task Generate_MissingRequiredResource_ReturnsStableError(
        string missingResource,
        string expectedCode)
    {
        await using var testContext = TestGenInMemoryContext.Create();
        if (missingResource != "student")
            AddStudent(testContext, StudentId, 12);
        if (missingResource != "blueprint")
        {
            AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Approved, grade: 12);
            AddQuestion(testContext, "question-a", TopicA);
        }
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Empty(await testContext.Context.Tests.ToListAsync());
    }

    [Fact]
    public async Task Generate_BlankBlueprintId_ReturnsRequestInvalid()
    {
        await using var testContext = TestGenInMemoryContext.Create();

        var result = await CreateHandler(testContext).Handle(
            new GenerateBlueprintExamCommand(" ", StudentId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.RequestInvalid, result.Error);
    }

    [Theory]
    [InlineData(BlueprintStatuses.Draft, 12, "TEST_GENERATION_BLUEPRINT_UNAVAILABLE")]
    [InlineData(BlueprintStatuses.PendingReview, 12, "TEST_GENERATION_BLUEPRINT_UNAVAILABLE")]
    [InlineData(BlueprintStatuses.Approved, 11, "TEST_GENERATION_GRADE_MISMATCH")]
    public async Task Generate_InvalidStatusOrGrade_ReturnsStableError(
        string status,
        int grade,
        string expectedCode)
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentId, 12);
        AddBlueprint(testContext, BlueprintId, status, grade);
        AddQuestion(testContext, "question-a", TopicA, grade: grade);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Empty(await testContext.Context.Tests.ToListAsync());
    }

    [Fact]
    public async Task Generate_ActiveBlueprint_RemainsActive()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentId, 12);
        var blueprint = AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Active, grade: 12);
        AddQuestion(testContext, "question-a", TopicA);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BlueprintStatuses.Active, blueprint.Status);
    }

    private static GenerateBlueprintExamCommandHandler CreateHandler(TestGenInMemoryContext testContext)
        => new(
            testContext.Context,
            new BlueprintExamCandidateProvider(testContext.Context),
            new CapacityAwareQuestionSelector(new NoOpGenerationRandomizer()));

    private static void AddStudent(TestGenInMemoryContext testContext, string studentId, int? grade)
        => testContext.Context.Students.Add(new StudentReadModel
        {
            StudentId = studentId,
            CurrentGrade = grade
        });

    private static Blueprint AddBlueprint(
        TestGenInMemoryContext testContext,
        string blueprintId,
        string status,
        int grade,
        params (string TagId, int Quantity)[] slots)
    {
        if (slots.Length == 0)
            slots = [(TopicA, 1)];

        var totalQuestions = slots.Sum(slot => slot.Quantity);
        var blueprint = new Blueprint
        {
            BlueprintId = blueprintId,
            BlueprintName = blueprintId,
            Grade = grade,
            TotalQuestions = totalQuestions,
            DurationMinutes = 30,
            ExpertId = "expert-owner",
            Status = status
        };
        var section = new BlueprintSection
        {
            BlueprintSectionId = $"{blueprintId}-section",
            BlueprintId = blueprintId,
            SectionOrder = 1,
            SectionName = "Single choice",
            QuestionType = BlueprintQuestionTypes.SingleChoice,
            TotalQuestions = totalQuestions,
            DefaultPointPerQuestion = 1m
        };

        for (var index = 0; index < slots.Length; index++)
        {
            section.Details.Add(new BlueprintDetail
            {
                BlueprintDetailId = $"{blueprintId}-detail-{index}",
                BlueprintId = blueprintId,
                BlueprintSectionId = section.BlueprintSectionId,
                TagId = slots[index].TagId,
                DifficultyId = EasyDifficultyId,
                Quantity = slots[index].Quantity
            });
        }

        blueprint.Sections.Add(section);
        testContext.Context.Blueprints.Add(blueprint);
        return blueprint;
    }

    private static void AddQuestion(
        TestGenInMemoryContext testContext,
        string questionId,
        string firstTag,
        string? secondTag = null,
        bool isActive = true,
        string status = "Approved",
        int grade = 12,
        string questionType = BlueprintQuestionTypes.SingleChoice,
        string difficultyId = EasyDifficultyId)
    {
        testContext.Context.Questions.Add(new QuestionReadModel
        {
            QuestionId = questionId,
            DifficultyId = difficultyId,
            Grade = grade,
            Status = status,
            QuestionType = questionType,
            IsActive = isActive
        });
        testContext.Context.QuestionTopics.Add(new QuestionTopicReadModel
        {
            QuestionTopicId = $"{questionId}-topic-1",
            QuestionId = questionId,
            TagId = firstTag,
            IsPrimary = true
        });

        if (secondTag is not null)
        {
            testContext.Context.QuestionTopics.Add(new QuestionTopicReadModel
            {
                QuestionTopicId = $"{questionId}-topic-2",
                QuestionId = questionId,
                TagId = secondTag,
                IsPrimary = false
            });
        }
    }

    private sealed class NoOpGenerationRandomizer : IGenerationRandomizer
    {
        public void Shuffle<T>(IList<T> values)
        {
        }
    }
}
