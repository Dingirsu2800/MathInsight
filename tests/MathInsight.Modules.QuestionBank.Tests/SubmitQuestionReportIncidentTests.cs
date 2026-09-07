using MathInsight.Modules.QuestionBank.Commands.ReportQuestion;
using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Commands.AdminApproveQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.AdminRejectQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.SubmitQuestionReportIncident;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class SubmitQuestionReportIncidentTests
{
    [Fact]
    public async Task SubmitByIncidentId_CreatesCorrectionAndMovesIncidentToPendingAdminReview()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "incident-submit-question");
        var version = AddVersion(database, question, 1);
        await database.Context.SaveChangesAsync();

        var reportResult = await new ReportQuestionCommandHandler(database.Context).Handle(
            new ReportQuestionCommand(
                question.QuestionId,
                new ReportQuestionRequest { ReportReason = "The answer is incorrect." },
                "admin-1",
                "Admin",
                null,
                version.VersionId),
            CancellationToken.None);
        Assert.True(reportResult.IsSuccess);
        var incident = await database.Context.QuestionReportIncidents.SingleAsync();
        var report = await database.Context.QuestionReports.SingleAsync();

        var command = new SubmitQuestionReportIncidentCommand(
            incident.IncidentId,
            new SubmitQuestionReportIncidentRequest
            {
                ExpectedRevision = incident.Revision,
                ExpectedQuestionVersionId = version.VersionId,
                SubmissionKey = "submit-1",
                ResolutionAction = "NoScoreChange",
                ReportDecisions =
                [
                    new QuestionReportDecisionRequest
                    {
                        ReportId = report.ReportId,
                        Disposition = "Resolved",
                        ReviewNote = "Correction addresses the answer."
                    }
                ],
                Correction = CreateCorrection(question)
            },
            question.ExpertId);

        var result = await new SubmitQuestionReportIncidentCommandHandler(database.Context)
            .Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(incident.IncidentId, result.Value!.IncidentId);
        Assert.Equal("PendingAdminReview", result.Value.Status);
        Assert.NotNull(result.Value.SubmittedCorrectionVersionId);
        Assert.Equal("PendingReview", report.Status);
        Assert.Equal("Resolved", report.ProposedStatus);
        Assert.Equal(2, await database.Context.QuestionVersions.CountAsync());
    }

    [Fact]
    public async Task RetryWithSameSubmissionKeyAndPayload_DoesNotCreateAnotherVersion()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var setup = await CreatePendingAdminIncidentAsync(database, "incident-retry-question");
        var request = CreateRequest(setup.Question, setup.Version, setup.Report, setup.Incident, "retry-key");
        var handler = new SubmitQuestionReportIncidentCommandHandler(database.Context);

        var first = await handler.Handle(
            new SubmitQuestionReportIncidentCommand(setup.Incident.IncidentId, request, setup.Question.ExpertId),
            CancellationToken.None);
        var retry = await handler.Handle(
            new SubmitQuestionReportIncidentCommand(setup.Incident.IncidentId, request, setup.Question.ExpertId),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value!.SubmittedCorrectionVersionId, retry.Value!.SubmittedCorrectionVersionId);
        Assert.Equal(2, await database.Context.QuestionVersions.CountAsync());
    }

    [Fact]
    public async Task SameSubmissionKeyWithDifferentPayload_ReturnsSubmissionKeyConflictBeforeMutation()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var setup = await CreatePendingAdminIncidentAsync(database, "incident-key-conflict-question");
        var handler = new SubmitQuestionReportIncidentCommandHandler(database.Context);
        var firstRequest = CreateRequest(setup.Question, setup.Version, setup.Report, setup.Incident, "same-key");

        var first = await handler.Handle(
            new SubmitQuestionReportIncidentCommand(setup.Incident.IncidentId, firstRequest, setup.Question.ExpertId),
            CancellationToken.None);
        var conflictingRequest = CreateRequest(setup.Question, setup.Version, setup.Report, setup.Incident, "same-key");
        conflictingRequest.Correction.QuestionContent = "A different correction";
        var conflicting = await handler.Handle(
            new SubmitQuestionReportIncidentCommand(setup.Incident.IncidentId, conflictingRequest, setup.Question.ExpertId),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(conflicting.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportSubmissionKeyConflict, conflicting.Error);
        Assert.Equal(2, await database.Context.QuestionVersions.CountAsync());
    }

    [Fact]
    public async Task StaleRevision_ReturnsIncidentConflictBeforeMutation()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var setup = await CreatePendingAdminIncidentAsync(database, "incident-stale-revision-question");
        var request = CreateRequest(setup.Question, setup.Version, setup.Report, setup.Incident, "stale-key");
        request.ExpectedRevision++;

        var result = await new SubmitQuestionReportIncidentCommandHandler(database.Context).Handle(
            new SubmitQuestionReportIncidentCommand(setup.Incident.IncidentId, request, setup.Question.ExpertId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportIncidentConflict, result.Error);
        Assert.Single(await database.Context.QuestionVersions.ToListAsync());
    }

    [Fact]
    public async Task NewReportDuringAdminReview_InvalidatesTheSubmittedProposal()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var setup = await CreatePendingAdminIncidentAsync(database, "incident-new-report-question");
        var submission = await new SubmitQuestionReportIncidentCommandHandler(database.Context).Handle(
            new SubmitQuestionReportIncidentCommand(
                setup.Incident.IncidentId,
                CreateRequest(setup.Question, setup.Version, setup.Report, setup.Incident, "first-submission"),
                setup.Question.ExpertId),
            CancellationToken.None);
        Assert.True(submission.IsSuccess);

        var newReport = await new ReportQuestionCommandHandler(database.Context).Handle(
            new ReportQuestionCommand(
                setup.Question.QuestionId,
                new ReportQuestionRequest { ReportReason = "A separate issue was found." },
                "student-2",
                "Student",
                null,
                setup.Version.VersionId),
            CancellationToken.None);

        Assert.True(newReport.IsSuccess);
        var incident = await database.Context.QuestionReportIncidents.SingleAsync();
        var reports = await database.Context.QuestionReports.OrderBy(item => item.CreatedTime).ToListAsync();
        Assert.Equal("Open", incident.Status);
        Assert.Null(incident.SubmittedCorrectionVersionId);
        Assert.Null(incident.SubmissionKey);
        Assert.Equal(2, incident.Revision);
        Assert.Equal(QuestionReportWorkflow.PendingFix, reports[0].Status);
        Assert.Null(reports[0].ProposedStatus);
        Assert.Equal(QuestionReportWorkflow.Pending, reports[1].Status);
    }

    [Fact]
    public async Task AdminApproval_RequiresTheExactSubmittedCorrectionVersion()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var setup = await CreatePendingAdminIncidentAsync(database, "incident-approval-version-question");
        var submission = await new SubmitQuestionReportIncidentCommandHandler(database.Context).Handle(
            new SubmitQuestionReportIncidentCommand(
                setup.Incident.IncidentId,
                CreateRequest(setup.Question, setup.Version, setup.Report, setup.Incident, "approval-submission"),
                setup.Question.ExpertId),
            CancellationToken.None);
        Assert.True(submission.IsSuccess);

        AddVersion(database, setup.Question, 3);
        await database.Context.SaveChangesAsync();

        var approval = await new AdminApproveQuestionReportCommandHandler(database.Context).Handle(
            new AdminApproveQuestionReportCommand(setup.Report.ReportId, "admin-1"),
            CancellationToken.None);

        Assert.True(approval.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAlreadyHandled, approval.Error);
        var incident = await database.Context.QuestionReportIncidents.SingleAsync();
        Assert.Equal("PendingAdminReview", incident.Status);
        Assert.Equal(3, await database.Context.QuestionVersions.CountAsync());
    }

    [Fact]
    public async Task SubmitWithoutAdmin_ForScoreAdjustment_LeavesIncidentPendingForDurableRecovery()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "incident-score-adjustment-question");
        var version = AddVersion(database, question, 1);
        await database.Context.SaveChangesAsync();

        var reportResult = await new ReportQuestionCommandHandler(database.Context).Handle(
            new ReportQuestionCommand(
                question.QuestionId,
                new ReportQuestionRequest { ReportReason = "The answer is incorrect." },
                "student-1",
                "Student",
                "session-1",
                version.VersionId),
            CancellationToken.None);
        Assert.True(reportResult.IsSuccess);
        var incident = await database.Context.QuestionReportIncidents.SingleAsync();
        var report = await database.Context.QuestionReports.SingleAsync();
        var request = CreateRequest(question, version, report, incident, "score-adjustment-submit");
        request.ResolutionAction = "InvalidateAndAwardFull";

        var result = await new SubmitQuestionReportIncidentCommandHandler(database.Context).Handle(
            new SubmitQuestionReportIncidentCommand(incident.IncidentId, request, question.ExpertId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AdjustmentPending", result.Value!.Status);
        Assert.Equal("Pending", result.Value.AdjustmentStatus);
        Assert.Equal("Resolved", (await database.Context.QuestionReports.SingleAsync()).Status);
    }

    [Fact]
    public async Task AdminRejection_ClearsSubmissionIdentitySoTheExpertCanResubmit()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var setup = await CreatePendingAdminIncidentAsync(database, "incident-resubmit-question");
        var request = CreateRequest(setup.Question, setup.Version, setup.Report, setup.Incident, "resubmit-key");
        var handler = new SubmitQuestionReportIncidentCommandHandler(database.Context);

        Assert.True((await handler.Handle(
            new SubmitQuestionReportIncidentCommand(setup.Incident.IncidentId, request, setup.Question.ExpertId),
            CancellationToken.None)).IsSuccess);
        Assert.True((await new AdminRejectQuestionReportCommandHandler(database.Context).Handle(
            new AdminRejectQuestionReportCommand(
                setup.Report.ReportId,
                new AdminRejectQuestionReportRequest { ReviewNote = "Please revise the correction." },
                "admin-1"),
            CancellationToken.None)).IsSuccess);

        var reopened = await database.Context.QuestionReportIncidents.SingleAsync();
        Assert.Equal("Open", reopened.Status);
        Assert.Null(reopened.SubmissionKey);
        Assert.Null(reopened.SubmissionPayloadHash);
        Assert.Null((await database.Context.QuestionReports.SingleAsync()).ProposedStatus);

        var retryRequest = CreateRequest(setup.Question, setup.Version, setup.Report, reopened, "resubmit-key");
        var retry = await handler.Handle(
            new SubmitQuestionReportIncidentCommand(reopened.IncidentId, retryRequest, setup.Question.ExpertId),
            CancellationToken.None);

        Assert.True(retry.IsSuccess);
        Assert.Equal(3, await database.Context.QuestionVersions.CountAsync());
    }

    private static async Task<(Question Question, QuestionVersion Version, QuestionReportIncident Incident, QuestionReport Report)> CreatePendingAdminIncidentAsync(
        QuestionBankInMemoryContext database,
        string questionId)
    {
        var question = await AddQuestionAsync(database, questionId);
        var version = AddVersion(database, question, 1);
        await database.Context.SaveChangesAsync();
        var reportResult = await new ReportQuestionCommandHandler(database.Context).Handle(
            new ReportQuestionCommand(
                question.QuestionId,
                new ReportQuestionRequest { ReportReason = "The answer is incorrect." },
                "admin-1",
                "Admin",
                null,
                version.VersionId),
            CancellationToken.None);
        Assert.True(reportResult.IsSuccess);
        return (
            question,
            version,
            await database.Context.QuestionReportIncidents.SingleAsync(),
            await database.Context.QuestionReports.SingleAsync());
    }

    private static SubmitQuestionReportIncidentRequest CreateRequest(
        Question question,
        QuestionVersion version,
        QuestionReport report,
        QuestionReportIncident incident,
        string submissionKey) => new()
    {
        ExpectedRevision = incident.Revision,
        ExpectedQuestionVersionId = version.VersionId,
        SubmissionKey = submissionKey,
        ResolutionAction = "NoScoreChange",
        ReportDecisions =
        [
            new QuestionReportDecisionRequest
            {
                ReportId = report.ReportId,
                Disposition = "Resolved",
                ReviewNote = "Correction addresses the answer."
            }
        ],
        Correction = CreateCorrection(question)
    };

    private static UpdateQuestionRequest CreateCorrection(Question question) => new()
    {
        QuestionContent = "Corrected question content",
        SolutionContent = "Corrected detailed solution",
        DifficultyId = question.DifficultyId,
        Grade = question.Grade,
        QuestionType = "SINGLE_CHOICE",
        DefaultWeight = 1m,
        Topics = [new CreateQuestionTopicRequest("topic-1", true)],
        Answers =
        [
            new CreateAnswerRequest { AnswerContent = "A", IsCorrect = true },
            new CreateAnswerRequest { AnswerContent = "B", IsCorrect = false }
        ]
    };

    private static async Task<Question> AddQuestionAsync(QuestionBankInMemoryContext database, string questionId)
    {
        var difficulty = new TagDifficulty
        {
            DifficultyId = "difficulty-1",
            DifficultyName = "Easy",
            LevelValue = 1,
            DisplayOrder = 1,
            IsActive = true
        };
        var rootTopic = new TagTopic
        {
            TagId = "root-topic-1",
            TagName = "Root topic",
            Grade = 10,
            DisplayOrder = 1,
            IsActive = true
        };
        var topic = new TagTopic
        {
            TagId = "topic-1",
            ParentTagId = rootTopic.TagId,
            TagName = "Topic",
            Grade = 10,
            DisplayOrder = 1,
            IsActive = true
        };
        var question = new Question
        {
            QuestionId = questionId,
            QuestionContent = "Question content",
            SolutionContent = "Solution content",
            DifficultyId = difficulty.DifficultyId,
            Difficulty = difficulty,
            Grade = 10,
            Status = "Approved",
            QuestionType = "SingleChoice",
            ExpertId = "expert-1",
            DefaultWeight = 1m,
            IsActive = true,
            Answers =
            [
                new Answer { AnswerId = "answer-1", AnswerContent = "A", IsCorrect = true },
                new Answer { AnswerId = "answer-2", AnswerContent = "B", IsCorrect = false }
            ],
            QuestionTopics =
            [
                new QuestionTopic { QuestionTopicId = "question-topic-1", TagId = topic.TagId, Tag = topic, IsPrimary = true }
            ]
        };
        database.Context.TagTopics.Add(rootTopic);
        database.Context.Questions.Add(question);
        await database.Context.SaveChangesAsync();
        return question;
    }

    private static QuestionVersion AddVersion(QuestionBankInMemoryContext database, Question question, int versionNumber)
    {
        var version = new QuestionVersion
        {
            VersionId = $"{question.QuestionId}-v{versionNumber}",
            QuestionId = question.QuestionId,
            QuestionContent = question.QuestionContent,
            QuestionAnswer = question.SolutionContent,
            AnswersSnapshot = "{}",
            VersionNumber = versionNumber,
            SnapshotSchemaVersion = 2,
            CreatedTime = DateTime.UtcNow.AddMinutes(versionNumber),
            ExpertId = question.ExpertId
        };
        database.Context.QuestionVersions.Add(version);
        return version;
    }
}
