using System.Net;
using System.Net.Http.Json;
using System.Text;
using MathInsight.WebAPI.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.WebAPI.IntegrationTests;

/// <summary>
/// L2 integration tests for the registration and password endpoints of AuthController:
/// register/student, register/teacher, confirm-email, reset-password and confirm-reset-password.
///
/// These are the two-step flows, so the tests drive them end to end through HTTP: register, read
/// the token out of the captured email, then confirm — exactly the sequence a real user performs.
/// </summary>
public class AuthControllerRegistrationTests : AuthTestBase
{
    public AuthControllerRegistrationTests(AuthApiFactory factory) : base(factory) { }

    private static object StudentPayload(string email, string username, string password = ValidPassword) => new
    {
        username,
        email,
        password,
        firstName = "New",
        lastName = "Student",
        gender = "Female",
        school = "Le Quy Don High School",
        currentGrade = 11
    };

    private static MultipartFormDataContent TeacherForm(
        string email,
        string username,
        string phoneNumber,
        string password = ValidPassword,
        int certificateCount = 1)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(username), "Username" },
            { new StringContent(email), "Email" },
            { new StringContent(password), "Password" },
            { new StringContent("New"), "FirstName" },
            { new StringContent("Teacher"), "LastName" },
            { new StringContent(phoneNumber), "PhoneNumber" },
            { new StringContent("Ten years of calculus."), "Biography" }
        };

        for (var index = 0; index < certificateCount; index++)
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes($"fake-certificate-{index}"));
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, "Certificates", $"certificate-{index}.png");
        }

        return form;
    }

    // ================================================================ POST /api/v1/auth/register/student

    [Fact]
    public async Task RegisterStudent_ValidRequest_Returns202AndWritesNoAccountRow()
    {
        // DD-01: nothing reaches SQL until the email is confirmed.
        var email = $"{Unique("reg-ok")}@mathinsight.test";
        var username = Unique("reg_ok");
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register/student", StudentPayload(email, username));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Null(await FindAccountAsync(email));
        Assert.NotNull(Factory.Emails.LastTokenFor("confirmation", email));
    }

    [Fact]
    public async Task RegisterStudent_DuplicateEmail_Returns409()
    {
        var email = $"{Unique("reg-dupe")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("reg_dupe"));
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register/student", StudentPayload(email, Unique("reg_other")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("AUTH_EMAIL_ALREADY_CONFIRMED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task RegisterStudent_DuplicateUsername_Returns409()
    {
        var username = Unique("reg_dupeu");
        await SeedAccountAsync($"{Unique("reg-dupeu")}@mathinsight.test", username);
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register/student",
            StudentPayload($"{Unique("reg-fresh")}@mathinsight.test", username));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegisterStudent_MalformedEmail_Returns400()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register/student", StudentPayload("not-an-email", Unique("reg_bad")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterStudent_WeakPassword_Returns400()
    {
        // BR-08 is enforced at the DTO, so the handler is never reached.
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register/student",
            StudentPayload($"{Unique("reg-weak")}@mathinsight.test", Unique("reg_weak"), password: "abc"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterStudent_OutOfRangeGrade_Returns400()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register/student", new
        {
            username = Unique("reg_grade"),
            email = $"{Unique("reg-grade")}@mathinsight.test",
            password = ValidPassword,
            firstName = "New",
            lastName = "Student",
            currentGrade = 13   // CK_Student_CurrentGrade allows 10..12 only
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterStudent_MissingRequiredField_Returns400()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register/student", new
        {
            email = $"{Unique("reg-miss")}@mathinsight.test",
            password = ValidPassword
            // username, firstName and lastName omitted
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ================================================================ POST /api/v1/auth/confirm-email

    [Fact]
    public async Task ConfirmEmail_ValidToken_Returns200AndCreatesTheAccount()
    {
        var email = $"{Unique("cfm-ok")}@mathinsight.test";
        var username = Unique("cfm_ok");
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register/student", StudentPayload(email, username));
        var token = Factory.Emails.LastTokenFor("confirmation", email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { token });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await FindAccountAsync(email);
        Assert.NotNull(account);
        Assert.True(account!.IsActive);
        Assert.Equal(AuthApiFactory.StudentRoleId, account.RoleId);

        var hasStudentRow = await Factory.FromIdentityDbAsync(db =>
            db.Students.AsNoTracking().AnyAsync(s => s.StudentId == account.AccountId));
        Assert.True(hasStudentRow);
    }

    [Fact]
    public async Task ConfirmEmail_ThenLogin_Succeeds()
    {
        // The whole point of the flow: the confirmed account can sign in with its password.
        var email = $"{Unique("cfm-login")}@mathinsight.test";
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register/student", StudentPayload(email, Unique("cfm_login")));
        var token = Factory.Emails.LastTokenFor("confirmation", email);
        await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { token });

        var login = await LoginAsync(client, email, ValidPassword);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_ReplayedToken_Returns410()
    {
        var email = $"{Unique("cfm-replay")}@mathinsight.test";
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register/student", StudentPayload(email, Unique("cfm_replay")));
        var token = Factory.Emails.LastTokenFor("confirmation", email);
        await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { token });

        var replay = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { token });

        Assert.Equal(HttpStatusCode.Gone, replay.StatusCode);
        var body = await ReadJsonAsync(replay);
        Assert.Equal("AUTH_TOKEN_EXPIRED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ConfirmEmail_UnknownToken_Returns410()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email", new { token = "never-issued-token" });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_EmailClaimedSinceRegistration_Returns409()
    {
        // The pending-registration race: a competing account took the email before confirmation.
        var email = $"{Unique("cfm-race")}@mathinsight.test";
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register/student", StudentPayload(email, Unique("cfm_race")));
        var token = Factory.Emails.LastTokenFor("confirmation", email);
        await SeedAccountAsync(email, Unique("cfm_winner"));

        var response = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { token });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_MissingToken_Returns400()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ================================================================ POST /api/v1/auth/register/teacher

    [Fact]
    public async Task RegisterTeacher_ValidMultipart_Returns202AndWritesNoRows()
    {
        var email = $"{Unique("tch-ok")}@mathinsight.test";
        var client = CreateClient();

        var response = await client.PostAsync(
            "/api/v1/auth/register/teacher",
            TeacherForm(email, Unique("tch_ok"), "0900100001"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Null(await FindAccountAsync(email));
        Assert.NotNull(Factory.Emails.LastTokenFor("confirmation", email));
    }

    [Fact]
    public async Task RegisterTeacher_ThenConfirm_CreatesTeacherAndPendingApplication()
    {
        var email = $"{Unique("tch-cfm")}@mathinsight.test";
        var client = CreateClient();
        await client.PostAsync(
            "/api/v1/auth/register/teacher", TeacherForm(email, Unique("tch_cfm"), "0900100002"));
        var token = Factory.Emails.LastTokenFor("confirmation", email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { token });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await FindAccountAsync(email);
        Assert.Equal(AuthApiFactory.TeacherRoleId, account!.RoleId);

        var application = await Factory.FromIdentityDbAsync(db =>
            db.TeacherApplications.AsNoTracking().FirstOrDefaultAsync(a => a.TeacherId == account.AccountId));
        Assert.NotNull(application);
        Assert.Equal("Pending", application!.Status);
    }

    [Fact]
    public async Task RegisterTeacher_MultipleCertificates_AreAllUploaded()
    {
        var email = $"{Unique("tch-multi")}@mathinsight.test";
        var client = CreateClient();
        var before = Factory.Images.UploadCount;

        var response = await client.PostAsync(
            "/api/v1/auth/register/teacher",
            TeacherForm(email, Unique("tch_multi"), "0900100003", certificateCount: 3));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(before + 3, Factory.Images.UploadCount);
    }

    [Fact]
    public async Task RegisterTeacher_NoCertificate_Returns400()
    {
        var client = CreateClient();

        var response = await client.PostAsync(
            "/api/v1/auth/register/teacher",
            TeacherForm($"{Unique("tch-nocert")}@mathinsight.test", Unique("tch_nocert"), "0900100004", certificateCount: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterTeacher_DuplicateEmail_Returns409()
    {
        var email = $"{Unique("tch-dupe")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("tch_dupe"), AuthApiFactory.TeacherRoleId);
        var client = CreateClient();

        var response = await client.PostAsync(
            "/api/v1/auth/register/teacher", TeacherForm(email, Unique("tch_new"), "0900100005"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegisterTeacher_DuplicatePhoneNumber_Returns409()
    {
        const string phone = "0900100099";
        await SeedAccountAsync(
            $"{Unique("tch-ph")}@mathinsight.test", Unique("tch_ph"),
            AuthApiFactory.TeacherRoleId, phoneNumber: phone);
        var client = CreateClient();

        var response = await client.PostAsync(
            "/api/v1/auth/register/teacher",
            TeacherForm($"{Unique("tch-ph2")}@mathinsight.test", Unique("tch_ph2"), phone));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("AUTH_PHONE_ALREADY_USED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task RegisterTeacher_MissingPhoneNumber_Returns400()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(Unique("tch_nophone")), "Username" },
            { new StringContent($"{Unique("tch-nophone")}@mathinsight.test"), "Email" },
            { new StringContent(ValidPassword), "Password" },
            { new StringContent("New"), "FirstName" },
            { new StringContent("Teacher"), "LastName" }
        };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("fake"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(file, "Certificates", "cert.png");
        var client = CreateClient();

        var response = await client.PostAsync("/api/v1/auth/register/teacher", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ================================================================ POST /api/v1/auth/reset-password

    [Fact]
    public async Task ResetPassword_KnownEmail_Returns200AndSendsAResetLink()
    {
        var email = $"{Unique("rst-ok")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("rst_ok"));
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(Factory.Emails.LastTokenFor("reset", email));
    }

    [Fact]
    public async Task ResetPassword_UnknownEmail_Returns200WithTheSameBodyAndSendsNothing()
    {
        // UC-06 enumeration protection, observed at the API surface.
        var knownEmail = $"{Unique("rst-enum")}@mathinsight.test";
        await SeedAccountAsync(knownEmail, Unique("rst_enum"));
        var unknownEmail = $"{Unique("rst-ghost")}@mathinsight.test";
        var client = CreateClient();

        var known = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email = knownEmail });
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email = unknownEmail });

        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(
            await known.Content.ReadAsStringAsync(),
            await unknown.Content.ReadAsStringAsync());
        Assert.Null(Factory.Emails.LastTokenFor("reset", unknownEmail));
    }

    [Fact]
    public async Task ResetPassword_MissingEmail_Returns400()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ================================================================ POST /api/v1/auth/confirm-reset-password

    [Fact]
    public async Task ConfirmResetPassword_ValidToken_Returns200AndTheNewPasswordWorks()
    {
        const string newPassword = "Rotated#Password9";
        var email = $"{Unique("crp-ok")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("crp_ok"));
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email });
        var token = Factory.Emails.LastTokenFor("reset", email);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-reset-password", new { token, newPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email, newPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, email, ValidPassword)).StatusCode);
    }

    [Fact]
    public async Task ConfirmResetPassword_ReplayedToken_Returns410()
    {
        var email = $"{Unique("crp-replay")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("crp_replay"));
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email });
        var token = Factory.Emails.LastTokenFor("reset", email);
        await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-reset-password", new { token, newPassword = "Rotated#Password9" });

        var replay = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-reset-password", new { token, newPassword = "Another#Password9" });

        Assert.Equal(HttpStatusCode.Gone, replay.StatusCode);
    }

    [Fact]
    public async Task ConfirmResetPassword_UnknownToken_Returns410()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-reset-password",
            new { token = "never-issued", newPassword = "Rotated#Password9" });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmResetPassword_WeakNewPassword_Returns400WithoutConsumingTheToken()
    {
        var email = $"{Unique("crp-weak")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("crp_weak"));
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email });
        var token = Factory.Emails.LastTokenFor("reset", email);

        var weak = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-reset-password", new { token, newPassword = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        // The token survived the rejected attempt and still works.
        var retry = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-reset-password", new { token, newPassword = "Rotated#Password9" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task ConfirmResetPassword_RevokesExistingSessions()
    {
        // BR-15: the refresh token issued before the reset must stop working.
        var email = $"{Unique("crp-rev")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("crp_rev"));
        var client = CreateClient();
        var login = await ReadJsonAsync(await LoginAsync(client, email, ValidPassword));
        var refreshToken = login.GetProperty("refreshToken").GetString();
        await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email });
        var token = Factory.Emails.LastTokenFor("reset", email);

        await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-reset-password", new { token, newPassword = "Rotated#Password9" });

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }
}
