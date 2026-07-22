using ClosedXML.Excel;
using System.IO.Compression;
using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Modules.QuestionBank.Errors;

namespace MathInsight.Modules.QuestionBank.Imports;

public interface IQuestionImportWorkbookParser
{
    ParsedQuestionImportWorkbook Parse(Stream stream);
}

public sealed class QuestionImportWorkbookParser : IQuestionImportWorkbookParser
{
    public ParsedQuestionImportWorkbook Parse(Stream stream)
    {
        try
        {
            EnsureArchiveLimits(stream);
            using var workbook = new XLWorkbook(stream);
            EnsureRequiredSheets(workbook);
            EnsureTemplateVersion(workbook.Worksheet("_Meta"));
            EnsureInputSheetRowLimit(workbook.Worksheet("Questions"));
            EnsureInputSheetRowLimit(workbook.Worksheet("Answers"));
            EnsureInputSheetRowLimit(workbook.Worksheet("Parts"));
            EnsureInputSheetRowLimit(workbook.Worksheet("Topics"));

            var issues = new List<QuestionImportIssueResponse>();
            var questions = ReadQuestions(workbook.Worksheet("Questions"), issues);
            var answers = ReadAnswers(workbook.Worksheet("Answers"), issues);
            var parts = ReadParts(workbook.Worksheet("Parts"), issues);
            var topics = ReadTopics(workbook.Worksheet("Topics"), issues);

            var totalRows = questions.Count + answers.Count + parts.Count + topics.Count;
            if (totalRows > QuestionImportConstants.MaxTotalDataRows)
                throw new QuestionImportException(QuestionBankErrors.QuestionImportLimitExceeded);

            return new ParsedQuestionImportWorkbook(questions, answers, parts, topics, issues);
        }
        catch (QuestionImportException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            throw new QuestionImportException(QuestionBankErrors.QuestionImportTemplateInvalid, ex);
        }
    }

    private static void EnsureRequiredSheets(XLWorkbook workbook)
    {
        foreach (var name in QuestionImportConstants.RequiredSheets)
        {
            if (!workbook.Worksheets.Contains(name))
                throw new QuestionImportException(QuestionBankErrors.QuestionImportTemplateInvalid);
        }
    }

    private static void EnsureArchiveLimits(Stream stream)
    {
        if (!stream.CanSeek)
            throw new QuestionImportException(QuestionBankErrors.QuestionImportTemplateInvalid);

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > QuestionImportConstants.MaxArchiveEntries)
                throw new QuestionImportException(QuestionBankErrors.QuestionImportLimitExceeded);

            long totalUncompressedBytes = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > QuestionImportConstants.MaxUncompressedEntryBytes ||
                    entry.Length > QuestionImportConstants.MaxUncompressedArchiveBytes - totalUncompressedBytes)
                {
                    throw new QuestionImportException(QuestionBankErrors.QuestionImportLimitExceeded);
                }

                totalUncompressedBytes += entry.Length;
            }
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static void EnsureInputSheetRowLimit(IXLWorksheet worksheet)
    {
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow - 1 > QuestionImportConstants.MaxDataRowsPerSheet)
            throw new QuestionImportException(QuestionBankErrors.QuestionImportLimitExceeded);
    }

    private static void EnsureTemplateVersion(IXLWorksheet worksheet)
    {
        var key = worksheet.Cell(1, 1).GetString().Trim();
        var version = worksheet.Cell(1, 2).GetString().Trim();
        if (!string.Equals(key, "TemplateVersion", StringComparison.OrdinalIgnoreCase))
            throw new QuestionImportException(QuestionBankErrors.QuestionImportTemplateInvalid);

        if (!string.Equals(version, QuestionImportConstants.TemplateVersion, StringComparison.Ordinal))
            throw new QuestionImportException(QuestionBankErrors.QuestionImportTemplateVersionUnsupported);
    }

    private static List<RawQuestionRow> ReadQuestions(
        IXLWorksheet worksheet,
        List<QuestionImportIssueResponse> issues)
    {
        var headers = ReadHeaders(worksheet, QuestionImportConstants.QuestionHeaders);
        var rows = new List<RawQuestionRow>();
        foreach (var row in DataRows(worksheet))
        {
            EnsureNoFormula(row, worksheet.Name, headers, issues);
            if (IsEmpty(row, headers.Values))
                continue;

            rows.Add(new RawQuestionRow(
                Text(row, headers, "QuestionKey"),
                row.RowNumber(),
                Text(row, headers, "QuestionContent"),
                Text(row, headers, "SolutionContent"),
                Text(row, headers, "QuestionType"),
                Text(row, headers, "Grade"),
                Text(row, headers, "DifficultyLevel"),
                Text(row, headers, "DefaultWeight"),
                Text(row, headers, "PictureUrl")));
        }

        return rows;
    }

    private static List<RawAnswerRow> ReadAnswers(
        IXLWorksheet worksheet,
        List<QuestionImportIssueResponse> issues)
    {
        var headers = ReadHeaders(worksheet, QuestionImportConstants.AnswerHeaders);
        var rows = new List<RawAnswerRow>();
        foreach (var row in DataRows(worksheet))
        {
            EnsureNoFormula(row, worksheet.Name, headers, issues);
            if (IsEmpty(row, headers.Values))
                continue;

            rows.Add(new RawAnswerRow(
                Text(row, headers, "QuestionKey"),
                row.RowNumber(),
                Text(row, headers, "AnswerContent"),
                Text(row, headers, "IsCorrect")));
        }

        return rows;
    }

    private static List<RawPartRow> ReadParts(
        IXLWorksheet worksheet,
        List<QuestionImportIssueResponse> issues)
    {
        var headers = ReadHeaders(worksheet, QuestionImportConstants.PartHeaders);
        var rows = new List<RawPartRow>();
        foreach (var row in DataRows(worksheet))
        {
            EnsureNoFormula(row, worksheet.Name, headers, issues);
            if (IsEmpty(row, headers.Values))
                continue;

            rows.Add(new RawPartRow(
                Text(row, headers, "QuestionKey"),
                row.RowNumber(),
                Text(row, headers, "PartOrder"),
                Text(row, headers, "PartLabel"),
                Text(row, headers, "PartContent"),
                Text(row, headers, "PartType"),
                Text(row, headers, "CorrectBoolean"),
                Text(row, headers, "CorrectText"),
                Text(row, headers, "CorrectNumeric"),
                Text(row, headers, "NumericTolerance"),
                Text(row, headers, "Explanation"),
                Text(row, headers, "DefaultWeight")));
        }

        return rows;
    }

    private static List<RawTopicRow> ReadTopics(
        IXLWorksheet worksheet,
        List<QuestionImportIssueResponse> issues)
    {
        var headers = ReadHeaders(worksheet, QuestionImportConstants.TopicHeaders);
        var rows = new List<RawTopicRow>();
        foreach (var row in DataRows(worksheet))
        {
            EnsureNoFormula(row, worksheet.Name, headers, issues);
            if (IsEmpty(row, headers.Values))
                continue;

            rows.Add(new RawTopicRow(
                Text(row, headers, "QuestionKey"),
                row.RowNumber(),
                Text(row, headers, "TopicName"),
                Text(row, headers, "IsPrimary")));
        }

        return rows;
    }

    private static Dictionary<string, int> ReadHeaders(IXLWorksheet worksheet, IEnumerable<string> requiredHeaders)
    {
        var lastCell = worksheet.LastCellUsed();
        if (lastCell is null)
            throw new QuestionImportException(QuestionBankErrors.QuestionImportTemplateInvalid);

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= lastCell.Address.ColumnNumber; column++)
        {
            var value = worksheet.Cell(1, column).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(value) && !headers.TryAdd(value, column))
                throw new QuestionImportException(QuestionBankErrors.QuestionImportTemplateInvalid);
        }

        if (requiredHeaders.Any(header => !headers.ContainsKey(header)))
            throw new QuestionImportException(QuestionBankErrors.QuestionImportTemplateInvalid);

        return headers;
    }

    private static IEnumerable<IXLRow> DataRows(IXLWorksheet worksheet)
    {
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        return Enumerable.Range(2, Math.Max(lastRow - 1, 0)).Select(worksheet.Row);
    }

    private static void EnsureNoFormula(
        IXLRow row,
        string sheet,
        IReadOnlyDictionary<string, int> headers,
        List<QuestionImportIssueResponse> issues)
    {
        foreach (var header in headers)
        {
            var cell = row.Cell(header.Value);
            if (string.IsNullOrWhiteSpace(cell.FormulaA1))
                continue;

            issues.Add(new QuestionImportIssueResponse(
                QuestionBankErrors.QuestionImportFormulaNotAllowed.Code,
                QuestionBankErrors.QuestionImportFormulaNotAllowed.Message,
                sheet,
                row.RowNumber(),
                header.Key,
                Text(row, headers, "QuestionKey")));
        }
    }

    private static bool IsEmpty(IXLRow row, IEnumerable<int> columns) =>
        columns.All(column => string.IsNullOrWhiteSpace(row.Cell(column).GetString()));

    private static string Text(IXLRow row, IReadOnlyDictionary<string, int> headers, string header) =>
        headers.TryGetValue(header, out var column)
            ? row.Cell(column).GetString().Trim()
            : string.Empty;
}

public sealed record ParsedQuestionImportWorkbook(
    IReadOnlyList<RawQuestionRow> Questions,
    IReadOnlyList<RawAnswerRow> Answers,
    IReadOnlyList<RawPartRow> Parts,
    IReadOnlyList<RawTopicRow> Topics,
    IReadOnlyList<QuestionImportIssueResponse> ParserIssues);

public sealed record RawQuestionRow(
    string QuestionKey,
    int SourceRow,
    string QuestionContent,
    string SolutionContent,
    string QuestionType,
    string Grade,
    string DifficultyLevel,
    string DefaultWeight,
    string PictureUrl);

public sealed record RawAnswerRow(string QuestionKey, int SourceRow, string AnswerContent, string IsCorrect);

public sealed record RawPartRow(
    string QuestionKey,
    int SourceRow,
    string PartOrder,
    string PartLabel,
    string PartContent,
    string PartType,
    string CorrectBoolean,
    string CorrectText,
    string CorrectNumeric,
    string NumericTolerance,
    string Explanation,
    string DefaultWeight);

public sealed record RawTopicRow(string QuestionKey, int SourceRow, string TopicName, string IsPrimary);

public sealed class QuestionImportException : Exception
{
    public QuestionImportException(MathInsight.Shared.Results.Error error, Exception? innerException = null)
        : base(error.Message, innerException)
    {
        Error = error;
    }

    public MathInsight.Shared.Results.Error Error { get; }
}
