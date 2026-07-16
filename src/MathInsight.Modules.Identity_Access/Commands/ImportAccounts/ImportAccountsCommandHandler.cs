using ClosedXML.Excel;
using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.ImportAccounts;

/// <summary>
/// Expected .xlsx template columns (row 1 = header, data from row 2):
/// A=Username, B=Email, C=Password, D=FirstName, E=LastName, F=PhoneNumber, G=DateOfBirth (yyyy-MM-dd), H=Role (Student/Teacher/Expert)
/// </summary>
public class ImportAccountsCommandHandler
    : IRequestHandler<ImportAccountsCommand, Result<ImportAccountsResponse>>
{
    private const int BCryptWorkFactor = 12;

    private readonly IdentityDbContext _dbContext;

    public ImportAccountsCommandHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ImportAccountsResponse>> Handle(
        ImportAccountsCommand request,
        CancellationToken cancellationToken)
    {
        XLWorkbook workbook;
        try
        {
            using var stream = new MemoryStream(request.FileContent);
            workbook = new XLWorkbook(stream);
        }
        catch
        {
            return Result<ImportAccountsResponse>.Failure(IdentityErrors.InvalidExcelFile);
        }

        using (workbook)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null)
                return Result<ImportAccountsResponse>.Failure(IdentityErrors.InvalidExcelFile);

            var roles = await _dbContext.Roles.ToListAsync(cancellationToken);
            var existingUsernames = new HashSet<string>(
                await _dbContext.Accounts.Select(account => account.Username).ToListAsync(cancellationToken),
                StringComparer.OrdinalIgnoreCase);
            var existingEmails = new HashSet<string>(
                await _dbContext.Accounts.Select(account => account.Email).ToListAsync(cancellationToken),
                StringComparer.OrdinalIgnoreCase);

            var seenUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var skippedRows = new List<ImportRowResult>();
            var newAccounts = new List<Account>();
            var newStudents = new List<Student>();
            var newTeachers = new List<Teacher>();
            var newExperts = new List<Expert>();

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            var totalRows = Math.Max(0, lastRow - 1);

            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                var username = row.Cell(1).GetString().Trim();
                var email = row.Cell(2).GetString().Trim();
                var password = row.Cell(3).GetString().Trim();
                var firstName = row.Cell(4).GetString().Trim();
                var lastName = row.Cell(5).GetString().Trim();
                var phoneNumber = row.Cell(6).GetString().Trim();
                var dateOfBirthRaw = row.Cell(7).GetString().Trim();
                var roleName = row.Cell(8).GetString().Trim();

                if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(email))
                    continue; // blank trailing row

                string? reason = null;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(firstName) ||
                    string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(roleName))
                {
                    reason = "Missing required field(s).";
                }
                else if (password.Length < 8)
                {
                    reason = "Password must be at least 8 characters.";
                }
                else if (!roles.Any(role =>
                             string.Equals(role.RoleName, roleName, StringComparison.OrdinalIgnoreCase) &&
                             (role.RoleName.Equals("Student", StringComparison.OrdinalIgnoreCase) ||
                              role.RoleName.Equals("Teacher", StringComparison.OrdinalIgnoreCase) ||
                              role.RoleName.Equals("Expert", StringComparison.OrdinalIgnoreCase))))
                {
                    reason = "Role must be one of: Student, Teacher, Expert.";
                }
                else if (existingUsernames.Contains(username) || !seenUsernames.Add(username))
                {
                    reason = "Username already exists.";
                }
                else if (existingEmails.Contains(email) || !seenEmails.Add(email))
                {
                    reason = "Email already exists.";
                }

                if (reason is not null)
                {
                    skippedRows.Add(new ImportRowResult(rowNumber, username, email, reason));
                    continue;
                }

                DateOnly? dateOfBirth = DateOnly.TryParse(dateOfBirthRaw, out var parsedDate)
                    ? parsedDate
                    : null;

                var accountId = Guid.NewGuid().ToString();
                var role = roles.First(role => string.Equals(role.RoleName, roleName, StringComparison.OrdinalIgnoreCase));

                newAccounts.Add(new Account
                {
                    AccountId = accountId,
                    Username = username,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, BCryptWorkFactor),
                    FirstName = firstName,
                    LastName = lastName,
                    PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber,
                    DateOfBirth = dateOfBirth,
                    RoleId = role.RoleId,
                    IsActive = true,
                    CreatedTime = DateTime.UtcNow
                });

                switch (role.RoleName.ToUpperInvariant())
                {
                    case "STUDENT":
                        newStudents.Add(new Student { StudentId = accountId });
                        break;
                    case "TEACHER":
                        newTeachers.Add(new Teacher { TeacherId = accountId, IsVerified = true });
                        break;
                    case "EXPERT":
                        newExperts.Add(new Expert { ExpertId = accountId });
                        break;
                }
            }

            if (newAccounts.Count > 0)
            {
                _dbContext.Accounts.AddRange(newAccounts);
                _dbContext.Students.AddRange(newStudents);
                _dbContext.Teachers.AddRange(newTeachers);
                _dbContext.Experts.AddRange(newExperts);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Result<ImportAccountsResponse>.Success(new ImportAccountsResponse(
                totalRows,
                newAccounts.Count,
                skippedRows.Count,
                skippedRows));
        }
    }
}
