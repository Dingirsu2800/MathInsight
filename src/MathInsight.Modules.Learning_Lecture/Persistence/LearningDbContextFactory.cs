using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MathInsight.Modules.Learning_Lecture.Persistence;

public class LearningDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LearningDbContext>();
        
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = "Server=localhost;Database=MathInsight;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=True;";
        }
        
        optionsBuilder.UseSqlServer(connectionString);

        return new LearningDbContext(optionsBuilder.Options);
    }
}
