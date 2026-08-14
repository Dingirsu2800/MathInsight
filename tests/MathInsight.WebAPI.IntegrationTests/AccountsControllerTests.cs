using System.Net;
using System.Net.Http.Json;
using MathInsight.WebAPI.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.WebAPI.IntegrationTests;

/// <summary>
/// L2 integration tests for AccountsController — GET/PUT /accounts/profile and
/// POST /accounts/change-password. Every action is [Authorize]d and resolves the account from the
/// caller's own token, so the auth checks and the "cannot touch anyone else's account" property are
/// only observable at this level. Tokens come from the real login endpoint.
/// </summary>
public class AccountsControllerTests : AuthTestBase
{
    public AccountsControllerTests(AuthApiFactory factory) : base(factory) { }

    // ================================================================ GET /api/v1/accounts/profile

    [Fact]
    public async Task GetProfile_WithoutToken_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/accounts/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_AuthenticatedStudent_Returns200WithTheStudentBlock()
    {
        var email = $"{Unique("prof-get")}@mathinsight.test";
        var username = Unique("prof_get");
        var accountId = await SeedAccountAsync(email, username);
        await SeedStudentRowAsync(accountId);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.GetAsync("/api/v1/accounts/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal(username, body.GetProperty("username").GetString());
        Assert.Equal("Student", body.GetProperty("roleName").GetString());
        Assert.Equal(11, body.GetProperty("student").GetProperty("currentGrade").GetInt32());
        Assert.Equal(JsonValueKindNull, body.GetProperty("teacher").ValueKind);
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;

    [Fact]
    public async Task GetProfile_AuthenticatedTeacher_SurfacesTheApplicationStatus()
    {
        var email = $"{Unique("prof-tch")}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique("prof_tch"), AuthApiFactory.TeacherRoleId);
        await SeedTeacherRowAsync(accountId);
        await SeedTeacherApplicationAsync(accountId, "Pending");
        var (client, _) = await AuthenticatedClientAsync(email);

        var body = await ReadJsonAsync(await client.GetAsync("/api/v1/accounts/profile"));

        Assert.Equal("Teacher", body.GetProperty("roleName").GetString());
        var teacher = body.GetProperty("teacher");
        Assert.Equal("Pending", teacher.GetProperty("applicationStatus").GetString());
        Assert.False(teacher.GetProperty("isVerified").GetBoolean());
    }

    [Fact]
    public async Task GetProfile_TokenForADeletedAccount_Returns401()
    {
        // The JWT is still cryptographically valid; the row behind it is gone.
        var email = $"{Unique("prof-gone")}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique("prof_gone"));
        var (client, _) = await AuthenticatedClientAsync(email);
        await DeleteAccountAsync(accountId);

        var response = await client.GetAsync("/api/v1/accounts/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("AUTH_TOKEN_INVALID", body.GetProperty("code").GetString());
    }

    // ================================================================ PUT /api/v1/accounts/profile

    [Fact]
    public async Task UpdateProfile_WithoutToken_Returns401()
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/accounts/profile", new { firstName = "Ada" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ValidPartialUpdate_Returns200AndPersists()
    {
        var email = $"{Unique("prof-put")}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique("prof_put"));
        await SeedStudentRowAsync(accountId);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsJsonAsync(
            "/api/v1/accounts/profile", new { firstName = "Ada", school = "Chu Van An" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Ada", body.GetProperty("firstName").GetString());

        var account = await FindAccountAsync(email);
        Assert.Equal("Ada", account!.FirstName);
        Assert.Equal("User", account.LastName);   // omitted field kept its stored value

        var student = await Factory.FromIdentityDbAsync(db =>
            db.Students.AsNoTracking().FirstAsync(s => s.StudentId == accountId));
        Assert.Equal("Chu Van An", student.School);
        Assert.Equal(11, student.CurrentGrade);   // omitted field kept its stored value
    }

    [Fact]
    public async Task UpdateProfile_OverlongFirstName_Returns400()
    {
        var email = $"{Unique("prof-long")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("prof_long"));
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsJsonAsync(
            "/api/v1/accounts/profile", new { firstName = new string('A', 51) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_OutOfRangeGrade_Returns400()
    {
        var email = $"{Unique("prof-grade")}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique("prof_grade"));
        await SeedStudentRowAsync(accountId);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsJsonAsync("/api/v1/accounts/profile", new { currentGrade = 13 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_CannotChangeEmailUsernameOrRole()
    {
        // Those properties are absent from the DTO, so the deserializer ignores them silently.
        var email = $"{Unique("prof-imm")}@mathinsight.test";
        var username = Unique("prof_imm");
        await SeedAccountAsync(email, username);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsJsonAsync("/api/v1/accounts/profile", new
        {
            firstName = "Ada",
            email = "hijacked@mathinsight.test",
            username = "hijacked_user",
            roleName = "Admin"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await FindAccountAsync(email);
        Assert.NotNull(account);
        Assert.Equal(username, account!.Username);
        Assert.Equal(AuthApiFactory.StudentRoleId, account.RoleId);
        Assert.Null(await FindAccountAsync("hijacked@mathinsight.test"));
    }

    [Fact]
    public async Task UpdateProfile_TouchesOnlyTheCallersAccount()
    {
        var email = $"{Unique("prof-mine")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("prof_mine"));
        var otherEmail = $"{Unique("prof-other")}@mathinsight.test";
        await SeedAccountAsync(otherEmail, Unique("prof_other"));
        var (client, _) = await AuthenticatedClientAsync(email);

        await client.PutAsJsonAsync("/api/v1/accounts/profile", new { firstName = "Ada" });

        var other = await FindAccountAsync(otherEmail);
        Assert.Equal("Test", other!.FirstName);
    }

    // ================================================================ POST /api/v1/accounts/change-password

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/accounts/change-password", new
        {
            currentPassword = ValidPassword,
            newPassword = "Rotated#Password9"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ValidRequest_Returns200WithAFreshTokenPair()
    {
        const string newPassword = "Rotated#Password9";
        var email = $"{Unique("pwd-ok")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("pwd_ok"));
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PostAsJsonAsync("/api/v1/accounts/change-password", new
        {
            currentPassword = ValidPassword,
            newPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task ChangePassword_ValidRequest_RotatesTheStoredCredential()
    {
        const string newPassword = "Rotated#Password9";
        var email = $"{Unique("pwd-rot")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("pwd_rot"));
        var (client, _) = await AuthenticatedClientAsync(email);

        await client.PostAsJsonAsync("/api/v1/accounts/change-password", new
        {
            currentPassword = ValidPassword,
            newPassword
        });

        var plain = CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(plain, email, newPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(plain, email, ValidPassword)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RevokesThePreExistingSession()
    {
        // BR-15: the refresh token the caller held before the change is dead afterwards.
        var email = $"{Unique("pwd-rev")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("pwd_rev"));
        var (client, oldRefreshToken) = await AuthenticatedClientAsync(email);

        await client.PostAsJsonAsync("/api/v1/accounts/change-password", new
        {
            currentPassword = ValidPassword,
            newPassword = "Rotated#Password9"
        });

        var plain = CreateClient();
        var reuse = await plain.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = oldRefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns400()
    {
        // The caller's token is valid, so this is a bad payload — not an authentication failure.
        var email = $"{Unique("pwd-bad")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("pwd_bad"));
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PostAsJsonAsync("/api/v1/accounts/change-password", new
        {
            currentPassword = "Not#TheCurrent1",
            newPassword = "Rotated#Password9"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("AUTH_INVALID_CURRENT_PASSWORD", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_LeavesTheCredentialIntact()
    {
        var email = $"{Unique("pwd-keep")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("pwd_keep"));
        var (client, _) = await AuthenticatedClientAsync(email);

        await client.PostAsJsonAsync("/api/v1/accounts/change-password", new
        {
            currentPassword = "Not#TheCurrent1",
            newPassword = "Rotated#Password9"
        });

        var plain = CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(plain, email, ValidPassword)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ReusingTheCurrentPassword_Returns400()
    {
        var email = $"{Unique("pwd-same")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("pwd_same"));
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PostAsJsonAsync("/api/v1/accounts/change-password", new
        {
            currentPassword = ValidPassword,
            newPassword = ValidPassword
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("AUTH_SAME_PASSWORD", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_Returns400FromModelValidation()
    {
        var email = $"{Unique("pwd-weak")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("pwd_weak"));
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PostAsJsonAsync("/api/v1/accounts/change-password", new
        {
            currentPassword = ValidPassword,
            newPassword = "abc"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_MissingCurrentPassword_Returns400()
    {
        var email = $"{Unique("pwd-miss")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("pwd_miss"));
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PostAsJsonAsync(
            "/api/v1/accounts/change-password", new { newPassword = "Rotated#Password9" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
