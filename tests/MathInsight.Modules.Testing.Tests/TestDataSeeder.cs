using System.Text.Json;
using MathInsight.Modules.Testing.Entities;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Questions;

namespace MathInsight.Modules.Testing.Tests;

/// <summary>
/// Provides reusable seed data for integration tests.
/// Creates a deterministic test scenario with known IDs to make assertions predictable.
/// </summary>
internal static class TestDataSeeder
{
    // ─── Well-known IDs ─────────────────────────────────────────────────────
    public static readonly string ActiveTestId = "00000000-0000-0000-0000-000000000001";
    public static readonly string ArchivedTestId = "00000000-0000-0000-0000-000000000002";
    public static readonly string SharedTestId = "00000000-0000-0000-0000-000000000003";
    public static readonly string BlueprintId = "00000000-0000-0000-0000-000000000004";
    public static readonly string StudentId = "00000000-0000-0000-0000-000000000010";
    public static readonly string OtherStudentId = "00000000-0000-0000-0000-000000000020";
    public static readonly string Question1Id = "00000000-0000-0000-0000-000000000101";
    public static readonly string Question2Id = "00000000-0000-0000-0000-000000000102";
    public static readonly string Question3Id = "00000000-0000-0000-0000-000000000103";
    public static readonly string Question4Id = "00000000-0000-0000-0000-000000000104";
    public static readonly string Question5Id = "00000000-0000-0000-0000-000000000105";
    public static readonly string Answer1Id = "00000000-0000-0000-0000-000000000201";

    /// <summary>
    /// Seeds a minimal dataset: one Active personal practice test with 5 questions and one Archived test.
    /// </summary>
    public static async Task SeedActiveTestWithQuestions(TestingDbContext db)
    {
        db.Students.AddRange(
            new Student { StudentId = StudentId, CurrentGrade = 10 },
            new Student { StudentId = OtherStudentId, CurrentGrade = 10 });

        var activeTest = new Test
        {
            TestId = ActiveTestId,
            TestStatus = "Active",
            TestMode = "AdaptivePractice", // maps to Practice format
            GeneratedForStudentId = StudentId,
            TestName = "Practice Test 1",
            DurationMinutes = 60,
            TotalQuestions = 5,
            MaxScore = 10m,
            ScoringPolicy = "NormalizedWeight",
            CreatedTime = DateTime.UtcNow.AddDays(-1),
            GeneratedBy = "System"
        };

        db.Tests.Add(activeTest);

        var questionIds = new[] { Question1Id, Question2Id, Question3Id, Question4Id, Question5Id };
        for (int i = 0; i < questionIds.Length; i++)
        {
            var versionId = $"version-{i + 1}";
            var snapshot = CreateSnapshot(questionIds[i], i);
            db.QuestionVersions.Add(new QuestionVersion
            {
                VersionId = versionId,
                QuestionId = questionIds[i],
                QuestionContent = snapshot.QuestionContent!,
                QuestionAnswer = "Test solution",
                AnswersSnapshot = JsonSerializer.Serialize(snapshot),
                SnapshotSchemaVersion = 2
            });
            db.TestQuestions.Add(new TestQuestion
            {
                TestId = ActiveTestId,
                QuestionId = questionIds[i],
                QuestionOrder = i + 1,
                SelectionReason = "BlueprintNormal",
                QuestionVersionId = versionId,
                WeightSnapshot = 1m,
                MaxPointsSnapshot = 2m,
                ScoringRuleSnapshot = "AllOrNothing"
            });
        }

        var archivedTest = new Test
        {
            TestId = ArchivedTestId,
            TestStatus = "Archived",
            TestMode = "AdaptivePractice",
            GeneratedForStudentId = StudentId,
            TestName = "Archived Test",
            DurationMinutes = 90,
            TotalQuestions = 3,
            CreatedTime = DateTime.UtcNow.AddDays(-30),
            GeneratedBy = "System"
        };

        db.Tests.Add(archivedTest);

        await db.SaveChangesAsync();
    }

    public static async Task SeedSharedBlueprintExam(
        TestingDbContext db,
        int blueprintGrade = 10,
        string blueprintStatus = "Active",
        string testMode = "BlueprintExam")
    {
        await SeedActiveTestWithQuestions(db);

        db.Blueprints.Add(new Blueprint
        {
            BlueprintId = BlueprintId,
            BlueprintName = "Shared Grade 10 Exam",
            Grade = blueprintGrade,
            Status = blueprintStatus
        });

        db.Tests.Add(new Test
        {
            TestId = SharedTestId,
            BlueprintId = BlueprintId,
            TestStatus = "Active",
            TestMode = testMode,
            GeneratedForStudentId = null,
            GeneratedBy = "System",
            TestName = "Shared Blueprint Exam",
            DurationMinutes = 45,
            TotalQuestions = 1,
            MaxScore = 10m,
            ScoringPolicy = "NormalizedWeight",
            CreatedTime = DateTime.UtcNow
        });

        db.TestQuestions.Add(new TestQuestion
        {
            TestId = SharedTestId,
            QuestionId = Question1Id,
            QuestionOrder = 1,
            SelectionReason = "BlueprintNormal",
            QuestionVersionId = "version-1",
            WeightSnapshot = 1m,
            MaxPointsSnapshot = 10m,
            ScoringRuleSnapshot = "AllOrNothing"
        });

        await db.SaveChangesAsync();
    }

    private static QuestionSnapshotV2 CreateSnapshot(string questionId, int index)
    {
        IReadOnlyList<QuestionAnswerSnapshot> answers = index switch
        {
            0 => [new(Answer1Id, "Answer 1", true)],
            1 => [new("ans-2", "Answer 2", true)],
            2 => [new("opt-a", "Option A", true), new("opt-b", "Option B", true)],
            4 => [new("ans-5", "Answer 5", true)],
            _ => []
        };
        IReadOnlyList<QuestionPartSnapshot> parts = index == 3
            ?
            [
                new("part-1", 1, "a", "Statement 1", "Boolean", true, null, null, null, null, 1m),
                new("part-2", 2, "b", "Statement 2", "Text", null, "answer text", null, null, null, 1m)
            ]
            : [];

        return new QuestionSnapshotV2(
            questionId,
            index == 3 ? "COMPOSITE" : "SINGLE_CHOICE",
            "DIFF-MEDIUM",
            12,
            1m,
            [new QuestionTopicSnapshot("TOPIC-G12-TEST", true)],
            answers,
            parts,
            $"Immutable question {index + 1}");
    }
}
