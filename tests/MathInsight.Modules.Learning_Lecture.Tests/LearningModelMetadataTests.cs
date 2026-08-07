using MathInsight.Modules.Learning_Lecture.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace MathInsight.Modules.Learning_Lecture.Tests;

public sealed class LearningModelMetadataTests
{
    [Fact]
    public async Task LearningModel_MapsLectureDifficultyAndExcludesTaxonomyReadModels()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        var model = database.Context.GetService<IDesignTimeModel>().Model;

        var lectureType = model.FindEntityType(typeof(Lecture));
        Assert.NotNull(lectureType);

        var difficultyProperty = lectureType!.FindProperty(nameof(Lecture.DifficultyId));
        Assert.NotNull(difficultyProperty);
        Assert.Equal("DifficultyID", difficultyProperty!.GetColumnName());
        Assert.Equal(36, difficultyProperty.GetMaxLength());
        Assert.False(difficultyProperty.IsUnicode());

        var topicType = model.FindEntityType(typeof(TagTopicReadOnly));
        var difficultyType = model.FindEntityType(typeof(TagDifficultyReadOnly));
        Assert.NotNull(topicType);
        Assert.NotNull(difficultyType);
        Assert.True(topicType!.IsTableExcludedFromMigrations());
        Assert.True(difficultyType!.IsTableExcludedFromMigrations());
    }
}
