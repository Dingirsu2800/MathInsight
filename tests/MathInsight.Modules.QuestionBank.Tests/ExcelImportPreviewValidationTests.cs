using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MathInsight.Modules.QuestionBank.Commands.PreviewQuestionImport;
using MathInsight.Modules.QuestionBank.Imports;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class ExcelImportPreviewValidationTests
{
    private static string? GetDatasetRoot()
    {
        var envRoot = Environment.GetEnvironmentVariable("QUESTION_IMPORT_DATASET_ROOT");
        if (!string.IsNullOrEmpty(envRoot) && Directory.Exists(envRoot))
            return envRoot;

        return null;
    }

    [Theory(Skip = "Opt-in dataset integration test. Set QUESTION_IMPORT_DATASET_ROOT environment variable to enable.")]
    [InlineData("2023", 50)]
    [InlineData("2024", 50)]
    [InlineData("2025", 22)]
    [InlineData("2026", 22)]
    public async Task Preview_FullWorkbooks_PassAllBackendValidationWithoutErrors(string year, int expectedCount)
    {
        var datasetRoot = GetDatasetRoot();
        if (string.IsNullOrEmpty(datasetRoot) || !Directory.Exists(datasetRoot))
        {
            return;
        }

        var excelPath = Path.Combine(datasetRoot, year, $"MathInsight_THPT_{year}_v3.xlsx");
        if (!File.Exists(excelPath))
        {
            return;
        }

        await RunValidationOnExcel(excelPath, expectedCount);
    }

    [Theory(Skip = "Opt-in dataset integration test. Set QUESTION_IMPORT_DATASET_ROOT environment variable to enable.")]
    [InlineData("MM2026-D001", 22)]
    [InlineData("MM2026-D002", 22)]
    public async Task Preview_MathMaterial_Batch1Workbooks_PassAllBackendValidation(string examKey, int expectedCount)
    {
        var datasetRoot = GetDatasetRoot();
        if (string.IsNullOrEmpty(datasetRoot) || !Directory.Exists(datasetRoot))
        {
            return;
        }

        var mathMatRoot = Path.Combine(datasetRoot, "math-material");
        var excelPath = Path.Combine(mathMatRoot, examKey, $"MathInsight_{examKey}_v3.xlsx");
        if (!File.Exists(excelPath))
        {
            return;
        }

        await RunValidationOnExcel(excelPath, expectedCount);
    }

    private static async Task RunValidationOnExcel(string excelPath, int expectedCount)
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();

        database.Context.TagDifficulties.AddRange(
            new Entities.TagDifficulty { DifficultyId = "DIFF-LEVEL-1", DifficultyName = "Nhận biết", LevelValue = 1, DisplayOrder = 1, IsActive = true },
            new Entities.TagDifficulty { DifficultyId = "DIFF-LEVEL-2", DifficultyName = "Thông hiểu", LevelValue = 2, DisplayOrder = 2, IsActive = true },
            new Entities.TagDifficulty { DifficultyId = "DIFF-LEVEL-3", DifficultyName = "Vận dụng", LevelValue = 3, DisplayOrder = 3, IsActive = true },
            new Entities.TagDifficulty { DifficultyId = "DIFF-LEVEL-4", DifficultyName = "Vận dụng cao", LevelValue = 4, DisplayOrder = 4, IsActive = true }
        );

        database.Context.TagTopics.AddRange(
            new Entities.TagTopic { TagId = "TOPIC-G10-SET", TagName = "Lớp 10 - Mệnh đề, tập hợp", Grade = 10, DisplayOrder = 1, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G10-INEQ", TagName = "Lớp 10 - Bất phương trình", Grade = 10, DisplayOrder = 2, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G11-PROG", TagName = "Lớp 11 - Cấp số", Grade = 11, DisplayOrder = 3, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G11-COMB", TagName = "Lớp 11 - Tổ hợp, xác suất", Grade = 11, DisplayOrder = 4, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-DERIVAPP", TagName = "Lớp 12 - Ứng dụng đạo hàm", Grade = 12, DisplayOrder = 1, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-EXPLOG", TagName = "Lớp 12 - Mũ và logarit", Grade = 12, DisplayOrder = 2, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-INTEGRAL", TagName = "Lớp 12 - Nguyên hàm, tích phân", Grade = 12, DisplayOrder = 3, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-COMPLEX", TagName = "Lớp 12 - Số phức", Grade = 12, DisplayOrder = 4, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-POLY", TagName = "Lớp 12 - Khối đa diện", Grade = 12, DisplayOrder = 5, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-REV", TagName = "Lớp 12 - Mặt tròn xoay", Grade = 12, DisplayOrder = 6, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-OXYZ", TagName = "Lớp 12 - Tọa độ Oxyz", Grade = 12, DisplayOrder = 7, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-LINEPLANE", TagName = "Lớp 12 - Mặt phẳng, đường thẳng", Grade = 12, DisplayOrder = 8, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-SPHERE", TagName = "Lớp 12 - Mặt cầu", Grade = 12, DisplayOrder = 9, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-CONPROB", TagName = "Lớp 12 - Xác suất có điều kiện", Grade = 12, DisplayOrder = 10, IsActive = true },
            new Entities.TagTopic { TagId = "TOPIC-G12-DATA", TagName = "Lớp 12 - Thống kê", Grade = 12, DisplayOrder = 11, IsActive = true }
        );

        await database.Context.SaveChangesAsync();

        var handler = new PreviewQuestionImportCommandHandler(
            new QuestionImportWorkbookParser(),
            new QuestionImportValidationService(database.Context));

        await using var stream = File.OpenRead(excelPath);
        var file = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(excelPath));

        var result = await handler.Handle(new PreviewQuestionImportCommand(file), CancellationToken.None);

        Assert.True(result.IsSuccess, $"Preview failed for {excelPath}");
        Assert.NotNull(result.Value);
        Assert.Equal(expectedCount, result.Value!.TotalCount);
        Assert.Equal(expectedCount, result.Value.ValidCount);
        Assert.Equal(0, result.Value.InvalidCount);
        Assert.Empty(result.Value.FileErrors);

        foreach (var item in result.Value.Items)
        {
            Assert.True(item.IsValid, $"Question {item.QuestionKey} in {excelPath} is invalid: {string.Join(", ", item.Errors.Select(e => e.Message))}");
            Assert.Empty(item.Errors);
            Assert.NotNull(item.Draft);
        }
    }
}
