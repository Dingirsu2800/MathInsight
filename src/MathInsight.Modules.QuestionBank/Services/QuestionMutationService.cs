using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Modules.QuestionBank.Validation;
using MathInsight.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Services;

/// <summary>
/// Applies a validated expert edit and creates its immutable snapshot. The caller owns the transaction.
/// </summary>
public sealed class QuestionMutationService
{
    private readonly QuestionBankDbContext _context;

    public QuestionMutationService(QuestionBankDbContext context) => _context = context;

    public async Task<Result<QuestionMutationResult>> ApplyAsync(
        string questionId,
        UpdateQuestionRequest request,
        string expertId,
        CancellationToken cancellationToken,
        bool rejectPendingAdminReview = true)
    {
        var validationError = QuestionRequestValidator.Validate(ToCreateRequest(request), out var questionType);
        if (validationError is not null)
            return Result<QuestionMutationResult>.Failure(validationError);

        var question = await _context.Questions
            .Include(item => item.Answers.Where(answer => !answer.IsArchived))
            .Include(item => item.Parts.Where(part => !part.IsArchived))
            .Include(item => item.QuestionTopics)
            .FirstOrDefaultAsync(item => item.QuestionId == questionId, cancellationToken);
        if (question is null)
            return Result<QuestionMutationResult>.Failure(QuestionBankErrors.QuestionNotFound);
        if (!string.Equals(question.ExpertId, expertId, StringComparison.OrdinalIgnoreCase))
            return Result<QuestionMutationResult>.Failure(QuestionBankErrors.QuestionUpdateForbidden);

        if (rejectPendingAdminReview && await _context.QuestionReportIncidents.AnyAsync(
                item => item.QuestionId == questionId && item.RequiresAdminReview &&
                        (item.Status == "Open" || item.Status == "PendingAdminReview"),
                cancellationToken))
        {
            return Result<QuestionMutationResult>.Failure(QuestionBankErrors.AdminReportRequiresReview);
        }

        var referenceError = await QuestionReferenceValidator.ValidateAsync(_context, ToCreateRequest(request), cancellationToken);
        if (referenceError is not null)
            return Result<QuestionMutationResult>.Failure(referenceError);

        _context.QuestionTopics.RemoveRange(question.QuestionTopics.ToList());
        foreach (var answer in question.Answers) answer.IsArchived = true;
        foreach (var part in question.Parts) part.IsArchived = true;
        question.QuestionTopics.Clear();

        question.QuestionContent = request.QuestionContent;
        question.SolutionContent = request.SolutionContent;
        question.PictureUrl = request.PictureUrl;
        question.DifficultyId = request.DifficultyId;
        question.Grade = request.Grade;
        question.QuestionType = questionType!;
        question.DefaultWeight = request.DefaultWeight;
        question.UpdatedTime = DateTime.UtcNow;

        foreach (var topic in request.Topics)
            question.QuestionTopics.Add(new QuestionTopic { QuestionTopicId = Guid.NewGuid().ToString(), QuestionId = question.QuestionId, TagId = topic.TagId, IsPrimary = topic.IsPrimary });

        if (questionType == "Composite")
        {
            foreach (var part in request.Parts)
                question.Parts.Add(new QuestionPart { PartId = Guid.NewGuid().ToString(), QuestionId = question.QuestionId, PartOrder = part.PartOrder, PartLabel = part.PartLabel, PartContent = part.PartContent, PartType = MapPartType(part.PartType)!, CorrectBoolean = part.CorrectBoolean, CorrectText = part.CorrectText, CorrectNumeric = part.CorrectNumeric, NumericTolerance = part.NumericTolerance, Explanation = part.Explanation, DefaultWeight = part.DefaultWeight, IsArchived = false });
        }
        else
        {
            foreach (var answer in request.Answers)
                question.Answers.Add(new Answer { AnswerId = Guid.NewGuid().ToString(), QuestionId = question.QuestionId, AnswerContent = answer.AnswerContent, IsCorrect = answer.IsCorrect, IsArchived = false });
        }

        var versionNumber = (await _context.QuestionVersions.Where(item => item.QuestionId == questionId).Select(item => (int?)item.VersionNumber).MaxAsync(cancellationToken) ?? 0) + 1;
        var version = QuestionVersionSnapshotFactory.Create(question, expertId, versionNumber, question.UpdatedTime);
        _context.QuestionVersions.Add(version);
        return Result<QuestionMutationResult>.Success(new QuestionMutationResult(question, version));
    }

    private static CreateQuestionRequest ToCreateRequest(UpdateQuestionRequest request) => new()
    {
        QuestionContent = request.QuestionContent, SolutionContent = request.SolutionContent, PictureUrl = request.PictureUrl,
        DifficultyId = request.DifficultyId, Grade = request.Grade, QuestionType = request.QuestionType,
        DefaultWeight = request.DefaultWeight, Topics = request.Topics, Answers = request.Answers, Parts = request.Parts
    };

    private static string? MapPartType(string? type) => type?.Trim().ToUpperInvariant() switch
    {
        "TRUE_FALSE" or "TRUEFALSE" => "TrueFalse",
        "SHORT_ANSWER" or "SHORTANSWER" => "ShortAnswer",
        "NUMERIC_ANSWER" or "NUMERICANSWER" => "NumericAnswer",
        _ => null
    };
}

public sealed record QuestionMutationResult(Question Question, QuestionVersion Version);
