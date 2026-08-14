using System.Text.Json;
using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.ArchiveSharedBlueprintExam;
using MathInsight.Modules.TestGen.Commands.GenerateSharedBlueprintExam;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Modules.TestGen.Queries.GetExpertTestPreview;
using MathInsight.Modules.TestGen.Queries.GetBlueprintGeneratedTests;
using MathInsight.Modules.TestGen.Queries.GetSharedBlueprintExams;
using MathInsight.Modules.TestGen.Queries.ResolveSharedTestCode;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Scoring;
using Microsoft.EntityFrameworkCore;
using TestEntity = MathInsight.Modules.TestGen.Persistence.Entities.Test;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class SharedBlueprintExamTests
{
    private const string OwnerExpertId = "expert-owner";
    private const string OtherExpertId = "expert-other";
    private const string StudentGrade12Id = "student-grade-12";
    private const string StudentGrade11Id = "student-grade-11";
    private const string BlueprintId = "shared-blueprint";
    private const string DifficultyId = "difficulty-easy";
    private const string TopicId = "topic-algebra";

    [Fact]
    public void SecureTestCodeGenerator_UsesEightCharactersFromUnambiguousAlphabet()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var generator = new SecureTestCodeGenerator();

        var codes = Enumerable.Range(0, 25).Select(_ => generator.Generate()).ToList();

        Assert.All(codes, code =>
        {
            Assert.Equal(8, code.Length);
            Assert.All(code, character => Assert.Contains(character, alphabet));
        });
    }

    [Fact]
    public async Task GetBlueprintGeneratedTests_OwnerReceivesActiveAndArchivedVariants()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Active, OwnerExpertId);
        AddGeneratedTest(testContext, "active-test", blueprint, "ACTIVE23");
        AddGeneratedTest(
            testContext,
            "archived-test",
            blueprint,
            "ARCHIVE2",
            GeneratedTestValues.ArchivedStatus);
        await testContext.Context.SaveChangesAsync();

        var result = await new GetBlueprintGeneratedTestsQueryHandler(testContext.Context).Handle(
            new GetBlueprintGeneratedTestsQuery(BlueprintId, OwnerExpertId, 1, 20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Contains(result.Value.Items, item => item.TestStatus == GeneratedTestValues.ActiveStatus);
        Assert.Contains(result.Value.Items, item => item.TestStatus == GeneratedTestValues.ArchivedStatus);
    }

    [Fact]
    public async Task GetBlueprintGeneratedTests_NonOwner_ReturnsMutationForbidden()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Active, OwnerExpertId);
        await testContext.Context.SaveChangesAsync();

        var result = await new GetBlueprintGeneratedTestsQueryHandler(testContext.Context).Handle(
            new GetBlueprintGeneratedTestsQuery(BlueprintId, OtherExpertId, 1, 20),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.MutationForbidden, result.Error);
    }

    [Fact]
    public async Task Generate_OwnerApprovedBlueprint_PersistsSharedSnapshotsAndActivatesBlueprint()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddExpert(testContext, OwnerExpertId);
        var blueprint = AddBlueprint(
            testContext,
            BlueprintId,
            BlueprintStatuses.Approved,
            OwnerExpertId,
            grade: 12,
            quantity: 3,
            totalScore: 10m,
            sectionScore: 10m);
        AddQuestion(testContext, "question-weight-one", 1m);
        AddQuestion(testContext, "question-weight-one-point-five", 1.5m);
        AddQuestion(testContext, "question-weight-two", 2m);
        await testContext.Context.SaveChangesAsync();
        var codeGenerator = new QueueTestCodeGenerator("SHARE234");

        var result = await CreateGenerationHandler(testContext, codeGenerator).Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OwnerExpertId, "  Midterm variant  ", 45),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SHARE234", result.Value!.TestCode);
        Assert.Equal("Midterm variant", result.Value.TestName);
        Assert.Null(result.Value.GeneratedForStudentId);
        Assert.Equal(GeneratedTestValues.SystemGenerator, result.Value.GeneratedBy);
        Assert.Equal(GeneratedTestValues.ActiveStatus, result.Value.TestStatus);
        Assert.Equal(ScoringPolicies.BlueprintBudget, result.Value.ScoringPolicy);
        Assert.Equal(BlueprintStatuses.Active, blueprint.Status);

        var persisted = await testContext.Context.Tests
            .Include(test => test.Questions)
            .SingleAsync();
        Assert.Null(persisted.GeneratedForStudentId);
        Assert.Equal(GeneratedTestValues.SystemGenerator, persisted.GeneratedBy);
        Assert.Equal(GeneratedTestValues.BlueprintExamMode, persisted.TestMode);
        Assert.Equal("SHARE234", persisted.TestCode);
        Assert.Equal(10m, persisted.Questions.Sum(question => question.MaxPointsSnapshot));
        Assert.Equal([1, 2, 3], persisted.Questions.OrderBy(question => question.QuestionOrder).Select(question => question.QuestionOrder));

        var snapshots = persisted.Questions.ToDictionary(question => question.QuestionId);
        Assert.Equal(2.22m, snapshots["question-weight-one"].MaxPointsSnapshot);
        Assert.Equal(3.33m, snapshots["question-weight-one-point-five"].MaxPointsSnapshot);
        Assert.Equal(4.45m, snapshots["question-weight-two"].MaxPointsSnapshot);
        Assert.Equal(1m, snapshots["question-weight-one"].WeightSnapshot);
        Assert.Equal(1.5m, snapshots["question-weight-one-point-five"].WeightSnapshot);
        Assert.Equal(2m, snapshots["question-weight-two"].WeightSnapshot);
        Assert.All(persisted.Questions, question =>
        {
            Assert.EndsWith("-version-1", question.QuestionVersionId);
            Assert.Equal(ScoringRules.AllOrNothing, question.ScoringRuleSnapshot);
            Assert.False(question.IsScoreInvalidated);
        });
        Assert.Equal(3, result.Value.Questions.Count);
        Assert.Equal(1, codeGenerator.Calls);
    }

    [Fact]
    public async Task Generate_ActiveBlueprint_CanCreateAnotherSharedVariant()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddExpert(testContext, OwnerExpertId);
        var blueprint = AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Approved, OwnerExpertId);
        AddQuestion(testContext, "variant-question", 1m);
        await testContext.Context.SaveChangesAsync();
        var codeGenerator = new QueueTestCodeGenerator("FIRST234", "NEXT5678");
        var handler = CreateGenerationHandler(testContext, codeGenerator);

        var first = await handler.Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OwnerExpertId, "Variant one", 30),
            CancellationToken.None);
        var second = await handler.Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OwnerExpertId, "Variant two", 35),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value!.TestId, second.Value!.TestId);
        Assert.Equal("FIRST234", first.Value.TestCode);
        Assert.Equal("NEXT5678", second.Value.TestCode);
        Assert.Equal(BlueprintStatuses.Active, blueprint.Status);
        Assert.Equal(2, await testContext.Context.Tests.CountAsync());
    }

    [Fact]
    public async Task Generate_NonOwner_ReturnsMutationForbiddenAndWritesNothing()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddExpert(testContext, OwnerExpertId);
        AddExpert(testContext, OtherExpertId);
        AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Approved, OwnerExpertId);
        AddQuestion(testContext, "owner-question", 1m);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateGenerationHandler(testContext, new QueueTestCodeGenerator("OTHER234")).Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OtherExpertId, "Forbidden", 30),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.MutationForbidden, result.Error);
        Assert.Empty(await testContext.Context.Tests.ToListAsync());
    }

    [Fact]
    public async Task Generate_DraftBlueprint_ReturnsStatusInvalid()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddExpert(testContext, OwnerExpertId);
        AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Draft, OwnerExpertId);
        AddQuestion(testContext, "draft-question", 1m);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateGenerationHandler(testContext, new QueueTestCodeGenerator("DRAFT234")).Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OwnerExpertId, "Draft exam", 30),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.StatusInvalid, result.Error);
        Assert.Empty(await testContext.Context.Tests.ToListAsync());
    }

    [Fact]
    public async Task Generate_ScoreBudgetMismatch_ReturnsSpecificError()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddExpert(testContext, OwnerExpertId);
        AddBlueprint(
            testContext,
            BlueprintId,
            BlueprintStatuses.Approved,
            OwnerExpertId,
            totalScore: 10m,
            sectionScore: 9m);
        AddQuestion(testContext, "score-question", 1m);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateGenerationHandler(testContext, new QueueTestCodeGenerator("SCORE234")).Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OwnerExpertId, "Score mismatch", 30),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.ScoreBudgetMismatch, result.Error);
        Assert.Empty(await testContext.Context.Tests.ToListAsync());
    }

    [Fact]
    public async Task Generate_InsufficientQuestionPool_ReturnsConflictError()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddExpert(testContext, OwnerExpertId);
        AddBlueprint(
            testContext,
            BlueprintId,
            BlueprintStatuses.Approved,
            OwnerExpertId,
            quantity: 2,
            totalScore: 2m,
            sectionScore: 2m);
        AddQuestion(testContext, "only-question", 1m);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateGenerationHandler(testContext, new QueueTestCodeGenerator("POOL2345")).Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OwnerExpertId, "Insufficient", 30),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.QuestionPoolInsufficient, result.Error);
        Assert.Empty(await testContext.Context.Tests.ToListAsync());
    }

    [Fact]
    public async Task Generate_InvalidLatestV2DespiteValidOlderVersion_ReturnsQuestionVersionMissing()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddExpert(testContext, OwnerExpertId);
        AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Approved, OwnerExpertId);
        AddQuestion(testContext, "versioned-question", 1m);
        testContext.Context.QuestionVersions.Add(new QuestionVersionReadModel
        {
            VersionId = "versioned-question-version-2-invalid",
            QuestionId = "versioned-question",
            VersionNumber = 2,
            SnapshotSchemaVersion = 2,
            AnswersSnapshot = "{ invalid-v2",
            CreatedTime = DateTime.UtcNow.AddMinutes(1)
        });
        await testContext.Context.SaveChangesAsync();

        var result = await CreateGenerationHandler(testContext, new QueueTestCodeGenerator("VERS2345")).Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OwnerExpertId, "Invalid latest", 30),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.QuestionVersionMissing, result.Error);
        Assert.Empty(await testContext.Context.Tests.ToListAsync());
    }

    [Fact]
    public async Task Generate_TestCodePrecheckCollision_RetriesWithNextDeterministicCode()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddExpert(testContext, OwnerExpertId);
        var blueprint = AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Approved, OwnerExpertId);
        AddQuestion(testContext, "collision-question", 1m);
        AddGeneratedTest(testContext, "existing-shared-test", blueprint, "TAKEN234");
        await testContext.Context.SaveChangesAsync();
        var codeGenerator = new QueueTestCodeGenerator("TAKEN234", "FRESH234");

        var result = await CreateGenerationHandler(testContext, codeGenerator).Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OwnerExpertId, "Collision retry", 30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("FRESH234", result.Value!.TestCode);
        Assert.Equal(2, codeGenerator.Calls);
        Assert.Equal(2, await testContext.Context.Tests.CountAsync());
    }

    [Fact]
    public async Task Generate_FiveTestCodePrecheckCollisions_ReturnsGenerationConflict()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddExpert(testContext, OwnerExpertId);
        var blueprint = AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Approved, OwnerExpertId);
        AddQuestion(testContext, "exhaustion-question", 1m);
        AddGeneratedTest(testContext, "existing-shared-test", blueprint, "TAKEN234");
        await testContext.Context.SaveChangesAsync();
        var codeGenerator = new QueueTestCodeGenerator(
            "TAKEN234", "TAKEN234", "TAKEN234", "TAKEN234", "TAKEN234");

        var result = await CreateGenerationHandler(testContext, codeGenerator).Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OwnerExpertId, "Collision exhaustion", 30),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.GenerationConflict, result.Error);
        Assert.Equal(5, codeGenerator.Calls);
        Assert.Single(await testContext.Context.Tests.ToListAsync());
    }

    [Fact]
    public async Task ExpertPreview_ReturnsImmutableVersionSolutionAndAnswerKey()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddExpert(testContext, OwnerExpertId);
        AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Approved, OwnerExpertId);
        AddQuestion(testContext, "preview-question", 1m);
        await testContext.Context.SaveChangesAsync();
        var generated = await CreateGenerationHandler(testContext, new QueueTestCodeGenerator("PREV2345")).Handle(
            new GenerateSharedBlueprintExamCommand(BlueprintId, OwnerExpertId, "Preview exam", 40),
            CancellationToken.None);
        Assert.True(generated.IsSuccess);

        var currentAnswers = await testContext.Context.Answers
            .Where(answer => answer.QuestionId == "preview-question")
            .OrderBy(answer => answer.AnswerId)
            .ToListAsync();
        Assert.Equal(2, currentAnswers.Count);
        currentAnswers[0].IsCorrect = false;
        currentAnswers[1].IsCorrect = true;
        await testContext.Context.SaveChangesAsync();

        var result = await new GetExpertTestPreviewQueryHandler(testContext.Context).Handle(
            new GetExpertTestPreviewQuery(generated.Value!.TestId, OwnerExpertId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PREV2345", result.Value!.TestCode);
        var section = Assert.Single(result.Value.Sections);
        var question = Assert.Single(section.Questions);
        Assert.Equal("Prompt for preview-question", question.QuestionContent);
        Assert.Equal("Solution for preview-question", question.SolutionContent);
        Assert.Equal("preview-question-version-1", question.QuestionVersionId);
        Assert.Collection(
            question.Answers,
            answer =>
            {
                Assert.Equal("preview-question-answer-correct", answer.AnswerId);
                Assert.Equal("Correct answer", answer.AnswerContent);
                Assert.True(answer.IsCorrect);
            },
            answer =>
            {
                Assert.Equal("preview-question-answer-wrong", answer.AnswerId);
                Assert.Equal("Wrong answer", answer.AnswerContent);
                Assert.False(answer.IsCorrect);
            });
    }

    [Theory]
    [InlineData(true, OwnerExpertId, "GENERATED_TEST_NOT_FOUND")]
    [InlineData(false, OtherExpertId, "BLUEPRINT_MUTATION_FORBIDDEN")]
    public async Task ExpertPreview_RejectsPersonalOrOtherOwnersTest(
        bool personal,
        string expertId,
        string expectedCode)
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Active, OwnerExpertId);
        AddGeneratedTest(
            testContext,
            "preview-inaccessible-test",
            blueprint,
            "DENY2345",
            generatedForStudentId: personal ? StudentGrade12Id : null);
        await testContext.Context.SaveChangesAsync();

        var result = await new GetExpertTestPreviewQueryHandler(testContext.Context).Handle(
            new GetExpertTestPreviewQuery("preview-inaccessible-test", expertId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public async Task Archive_SharedActiveTest_IsIdempotent()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Active, OwnerExpertId);
        AddGeneratedTest(testContext, "archive-shared-test", blueprint, "ARCH2345");
        await testContext.Context.SaveChangesAsync();
        var handler = new ArchiveSharedBlueprintExamCommandHandler(testContext.Context);
        var command = new ArchiveSharedBlueprintExamCommand(
            "archive-shared-test",
            OwnerExpertId,
            GeneratedTestValues.ArchivedStatus);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(GeneratedTestValues.ArchivedStatus, first.Value!.TestStatus);
        Assert.Equal(GeneratedTestValues.ArchivedStatus, second.Value!.TestStatus);
        Assert.Equal(
            GeneratedTestValues.ArchivedStatus,
            await testContext.Context.Tests.Select(test => test.TestStatus).SingleAsync());
    }

    [Theory]
    [InlineData(true, OwnerExpertId, "GENERATED_TEST_NOT_FOUND")]
    [InlineData(false, OtherExpertId, "BLUEPRINT_MUTATION_FORBIDDEN")]
    public async Task Archive_RejectsPersonalOrOtherOwnersTest(
        bool personal,
        string expertId,
        string expectedCode)
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Active, OwnerExpertId);
        AddGeneratedTest(
            testContext,
            "archive-inaccessible-test",
            blueprint,
            "NOAR2345",
            generatedForStudentId: personal ? StudentGrade12Id : null);
        await testContext.Context.SaveChangesAsync();

        var result = await new ArchiveSharedBlueprintExamCommandHandler(testContext.Context).Handle(
            new ArchiveSharedBlueprintExamCommand(
                "archive-inaccessible-test",
                expertId,
                GeneratedTestValues.ArchivedStatus),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(GeneratedTestValues.ActiveStatus, (await testContext.Context.Tests.SingleAsync()).TestStatus);
    }

    [Fact]
    public async Task Discovery_ReturnsOnlyActiveSharedBlueprintExamForExactActiveBlueprintGrade()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentGrade12Id, 12);
        var includedBlueprint = AddBlueprint(testContext, "blueprint-active-grade-12", BlueprintStatuses.Active, OwnerExpertId, grade: 12);
        var wrongGradeBlueprint = AddBlueprint(testContext, "blueprint-active-grade-11", BlueprintStatuses.Active, OwnerExpertId, grade: 11);
        var deactivatedBlueprint = AddBlueprint(testContext, "blueprint-deactivated", BlueprintStatuses.Deactivated, OwnerExpertId, grade: 12);
        var approvedBlueprint = AddBlueprint(testContext, "blueprint-approved", BlueprintStatuses.Approved, OwnerExpertId, grade: 12);
        AddGeneratedTest(testContext, "test-included", includedBlueprint, "INCL2345");
        AddGeneratedTest(testContext, "test-personal", includedBlueprint, null, generatedForStudentId: StudentGrade12Id);
        AddGeneratedTest(testContext, "test-archived", includedBlueprint, "ARCH6789", GeneratedTestValues.ArchivedStatus);
        AddGeneratedTest(testContext, "test-deactivated", deactivatedBlueprint, "DEAC2345");
        AddGeneratedTest(testContext, "test-wrong-grade", wrongGradeBlueprint, "GRAD2345");
        AddGeneratedTest(testContext, "test-blueprint-approved", approvedBlueprint, "APPR2345");
        AddGeneratedTest(testContext, "test-wrong-mode", includedBlueprint, "MODE2345", testMode: "AdaptivePractice");
        await testContext.Context.SaveChangesAsync();

        var result = await new GetSharedBlueprintExamsQueryHandler(testContext.Context).Handle(
            new GetSharedBlueprintExamsQuery(StudentGrade12Id, 1, 20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalCount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("test-included", item.TestId);
        Assert.Equal("blueprint-active-grade-12", item.BlueprintId);
        Assert.Equal(12, item.Grade);
    }

    [Fact]
    public async Task Discovery_FiltersGenerationTypeBeforeCountingAndPagination()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentGrade12Id, 12);
        var blueprint = AddBlueprint(testContext, "catalog-blueprint", BlueprintStatuses.Active, OwnerExpertId, grade: 12);
        AddGeneratedTest(testContext, "random-one", blueprint, "RANDOM01");
        AddGeneratedTest(testContext, "fixed-one", blueprint, "FIXED001", selectionReason: GeneratedTestValues.FixedExamReason);
        AddGeneratedTest(testContext, "random-two", blueprint, "RANDOM02");
        await testContext.Context.SaveChangesAsync();

        var handler = new GetSharedBlueprintExamsQueryHandler(testContext.Context);
        var fixedResult = await handler.Handle(
            new GetSharedBlueprintExamsQuery(StudentGrade12Id, 1, 1, " fixed "),
            CancellationToken.None);
        var randomResult = await handler.Handle(
            new GetSharedBlueprintExamsQuery(StudentGrade12Id, 1, 1, "RANDOM"),
            CancellationToken.None);

        Assert.True(fixedResult.IsSuccess);
        Assert.Equal(1, fixedResult.Value!.TotalCount);
        var fixedExam = Assert.Single(fixedResult.Value.Items);
        Assert.Equal("fixed-one", fixedExam.TestId);
        Assert.Equal(GeneratedTestValues.FixedGenerationType, fixedExam.GenerationType);

        Assert.True(randomResult.IsSuccess);
        Assert.Equal(2, randomResult.Value!.TotalCount);
        Assert.Single(randomResult.Value.Items);
        Assert.Equal(GeneratedTestValues.RandomGenerationType, randomResult.Value.Items[0].GenerationType);
    }

    [Fact]
    public async Task Discovery_MixedSelectionReasons_ReturnsStableContractError()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentGrade12Id, 12);
        var blueprint = AddBlueprint(testContext, "mixed-blueprint", BlueprintStatuses.Active, OwnerExpertId, grade: 12);
        var test = AddGeneratedTest(testContext, "mixed-test", blueprint, "MIXED001");
        test.Questions.Add(new TestQuestion
        {
            TestId = test.TestId,
            QuestionId = "mixed-fixed-question",
            QuestionOrder = 2,
            SelectionReason = GeneratedTestValues.FixedExamReason,
            QuestionVersionId = "mixed-fixed-version",
            WeightSnapshot = 1m,
            MaxPointsSnapshot = 1m
        });
        await testContext.Context.SaveChangesAsync();

        var result = await new GetSharedBlueprintExamsQueryHandler(testContext.Context).Handle(
            new GetSharedBlueprintExamsQuery(StudentGrade12Id, 1, 20, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.SharedExamGenerationTypeInvalid, result.Error);
    }

    [Fact]
    public async Task ResolveCode_TrimsAndNormalizesCase()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentGrade12Id, 12);
        var blueprint = AddBlueprint(testContext, BlueprintId, BlueprintStatuses.Active, OwnerExpertId, grade: 12);
        AddGeneratedTest(testContext, "resolvable-test", blueprint, "CODE2345");
        await testContext.Context.SaveChangesAsync();

        var result = await new ResolveSharedTestCodeQueryHandler(testContext.Context).Handle(
            new ResolveSharedTestCodeQuery(StudentGrade12Id, "  code2345  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("resolvable-test", result.Value!.TestId);
        Assert.Equal("CODE2345", result.Value.TestCode);
    }

    [Theory]
    [InlineData("mixed")]
    [InlineData("unknown")]
    [InlineData("empty")]
    public async Task ResolveCode_InvalidGenerationMetadata_ReturnsStableContractError(string scenario)
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentGrade12Id, 12);
        var blueprint = AddBlueprint(testContext, $"resolve-{scenario}", BlueprintStatuses.Active, OwnerExpertId, grade: 12);
        var test = AddGeneratedTest(
            testContext,
            $"resolve-{scenario}",
            blueprint,
            scenario.ToUpperInvariant(),
            selectionReason: scenario == "unknown" ? "UnexpectedReason" : GeneratedTestValues.BlueprintNormalReason);
        if (scenario == "mixed")
        {
            test.Questions.Add(new TestQuestion
            {
                TestId = test.TestId,
                QuestionId = "resolve-mixed-fixed",
                QuestionOrder = 2,
                SelectionReason = GeneratedTestValues.FixedExamReason,
                QuestionVersionId = "resolve-mixed-fixed-version",
                WeightSnapshot = 1m,
                MaxPointsSnapshot = 1m
            });
        }
        else if (scenario == "empty")
        {
            test.Questions.Clear();
        }
        await testContext.Context.SaveChangesAsync();

        var result = await new ResolveSharedTestCodeQueryHandler(testContext.Context).Handle(
            new ResolveSharedTestCodeQuery(StudentGrade12Id, scenario.ToUpperInvariant()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.SharedExamGenerationTypeInvalid, result.Error);
    }

    [Theory]
    [InlineData("PERSONAL")]
    [InlineData("ARCHIVED")]
    [InlineData("DEACTIVATED")]
    [InlineData("WRONGGRADE")]
    [InlineData("UNKNOWN")]
    public async Task ResolveCode_UnavailableCasesReturnSameGenericError(string scenario)
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext, StudentGrade12Id, 12);
        AddStudent(testContext, StudentGrade11Id, 11);
        var activeGrade12 = AddBlueprint(testContext, "resolve-active-grade-12", BlueprintStatuses.Active, OwnerExpertId, grade: 12);
        var deactivatedGrade12 = AddBlueprint(testContext, "resolve-deactivated", BlueprintStatuses.Deactivated, OwnerExpertId, grade: 12);
        var activeGrade11 = AddBlueprint(testContext, "resolve-active-grade-11", BlueprintStatuses.Active, OwnerExpertId, grade: 11);
        AddGeneratedTest(testContext, "resolve-personal", activeGrade12, "PERSONAL", generatedForStudentId: StudentGrade12Id);
        AddGeneratedTest(testContext, "resolve-archived", activeGrade12, "ARCHIVED", GeneratedTestValues.ArchivedStatus);
        AddGeneratedTest(testContext, "resolve-deactivated", deactivatedGrade12, "DEACTIVATED");
        AddGeneratedTest(testContext, "resolve-wrong-grade", activeGrade11, "WRONGGRADE");
        await testContext.Context.SaveChangesAsync();

        var result = await new ResolveSharedTestCodeQueryHandler(testContext.Context).Handle(
            new ResolveSharedTestCodeQuery(StudentGrade12Id, scenario),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.TestCodeNotAvailable, result.Error);
    }

    private static GenerateSharedBlueprintExamCommandHandler CreateGenerationHandler(
        TestGenInMemoryContext testContext,
        ITestCodeGenerator testCodeGenerator)
        => new(
            testContext.Context,
            new BlueprintExamCandidateProvider(testContext.Context),
            new CapacityAwareQuestionSelector(new NoOpGenerationRandomizer()),
            testCodeGenerator);

    private static void AddExpert(TestGenInMemoryContext testContext, string expertId)
        => testContext.Context.Experts.Add(new ExpertReadModel
        {
            ExpertId = expertId,
            Specialty = "Mathematics"
        });

    private static void AddStudent(TestGenInMemoryContext testContext, string studentId, int grade)
        => testContext.Context.Students.Add(new StudentReadModel
        {
            StudentId = studentId,
            CurrentGrade = grade
        });

    private static Blueprint AddBlueprint(
        TestGenInMemoryContext testContext,
        string blueprintId,
        string status,
        string expertId,
        int grade = 12,
        int quantity = 1,
        decimal totalScore = 1m,
        decimal? sectionScore = null)
    {
        var blueprint = new Blueprint
        {
            BlueprintId = blueprintId,
            BlueprintName = $"Exam from {blueprintId}",
            Grade = grade,
            TotalQuestions = quantity,
            TotalScore = totalScore,
            DurationMinutes = 30,
            ExpertId = expertId,
            Status = status
        };
        var section = new BlueprintSection
        {
            BlueprintSectionId = $"{blueprintId}-section",
            BlueprintId = blueprintId,
            SectionOrder = 1,
            SectionCode = "A",
            SectionName = "Algebra",
            QuestionType = BlueprintQuestionTypes.SingleChoice,
            InstructionText = "Choose one answer.",
            TotalQuestions = quantity,
            ScoreBudget = sectionScore ?? totalScore,
            ScoringRule = ScoringRules.AllOrNothing
        };
        section.Details.Add(new BlueprintDetail
        {
            BlueprintDetailId = $"{blueprintId}-detail",
            BlueprintId = blueprintId,
            BlueprintSectionId = section.BlueprintSectionId,
            TagId = TopicId,
            DifficultyId = DifficultyId,
            Quantity = quantity
        });
        blueprint.Sections.Add(section);
        testContext.Context.Blueprints.Add(blueprint);
        return blueprint;
    }

    private static void AddQuestion(
        TestGenInMemoryContext testContext,
        string questionId,
        decimal weight)
    {
        var answers = new[]
        {
            new QuestionAnswerSnapshot($"{questionId}-answer-correct", "Correct answer", true),
            new QuestionAnswerSnapshot($"{questionId}-answer-wrong", "Wrong answer", false)
        };
        var snapshot = new QuestionSnapshotV2(
            questionId,
            BlueprintQuestionTypes.SingleChoice,
            DifficultyId,
            12,
            weight,
            [new QuestionTopicSnapshot(TopicId, true)],
            answers,
            [],
            $"Prompt for {questionId}",
            $"Solution for {questionId}",
            $"https://images.local/{questionId}.png");

        testContext.Context.Questions.Add(new QuestionReadModel
        {
            QuestionId = questionId,
            DifficultyId = DifficultyId,
            Grade = 12,
            Status = "Approved",
            QuestionType = BlueprintQuestionTypes.SingleChoice,
            DefaultWeight = weight,
            IsActive = true
        });
        testContext.Context.QuestionVersions.Add(new QuestionVersionReadModel
        {
            VersionId = $"{questionId}-version-1",
            QuestionId = questionId,
            VersionNumber = 1,
            SnapshotSchemaVersion = 2,
            AnswersSnapshot = JsonSerializer.Serialize(snapshot),
            CreatedTime = DateTime.UtcNow
        });
        testContext.Context.QuestionTopics.Add(new QuestionTopicReadModel
        {
            QuestionTopicId = $"{questionId}-topic",
            QuestionId = questionId,
            TagId = TopicId,
            IsPrimary = true
        });
        foreach (var answer in answers)
        {
            testContext.Context.Answers.Add(new AnswerReadModel
            {
                AnswerId = answer.AnswerId,
                QuestionId = questionId,
                IsCorrect = answer.IsCorrect,
                IsArchived = false
            });
        }
    }

    private static TestEntity AddGeneratedTest(
        TestGenInMemoryContext testContext,
        string testId,
        Blueprint blueprint,
        string? testCode,
        string testStatus = GeneratedTestValues.ActiveStatus,
        string? generatedForStudentId = null,
        string testMode = GeneratedTestValues.BlueprintExamMode,
        string selectionReason = GeneratedTestValues.BlueprintNormalReason)
    {
        var test = new TestEntity
        {
            TestId = testId,
            BlueprintId = blueprint.BlueprintId,
            TestStatus = testStatus,
            TestMode = testMode,
            GeneratedForStudentId = generatedForStudentId,
            GeneratedBy = GeneratedTestValues.SystemGenerator,
            TestName = $"Generated {testId}",
            TestCode = testCode,
            DurationMinutes = 30,
            TotalQuestions = 1,
            MaxScore = 1m,
            ScoringPolicy = ScoringPolicies.BlueprintBudget,
            CreatedTime = DateTime.UtcNow
        };
        test.Questions.Add(new TestQuestion
        {
            TestId = testId,
            QuestionId = $"{testId}-question",
            QuestionOrder = 1,
            SelectionReason = selectionReason,
            QuestionVersionId = $"{testId}-version",
            WeightSnapshot = 1m,
            MaxPointsSnapshot = 1m
        });
        testContext.Context.Tests.Add(test);
        return test;
    }

    private sealed class QueueTestCodeGenerator(params string[] codes) : ITestCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);

        public int Calls { get; private set; }

        public string Generate()
        {
            Calls++;
            return _codes.Dequeue();
        }
    }

    private sealed class NoOpGenerationRandomizer : IGenerationRandomizer
    {
        public void Shuffle<T>(IList<T> values)
        {
        }
    }
}
