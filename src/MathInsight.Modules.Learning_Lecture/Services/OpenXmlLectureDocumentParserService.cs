using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Learning_Lecture.Services;

public sealed class OpenXmlLectureDocumentParserService : ILectureDocumentParserService
{
    public async Task<Result<string>> ParseDocxAsync(Stream docxStream, CancellationToken cancellationToken)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await docxStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            using var wordDocument = WordprocessingDocument.Open(memoryStream, false);
            var body = wordDocument.MainDocumentPart?.Document.Body;

            if (body is null)
                return Result<string>.Failure(new Error("DocxParser.Empty", "The document is empty or invalid."));

            var sb = new StringBuilder();

            foreach (var element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    sb.AppendLine(paragraph.InnerText);
                }
                else if (element is Table table)
                {
                    foreach (var row in table.Elements<TableRow>())
                    {
                        var rowText = string.Join(" | ", row.Elements<TableCell>().Select(c => c.InnerText));
                        sb.AppendLine($"| {rowText} |");
                    }
                    sb.AppendLine();
                }
            }

            var resultText = sb.ToString().Trim();
            
            if (string.IsNullOrWhiteSpace(resultText))
                return Result<string>.Failure(new Error("DocxParser.NoText", "Could not extract any text from the document."));

            return Result<string>.Success(resultText);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(new Error("DocxParser.Error", $"Failed to parse document: {ex.Message}"));
        }
    }
}
