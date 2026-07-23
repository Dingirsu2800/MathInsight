using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MathInsight.Modules.Learning_Lecture.Persistence;

public class LearningDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LearningDbContext>();
        optionsBuilder.UseSqlServer("Server=tcp:okb-girsu.database.windows.net,1433;Initial Catalog=math-insight;Persist Security Info=False;User ID=Charlemagne;Password=J2tdr2fMKdsZgQ8;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");

        return new LearningDbContext(optionsBuilder.Options);
    }
}
