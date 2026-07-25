using MathInsight.Modules.Gamification.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Gamification.Persistence.Configurations;

/// <summary>
/// Maps <see cref="StudyStreak"/> to DB table [StudyStreak]. One row per student (StudentID
/// UNIQUE, 1:1). LastActivityDate maps to the DATE column. BR-39..BR-42.
/// </summary>
public class StudyStreakConfiguration : IEntityTypeConfiguration<StudyStreak>
{
    public void Configure(EntityTypeBuilder<StudyStreak> builder)
    {
        builder.ToTable(nameof(StudyStreak));

        builder.HasKey(studyStreak => studyStreak.StreakId);

        builder.Property(studyStreak => studyStreak.StreakId)
            .HasColumnName("StreakID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(studyStreak => studyStreak.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(studyStreak => studyStreak.CurrentStreak)
            .HasColumnName("CurrentStreak");

        builder.Property(studyStreak => studyStreak.LongestStreak)
            .HasColumnName("LongestStreak");

        builder.Property(studyStreak => studyStreak.LastActivityDate)
            .HasColumnName("LastActivityDate")
            .HasColumnType("date");

        // 1:1 per student (BR-39). Matches the inline UNIQUE on StudentID in the DDL.
        builder.HasIndex(studyStreak => studyStreak.StudentId)
            .IsUnique()
            .HasDatabaseName("UQ_StudyStreak_StudentID");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_StudyStreak_Values",
                "[CurrentStreak] >= 0 AND [LongestStreak] >= 0 AND [CurrentStreak] <= [LongestStreak]");
        });
    }
}
