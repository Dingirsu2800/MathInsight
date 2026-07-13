using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Entities;
using System.Reflection;

namespace MathInsight.Modules.Learning_Lecture.Persistence;

public class LearningDbContext : DbContext
{
    public LearningDbContext(DbContextOptions<LearningDbContext> options) : base(options)
    {
    }

    public DbSet<Lecture> Lectures => Set<Lecture>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<LectureMaterial> LectureMaterials => Set<LectureMaterial>();
    public DbSet<LectureLike> LectureLikes => Set<LectureLike>();
    public DbSet<DiscussionQuestion> DiscussionQuestions => Set<DiscussionQuestion>();
    public DbSet<DiscussionAnswer> DiscussionAnswers => Set<DiscussionAnswer>();
    public DbSet<DiscussionReport> DiscussionReports => Set<DiscussionReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Ensure this only applies configurations from this assembly/module
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly(), 
            t => t.Namespace != null && t.Namespace.Contains("Learning_Lecture"));
    }
}
