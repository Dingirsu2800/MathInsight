namespace MathInsight.Modules.QuestionBank.Imports;

internal static class QuestionImportConstants
{
    public const string TemplateVersion = "1";
    public const int MaxFileBytes = 20 * 1024 * 1024;
    public const int MaxQuestions = 100;
    public const int MaxTotalDataRows = 5000;
    public const int MaxDataRowsPerSheet = MaxTotalDataRows;
    public const int MaxArchiveEntries = 200;
    public const long MaxUncompressedArchiveBytes = 100L * 1024 * 1024;
    public const long MaxUncompressedEntryBytes = 50L * 1024 * 1024;
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static readonly string[] RequiredSheets =
    [
        "_Meta", "Instructions", "Questions", "Answers", "Parts", "Topics", "Catalogs"
    ];

    public static readonly string[] QuestionHeaders =
    [
        "QuestionKey", "QuestionContent", "SolutionContent", "QuestionType", "Grade",
        "DifficultyLevel", "DefaultPoint", "PictureUrl"
    ];

    public static readonly string[] AnswerHeaders =
    ["QuestionKey", "AnswerContent", "IsCorrect"];

    public static readonly string[] PartHeaders =
    [
        "QuestionKey", "PartOrder", "PartLabel", "PartContent", "PartType",
        "CorrectBoolean", "CorrectText", "CorrectNumeric", "NumericTolerance",
        "Explanation", "DefaultPoint"
    ];

    public static readonly string[] TopicHeaders =
    ["QuestionKey", "TopicName", "IsPrimary"];
}
