using System.Net.Http.Json;
using System.Text.Json;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.WebAPI.IntegrationTests.Infrastructure;

/// <summary>
/// Shared seeding and request helpers for the AuthController integration suites. Each test uses a
/// unique email/username so the suites can share one booted host without interfering.
/// </summary>
public abstract class AuthTestBase : IClassFixture<AuthApiFactory>, IAsyncLifetime
{
    protected const string ValidPassword = "Str0ng#Password";

    protected readonly AuthApiFactory Factory;

    protected AuthTestBase(AuthApiFactory factory) => Factory = factory;

    public Task InitializeAsync() => Factory.EnsureRolesAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>A client that surfaces 3xx responses instead of following them.</summary>
    protected HttpClient CreateClient(bool followRedirects = true) =>
        Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = followRedirects,
            BaseAddress = new Uri("https://localhost")
        });

    protected static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 40)];

    protected async Task<string> SeedAccountAsync(
        string email,
        string username,
        string roleId = AuthApiFactory.StudentRoleId,
        string password = ValidPassword,
        bool isActive = true,
        string? phoneNumber = null)
    {
        var accountId = Guid.NewGuid().ToString();

        await Factory.WithIdentityDbAsync(async db =>
        {
            db.Accounts.Add(new Account
            {
                AccountId = accountId,
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FirstName = "Test",
                LastName = "User",
                PhoneNumber = phoneNumber,
                RoleId = roleId,
                IsActive = isActive,
                CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
        });

        return accountId;
    }

    protected async Task<string> SeedTeacherApplicationAsync(
        string teacherId,
        string status,
        string documentsUrl = "https://cdn.test/cert.png")
    {
        var applicationId = Guid.NewGuid().ToString();

        await Factory.WithIdentityDbAsync(async db =>
        {
            db.TeacherApplications.Add(new TeacherApplication
            {
                ApplicationId = applicationId,
                TeacherId = teacherId,
                DocumentsUrl = documentsUrl,
                Status = status,
                AppliedTime = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                ReviewComments = status == TeacherApplication.StatusRejected ? "Certificate unreadable." : null
            });
            await db.SaveChangesAsync();
        });

        return applicationId;
    }

    /// <summary>The Teacher row the application's Include(...).ThenInclude(...) chain requires.</summary>
    protected Task SeedTeacherRowAsync(string teacherId, string? biography = "Ten years of calculus.") =>
        Factory.WithIdentityDbAsync(async db =>
        {
            db.Teachers.Add(new Teacher { TeacherId = teacherId, Biography = biography, IsVerified = false });
            await db.SaveChangesAsync();
        });

    protected Task SeedStudentRowAsync(string studentId) =>
        Factory.WithIdentityDbAsync(async db =>
        {
            db.Students.Add(new Student
            {
                StudentId = studentId,
                Gender = "Female",
                School = "Le Quy Don High School",
                CurrentGrade = 11
            });
            await db.SaveChangesAsync();
        });

    protected Task DeleteAccountAsync(string accountId) =>
        Factory.WithIdentityDbAsync(async db =>
        {
            var account = await db.Accounts.FirstAsync(a => a.AccountId == accountId);
            db.Accounts.Remove(account);
            await db.SaveChangesAsync();
        });

    /// <summary>
    /// Signs in through the real login endpoint and returns a client carrying the issued bearer
    /// token, so protected endpoints are exercised with a genuine JWT rather than a forged principal.
    /// </summary>
    protected async Task<(HttpClient Client, string RefreshToken)> AuthenticatedClientAsync(
        string email, string password = ValidPassword)
    {
        var client = CreateClient();
        var login = await LoginAsync(client, email, password);
        login.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(login);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", body.GetProperty("accessToken").GetString());

        return (client, body.GetProperty("refreshToken").GetString()!);
    }

    protected Task<Account?> FindAccountAsync(string email) =>
        Factory.FromIdentityDbAsync(db =>
            db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Email == email));

    protected static Task<HttpResponseMessage> LoginAsync(HttpClient client, string usernameOrEmail, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { usernameOrEmail, password });

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    protected static string? StringOrNull(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
}
