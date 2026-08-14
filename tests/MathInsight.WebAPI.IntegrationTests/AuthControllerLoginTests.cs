using System.Net;
using System.Net.Http.Json;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.WebAPI.IntegrationTests.Infrastructure;
using Xunit;

namespace MathInsight.WebAPI.IntegrationTests;

/// <summary>
/// L2 integration tests for the session endpoints of AuthController: login, refresh, logout and the
/// Google OAuth pair. Requests go through the real pipeline (routing, model binding, JWT auth,
/// authorization policies, MediatR); only external infrastructure is faked — see AuthApiFactory.
/// </summary>
public class AuthControllerLoginTests : AuthTestBase
{
    public AuthControllerLoginTests(AuthApiFactory factory) : base(factory) { }

    // ================================================================ POST /api/v1/auth/login

    [Fact]
    public async Task Login_ValidStudentCredentials_Returns200WithTokenPair()
    {
        var email = $"{Unique("login-ok")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("login_ok"));
        var client = CreateClient();

        var response = await LoginAsync(client, email, ValidPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("refreshToken").GetString()));
        Assert.Equal("Student", body.GetProperty("roleName").GetString());
        Assert.Equal(email, body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Login_ByUsername_Returns200()
    {
        var email = $"{Unique("login-user")}@mathinsight.test";
        var username = Unique("login_user");
        await SeedAccountAsync(email, username);
        var client = CreateClient();

        var response = await LoginAsync(client, username, ValidPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401InvalidCredentials()
    {
        var email = $"{Unique("login-bad")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("login_bad"));
        var client = CreateClient();

        var response = await LoginAsync(client, email, "Wrong#Password1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("AUTH_INVALID_CREDENTIALS", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_UnknownAccount_IsIndistinguishableFromAWrongPassword()
    {
        var email = $"{Unique("login-enum")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("login_enum"));
        var client = CreateClient();

        var wrongPassword = await LoginAsync(client, email, "Wrong#Password1");
        var unknownAccount = await LoginAsync(client, "ghost@mathinsight.test", ValidPassword);

        Assert.Equal(wrongPassword.StatusCode, unknownAccount.StatusCode);
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await unknownAccount.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_MissingPassword_Returns400()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { usernameOrEmail = "someone@mathinsight.test" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_DeactivatedAccount_Returns403()
    {
        var email = $"{Unique("login-off")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("login_off"), isActive: false);
        var client = CreateClient();

        var response = await LoginAsync(client, email, ValidPassword);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("AUTH_ACCOUNT_DEACTIVATED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_Student_CarriesNullApplicationStatus()
    {
        var email = $"{Unique("login-stat0")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("login_stat0"));
        var client = CreateClient();

        var body = await ReadJsonAsync(await LoginAsync(client, email, ValidPassword));

        Assert.Null(StringOrNull(body, "applicationStatus"));
    }

    [Fact]
    public async Task Login_TeacherWithPendingApplication_Returns200WithPendingStatus()
    {
        var email = $"{Unique("login-pend")}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique("login_pend"), AuthApiFactory.TeacherRoleId);
        await SeedTeacherApplicationAsync(accountId, TeacherApplication.StatusPending);
        var client = CreateClient();

        var response = await LoginAsync(client, email, ValidPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // login is NOT blocked
        var body = await ReadJsonAsync(response);
        Assert.Equal("pending", body.GetProperty("applicationStatus").GetString());
    }

    [Fact]
    public async Task Login_TeacherWithRejectedApplication_Returns200WithRejectedStatus()
    {
        var email = $"{Unique("login-rej")}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique("login_rej"), AuthApiFactory.TeacherRoleId);
        await SeedTeacherApplicationAsync(accountId, TeacherApplication.StatusRejected);
        var client = CreateClient();

        var response = await LoginAsync(client, email, ValidPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("rejected", body.GetProperty("applicationStatus").GetString());
    }

    [Fact]
    public async Task Login_TeacherWithApprovedApplication_Returns200WithApprovedStatus()
    {
        var email = $"{Unique("login-appr")}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique("login_appr"), AuthApiFactory.TeacherRoleId);
        await SeedTeacherApplicationAsync(accountId, TeacherApplication.StatusApproved);
        var client = CreateClient();

        var body = await ReadJsonAsync(await LoginAsync(client, email, ValidPassword));

        Assert.Equal("approved", body.GetProperty("applicationStatus").GetString());
    }

    [Fact]
    public async Task Login_TeacherWithNoApplication_Returns200WithNoneStatus()
    {
        var email = $"{Unique("login-none")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("login_none"), AuthApiFactory.TeacherRoleId);
        var client = CreateClient();

        var body = await ReadJsonAsync(await LoginAsync(client, email, ValidPassword));

        Assert.Equal("none", body.GetProperty("applicationStatus").GetString());
    }

    // ================================================================ POST /api/v1/auth/refresh

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithANewPair()
    {
        var email = $"{Unique("refr-ok")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("refr_ok"));
        var client = CreateClient();
        var login = await ReadJsonAsync(await LoginAsync(client, email, ValidPassword));
        var refreshToken = login.GetProperty("refreshToken").GetString();

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.NotEqual(refreshToken, body.GetProperty("refreshToken").GetString());   // rotated
    }

    [Fact]
    public async Task Refresh_ReusedToken_Returns401()
    {
        // Rotation is single-use: replaying the consumed token must fail.
        var email = $"{Unique("refr-re")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("refr_re"));
        var client = CreateClient();
        var login = await ReadJsonAsync(await LoginAsync(client, email, ValidPassword));
        var refreshToken = login.GetProperty("refreshToken").GetString();

        await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Refresh_UnknownToken_Returns401()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = "not-a-real-refresh-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================ POST /api/v1/auth/logout

    [Fact]
    public async Task Logout_ValidSession_Returns204AndKillsTheRefreshToken()
    {
        var email = $"{Unique("out-ok")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("out_ok"));
        var client = CreateClient();
        var login = await ReadJsonAsync(await LoginAsync(client, email, ValidPassword));
        var refreshToken = login.GetProperty("refreshToken").GetString();

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Logout_UnknownRefreshToken_IsIdempotent204()
    {
        // Never reveal whether the session existed.
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/logout", new { refreshToken = "never-issued" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutBearerToken_Still204()
    {
        // BR-10: logout must work when the access token is missing or already expired.
        var email = $"{Unique("out-nobear")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("out_nobear"));
        var client = CreateClient();
        var login = await ReadJsonAsync(await LoginAsync(client, email, ValidPassword));

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new { refreshToken = login.GetProperty("refreshToken").GetString() });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ================================================================ GET /api/v1/auth/google

    [Fact]
    public async Task GoogleLogin_Redirects302ToConsentUrlCarryingState()
    {
        var client = CreateClient(followRedirects: false);

        var response = await client.GetAsync("/api/v1/auth/google");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("accounts.google.test", location);
        Assert.Contains("state=", location);
    }

    // ================================================================ GET /api/v1/auth/google/callback

    /// <summary>Starts the flow to obtain a live CSRF state, as the browser would.</summary>
    private async Task<string> BeginGoogleFlowAsync(HttpClient client)
    {
        var start = await client.GetAsync("/api/v1/auth/google");
        var location = start.Headers.Location!.ToString();
        return location[(location.IndexOf("state=", StringComparison.Ordinal) + "state=".Length)..];
    }

    [Fact]
    public async Task GoogleCallback_MissingCode_RedirectsToFrontendWithError()
    {
        var client = CreateClient(followRedirects: false);
        var state = await BeginGoogleFlowAsync(client);

        var response = await client.GetAsync($"/api/v1/auth/google/callback?state={state}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=google_failed", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task GoogleCallback_UnknownState_RedirectsToFrontendWithError()
    {
        // CSRF protection: a state the server never issued is refused.
        var client = CreateClient(followRedirects: false);
        Factory.Google.NextProfile = new GoogleUserProfile(
            "sub-csrf", $"{Unique("g-csrf")}@gmail.test", true, "Ada", "Lovelace");

        var response = await client.GetAsync("/api/v1/auth/google/callback?code=abc&state=forged-state");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=google_failed", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task GoogleCallback_ConsumedState_CannotBeReplayed()
    {
        var client = CreateClient(followRedirects: false);
        var state = await BeginGoogleFlowAsync(client);
        var email = $"{Unique("g-replay")}@gmail.test";
        Factory.Google.NextProfile = new GoogleUserProfile("sub-replay", email, true, "Ada", "Lovelace");

        var first = await client.GetAsync($"/api/v1/auth/google/callback?code=abc&state={state}");
        var replay = await client.GetAsync($"/api/v1/auth/google/callback?code=abc&state={state}");

        Assert.DoesNotContain("error=google_failed", first.Headers.Location!.ToString());
        Assert.Contains("error=google_failed", replay.Headers.Location!.ToString());
    }

    [Fact]
    public async Task GoogleCallback_VerifiedNewUser_CreatesAccountAndRedirectsWithTokens()
    {
        var client = CreateClient(followRedirects: false);
        var state = await BeginGoogleFlowAsync(client);
        var email = $"{Unique("g-new")}@gmail.test";
        Factory.Google.NextProfile = new GoogleUserProfile("sub-new-user", email, true, "Ada", "Lovelace");

        var response = await client.GetAsync($"/api/v1/auth/google/callback?code=valid-code&state={state}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("/auth/google/success", location);
        Assert.Contains("accessToken=", location);
        Assert.Contains("refreshToken=", location);

        var account = await FindAccountAsync(email);
        Assert.NotNull(account);
        Assert.True(account!.IsActive);
        Assert.Equal("sub-new-user", account.GoogleSubId);
    }

    [Fact]
    public async Task GoogleCallback_UnverifiedEmail_RedirectsWithErrorAndCreatesNothing()
    {
        var client = CreateClient(followRedirects: false);
        var state = await BeginGoogleFlowAsync(client);
        var email = $"{Unique("g-unver")}@gmail.test";
        Factory.Google.NextProfile = new GoogleUserProfile("sub-unverified", email, false, "Ada", "Lovelace");

        var response = await client.GetAsync($"/api/v1/auth/google/callback?code=valid-code&state={state}");

        Assert.Contains("error=google_failed", response.Headers.Location!.ToString());
        Assert.Null(await FindAccountAsync(email));
    }

    [Fact]
    public async Task GoogleCallback_ExistingPasswordAccount_IsLinkedNotDuplicated()
    {
        var email = $"{Unique("g-link")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("g_link"));
        var client = CreateClient(followRedirects: false);
        var state = await BeginGoogleFlowAsync(client);
        Factory.Google.NextProfile = new GoogleUserProfile("sub-linked", email, true, "Ada", "Lovelace");

        var response = await client.GetAsync($"/api/v1/auth/google/callback?code=valid-code&state={state}");

        Assert.Contains("/auth/google/success", response.Headers.Location!.ToString());
        var account = await FindAccountAsync(email);
        Assert.Equal("sub-linked", account!.GoogleSubId);
        var count = await Factory.FromIdentityDbAsync(db =>
            Task.FromResult(db.Accounts.Count(a => a.Email == email)));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GoogleCallback_DeactivatedAccount_RedirectsWithError()
    {
        var email = $"{Unique("g-off")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("g_off"), isActive: false);
        var client = CreateClient(followRedirects: false);
        var state = await BeginGoogleFlowAsync(client);
        Factory.Google.NextProfile = new GoogleUserProfile("sub-off", email, true, "Ada", "Lovelace");

        var response = await client.GetAsync($"/api/v1/auth/google/callback?code=valid-code&state={state}");

        Assert.Contains("error=google_failed", response.Headers.Location!.ToString());
    }
}
