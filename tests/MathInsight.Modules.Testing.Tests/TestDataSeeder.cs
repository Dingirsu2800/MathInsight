using MathInsight.Modules.Testing.Entities;
using MathInsight.Modules.Testing.Persistence;

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
    public static readonly string StudentId = "00000000-0000-0000-0000-000000000010";
    public static readonly string OtherStudentId = "00000000-0000-0000-0000-000000000020";
    public static readonly string Question1Id = "00000000-0000-0000-0000-000000000101";
    public static readonly string Question2Id = "00000000-0000-0000-0000-000000000102";
    public static readonly string Question3Id = "00000000-0000-0000-0000-000000000103";
    public static readonly string Question4Id = "00000000-0000-0000-0000-000000000104";
    public static readonly string Question5Id = "00000000-0000-0000-0000-000000000105";
    public static readonly string Answer1Id = "00000000-0000-0000-0000-000000000201";

    /// <summary>
    /// Seeds a minimal dataset: one ACTIVE practice test with 5 questions and one ARCHIVED test.
    /// </summary>
    public static async Task SeedActiveTestWithQuestions(TestingDbContext db)
    {
        var activeTest = new Test
        {
            TestId = ActiveTestId,
            TestStatus = "ACTIVE",
            TestMode = "AdaptivePractice", // maps to Practice format
            TestName = "Practice Test 1",
            DurationMinutes = 60,
            TotalQuestions = 5,
            CreatedTime = DateTime.UtcNow.AddDays(-1),
            GeneratedBy = "System"
        };

        db.Tests.Add(activeTest);

        var questionIds = new[] { Question1Id, Question2Id, Question3Id, Question4Id, Question5Id };
        for (int i = 0; i < questionIds.Length; i++)
        {
            db.TestQuestions.Add(new TestQuestion
            {
                TestId = ActiveTestId,
                QuestionId = questionIds[i],
                QuestionOrder = i + 1,
                SelectionReason = "BlueprintNormal"
            });
        }

        var archivedTest = new Test
        {
            TestId = ArchivedTestId,
            TestStatus = "ARCHIVED",
            TestMode = "BlueprintExam",
            TestName = "Archived Test",
            DurationMinutes = 90,
            TotalQuestions = 3,
            CreatedTime = DateTime.UtcNow.AddDays(-30),
            GeneratedBy = "System"
        };

        db.Tests.Add(archivedTest);

        await db.SaveChangesAsync();
    }
}
