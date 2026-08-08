using ClosedXML.Excel;
using MathInsight.Modules.Identity_Access.Commands.ImportAccounts;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

public class ImportAccountsCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly ImportAccountsCommandHandler _handler;

    public ImportAccountsCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        _handler = new ImportAccountsCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedRolesAsync()
    {
        _db.Roles.AddRange(
            new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Student" },
            new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Teacher" },
            new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Expert" });
        await _db.SaveChangesAsync();
    }

    private static byte[] BuildWorkbook(params string?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Accounts");
        sheet.Cell(1, 1).Value = "Username";
        sheet.Cell(1, 2).Value = "Email";
        sheet.Cell(1, 3).Value = "Password";
        sheet.Cell(1, 4).Value = "FirstName";
        sheet.Cell(1, 5).Value = "LastName";
        sheet.Cell(1, 6).Value = "PhoneNumber";
        sheet.Cell(1, 7).Value = "DateOfBirth";
        sheet.Cell(1, 8).Value = "Role";

        for (var r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            for (var c = 0; c < row.Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = row[c] ?? string.Empty;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string?[] ValidRow(string username, string role = "Student", string? email = null) =>
        new[] { username, email ?? $"{username}@example.com", "Password1!", "An", "Nguyen", null, null, role };

    [Fact]
    public async Task Handle_CorruptedFileBytes_ReturnsFailure()
    {
        var command = new ImportAccountsCommand(new byte[] { 1, 2, 3, 4, 5 });

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.InvalidExcelFile, result.Error);
    }

    [Fact]
    public async Task Handle_BlankTrailingRow_IsSilentlySkipped()
    {
        await SeedRolesAsync();
        var bytes = BuildWorkbook(
            ValidRow("user1"),
            // Username/Email blank (the only fields the handler checks to silently skip a row),
            // but the Role cell has stray content so ClosedXML still registers this as a "used"
            // row — otherwise LastRowUsed() would exclude an entirely-empty row on its own.
            new string?[] { null, null, null, null, null, null, null, "Student" });
        var command = new ImportAccountsCommand(bytes);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalRows);
        Assert.Equal(1, result.Value.SuccessCount);
        Assert.Equal(0, result.Value.SkippedCount);
    }

    [Fact]
    public async Task Handle_RowMissingRequiredField_IsSkippedWithReason()
    {
        await SeedRolesAsync();
        var bytes = BuildWorkbook(new[] { null, "noname@example.com", "Password1!", "A", "B", null, null, "Student" });
        var command = new ImportAccountsCommand(bytes);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.SkippedCount);
        Assert.Equal("Missing required field(s).", result.Value.SkippedRows[0].Reason);
        Assert.Equal(0, await _db.Accounts.CountAsync());
    }

    [Fact]
    public async Task Handle_RowPasswordTooShort_IsSkippedWithReason()
    {
        await SeedRolesAsync();
        var bytes = BuildWorkbook(new[] { "shortpw", "shortpw@example.com", "abc123", "A", "B", null, null, "Student" });
        var command = new ImportAccountsCommand(bytes);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.Value!.SkippedCount);
        Assert.Equal("Password must be at least 8 characters.", result.Value.SkippedRows[0].Reason);
    }

    [Fact]
    public async Task Handle_RowInvalidRole_IsSkippedWithReason()
    {
        await SeedRolesAsync();
        var bytes = BuildWorkbook(ValidRow("baduser", role: "Guardian"));
        var command = new ImportAccountsCommand(bytes);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.Value!.SkippedCount);
        Assert.Equal("Role must be one of: Student, Teacher, Expert.", result.Value.SkippedRows[0].Reason);
    }

    [Fact]
    public async Task Handle_RowUsernameDuplicatesExistingAccount_IsSkipped()
    {
        var roles = await SeedRolesAndReturnAsync();
        _db.Accounts.Add(new Account
        {
            AccountId = Guid.NewGuid().ToString(), Username = "john01", Email = "john01@existing.com",
            PasswordHash = "h", FirstName = "J", LastName = "D", RoleId = roles["Student"], IsActive = true, CreatedTime = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var bytes = BuildWorkbook(ValidRow("john01"));
        var result = await _handler.Handle(new ImportAccountsCommand(bytes), CancellationToken.None);

        Assert.Equal(1, result.Value!.SkippedCount);
        Assert.Equal("Username already exists.", result.Value.SkippedRows[0].Reason);
    }

    [Fact]
    public async Task Handle_TwoRowsShareUsername_SecondRowIsSkipped_InFileDeduplication()
    {
        await SeedRolesAsync();
        var bytes = BuildWorkbook(
            ValidRow("dupuser", email: "first@example.com"),
            ValidRow("dupuser", email: "second@example.com"));

        var result = await _handler.Handle(new ImportAccountsCommand(bytes), CancellationToken.None);

        Assert.Equal(1, result.Value!.SuccessCount);
        Assert.Equal(1, result.Value.SkippedCount);
        Assert.Equal(3, result.Value.SkippedRows[0].RowNumber);
        Assert.Equal("Username already exists.", result.Value.SkippedRows[0].Reason);
    }

    [Fact]
    public async Task Handle_RowEmailDuplicatesExistingAccount_IsSkipped()
    {
        var roles = await SeedRolesAndReturnAsync();
        _db.Accounts.Add(new Account
        {
            AccountId = Guid.NewGuid().ToString(), Username = "existing", Email = "taken@example.com",
            PasswordHash = "h", FirstName = "J", LastName = "D", RoleId = roles["Student"], IsActive = true, CreatedTime = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var bytes = BuildWorkbook(ValidRow("newuser", email: "taken@example.com"));
        var result = await _handler.Handle(new ImportAccountsCommand(bytes), CancellationToken.None);

        Assert.Equal(1, result.Value!.SkippedCount);
        Assert.Equal("Email already exists.", result.Value.SkippedRows[0].Reason);
    }

    [Fact]
    public async Task Handle_MixedValidAndInvalidRows_PartialSuccess()
    {
        var roles = await SeedRolesAndReturnAsync();
        _db.Accounts.Add(new Account
        {
            AccountId = Guid.NewGuid().ToString(), Username = "existing", Email = "existing@example.com",
            PasswordHash = "h", FirstName = "J", LastName = "D", RoleId = roles["Student"], IsActive = true, CreatedTime = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var bytes = BuildWorkbook(
            ValidRow("valid1"),
            ValidRow("valid2"),
            new[] { null, "nouser@example.com", "Password1!", "A", "B", null, null, "Student" },
            ValidRow("dupemail", email: "existing@example.com"));

        var result = await _handler.Handle(new ImportAccountsCommand(bytes), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.TotalRows);
        Assert.Equal(2, result.Value.SuccessCount);
        Assert.Equal(2, result.Value.SkippedCount);
        Assert.Equal(3, await _db.Accounts.CountAsync()); // 1 pre-existing + 2 imported
    }

    [Fact]
    public async Task Handle_AllRowsInvalid_NoAccountsPersisted()
    {
        await SeedRolesAsync();
        var bytes = BuildWorkbook(
            new[] { null, "a@example.com", "Password1!", "A", "B", null, null, "Student" },
            new[] { "user2", null, "Password1!", "A", "B", null, null, "Student" });

        var result = await _handler.Handle(new ImportAccountsCommand(bytes), CancellationToken.None);

        Assert.Equal(0, result.Value!.SuccessCount);
        Assert.Equal(2, result.Value.SkippedCount);
        Assert.Equal(0, await _db.Accounts.CountAsync());
    }

    [Fact]
    public async Task Handle_RoleSpecificRowsCreatedPerAccountType()
    {
        await SeedRolesAsync();
        var bytes = BuildWorkbook(
            ValidRow("stud1", role: "Student"),
            ValidRow("teach1", role: "Teacher"),
            ValidRow("exp1", role: "Expert"));

        var result = await _handler.Handle(new ImportAccountsCommand(bytes), CancellationToken.None);

        Assert.Equal(3, result.Value!.SuccessCount);
        Assert.Equal(1, await _db.Students.CountAsync());
        Assert.Equal(1, await _db.Teachers.CountAsync());
        Assert.True(await _db.Teachers.AnyAsync(t => t.IsVerified));
        Assert.Equal(1, await _db.Experts.CountAsync());
    }

    private async Task<Dictionary<string, string>> SeedRolesAndReturnAsync()
    {
        var student = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Student" };
        var teacher = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Teacher" };
        var expert = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Expert" };
        _db.Roles.AddRange(student, teacher, expert);
        await _db.SaveChangesAsync();
        return new Dictionary<string, string>
        {
            ["Student"] = student.RoleId,
            ["Teacher"] = teacher.RoleId,
            ["Expert"] = expert.RoleId,
        };
    }
}
