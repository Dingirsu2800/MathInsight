using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MathInsight.Modules.QuestionBank.Contracts.Reports;

namespace MathInsight.Modules.QuestionBank.Commands.SubmitQuestionReportIncident;

internal static class IncidentSubmissionFingerprint
{
    public static string Create(SubmitQuestionReportIncidentRequest request)
    {
        var canonical = new
        {
            request.ExpectedRevision,
            ExpectedQuestionVersionId = request.ExpectedQuestionVersionId.Trim(),
            ResolutionAction = request.ResolutionAction.Trim(),
            Decisions = request.ReportDecisions
                .OrderBy(item => item.ReportId, StringComparer.Ordinal)
                .Select(item => new { ReportId = item.ReportId.Trim(), Disposition = item.Disposition.Trim(), ReviewNote = item.ReviewNote?.Trim() }),
            request.Correction.QuestionContent,
            request.Correction.SolutionContent,
            request.Correction.PictureUrl,
            request.Correction.DifficultyId,
            request.Correction.Grade,
            request.Correction.QuestionType,
            request.Correction.DefaultWeight,
            Topics = request.Correction.Topics.OrderBy(item => item.TagId, StringComparer.Ordinal).Select(item => new { item.TagId, item.IsPrimary }),
            Answers = request.Correction.Answers.OrderBy(item => item.AnswerContent, StringComparer.Ordinal).Select(item => new { item.AnswerContent, item.IsCorrect }),
            Parts = request.Correction.Parts.OrderBy(item => item.PartOrder).Select(item => new { item.PartOrder, item.PartLabel, item.PartContent, item.PartType, item.CorrectBoolean, item.CorrectText, item.CorrectNumeric, item.NumericTolerance, item.Explanation, item.DefaultWeight })
        };
        var json = JsonSerializer.Serialize(canonical);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
