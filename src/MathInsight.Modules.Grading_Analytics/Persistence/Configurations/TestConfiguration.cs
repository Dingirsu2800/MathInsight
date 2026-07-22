using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TestConfiguration : IEntityTypeConfiguration<Test>
{
    public void Configure(EntityTypeBuilder<Test> builder)
    {
        builder.ToTable("Test");
        builder.HasKey(x => x.TestId);

        builder.Property(x => x.TestId).HasColumnName("TestID");
        builder.Property(x => x.MaxScore).HasColumnName("MaxScore").HasPrecision(5, 2);
        builder.Property(x => x.ScoringPolicy).HasColumnName("ScoringPolicy").HasMaxLength(30);
    }
}
