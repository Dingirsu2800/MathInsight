using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MathInsight.Modules.Learning_Lecture.Persistence;

public class LearningDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LearningDbContext>();
        
        // SECURITY WARNING: Never hardcode production connection strings here.
        // This is only used for local EF Core Migrations.
        optionsBuilder.UseSqlServer("Server=localhost;Database=MathInsight;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=True;");

        return new LearningDbContext(optionsBuilder.Options);
    }
}
