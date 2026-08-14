using System.Net;
using System.Text;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.WebAPI.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.WebAPI.IntegrationTests;

/// <summary>
/// L2 integration tests for TeacherApplicationController (UC-08 self-service).
///
/// Two things only this level can prove: the "TeacherApplicant" policy admits an UNAPPROVED Teacher
/// but rejects other roles, and the ownership check — the {applicationId} in the route is untrusted,
/// so another teacher's application must come back 403 no matter what id is supplied.
/// </summary>
public class TeacherApplicationControllerTests : AuthTestBase
{
    public TeacherApplicationControllerTests(AuthApiFactory factory) : base(factory) { }

    private const string Route = "/api/v1/teacher/application";

    /// <summary>Seeds an unapproved Teacher with an application in the given state.</summary>
    private async Task<(string Email, string AccountId, string ApplicationId)> SeedApplicantAsync(
        string prefix,
        string status,
        string documentsUrl = "https://cdn.test/cert-1.png")
    {
        var email = $"{Unique(prefix)}@mathinsight.test";
        var accountId = await SeedAccountAsync(
            email, Unique(prefix.Replace('-', '_')), AuthApiFactory.TeacherRoleId, phoneNumber: null);
        await SeedTeacherRowAsync(accountId);
        var applicationId = await SeedTeacherApplicationAsync(accountId, status, documentsUrl);

        return (email, accountId, applicationId);
    }

    private static MultipartFormDataContent EditForm(
        string phoneNumber,
        IEnumerable<string>? keptDocumentsUrls = null,
        int newCertificateCount = 0,
        string firstName = "Edited",
        string lastName = "Teacher")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(firstName), "FirstName" },
            { new StringContent(lastName), "LastName" },
            { new StringContent(phoneNumber), "PhoneNumber" },
            { new StringContent("Updated biography."), "Biography" }
        };

        foreach (var url in keptDocumentsUrls ?? [])
        {
            form.Add(new StringContent(url), "KeptDocumentsUrls");
        }

        for (var index = 0; index < newCertificateCount; index++)
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes($"fake-certificate-{index}"));
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, "Certificates", $"new-certificate-{index}.png");
        }

        return form;
    }

    private Task<TeacherApplication> LoadApplicationAsync(string applicationId) =>
        Factory.FromIdentityDbAsync(db =>
            db.TeacherApplications.AsNoTracking().FirstAsync(a => a.ApplicationId == applicationId));

    // ================================================================ GET /api/v1/teacher/application

    [Fact]
    public async Task GetMyApplication_WithoutToken_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyApplication_AsStudent_Returns403()
    {
        // TeacherApplicant is role-gated: a valid token for the wrong role is forbidden, not 401.
        var email = $"{Unique("app-stu")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("app_stu"));
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMyApplication_RejectedApplicant_Returns200WithCanEditTrue()
    {
        // The policy admits an unapproved teacher — that is the whole point of these endpoints.
        var (email, accountId, applicationId) = await SeedApplicantAsync(
            "app-rej", TeacherApplication.StatusRejected,
            "https://cdn.test/a.png\nhttps://cdn.test/b.png");
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(applicationId, body.GetProperty("applicationId").GetString());
        Assert.Equal(accountId, body.GetProperty("teacherId").GetString());
        Assert.Equal("Rejected", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("canEdit").GetBoolean());
        Assert.Equal("Certificate unreadable.", body.GetProperty("reviewComments").GetString());
        Assert.Equal(2, body.GetProperty("documentsUrls").GetArrayLength());   // split from the packed column
    }

    [Fact]
    public async Task GetMyApplication_PendingApplicant_Returns200WithCanEditFalse()
    {
        var (email, _, _) = await SeedApplicantAsync("app-pend", TeacherApplication.StatusPending);
        var (client, _) = await AuthenticatedClientAsync(email);

        var body = await ReadJsonAsync(await client.GetAsync(Route));

        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.False(body.GetProperty("canEdit").GetBoolean());
    }

    [Fact]
    public async Task GetMyApplication_TeacherWithNoApplication_Returns404()
    {
        // UC-11: an Admin-created Teacher never filed one.
        var email = $"{Unique("app-none")}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique("app_none"), AuthApiFactory.TeacherRoleId);
        await SeedTeacherRowAsync(accountId);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("APPLICATION_NOT_FOUND", body.GetProperty("code").GetString());
    }

    // ================================================================ PUT /api/v1/teacher/application/{id}

    [Fact]
    public async Task UpdateMyApplication_WithoutToken_Returns401()
    {
        var client = CreateClient();

        var response = await client.PutAsync($"{Route}/any-id", EditForm("0900200001"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMyApplication_AsStudent_Returns403()
    {
        var email = $"{Unique("upd-stu")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("upd_stu"));
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsync($"{Route}/any-id", EditForm("0900200002"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMyApplication_OwnRejectedApplication_Returns200AndPersists()
    {
        var (email, _, applicationId) = await SeedApplicantAsync(
            "upd-ok", TeacherApplication.StatusRejected, "https://cdn.test/keep.png");
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsync(
            $"{Route}/{applicationId}",
            EditForm("0900200003", keptDocumentsUrls: ["https://cdn.test/keep.png"], newCertificateCount: 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var application = await LoadApplicationAsync(applicationId);
        Assert.Contains("https://cdn.test/keep.png", application.DocumentsUrl);
        Assert.Equal(2, application.DocumentsUrl.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal("Rejected", application.Status);   // editing does not itself resubmit
    }

    [Fact]
    public async Task UpdateMyApplication_AnotherTeachersApplication_Returns403()
    {
        // The route id is untrusted; ownership comes from the token.
        var (_, _, victimApplicationId) = await SeedApplicantAsync("upd-victim", TeacherApplication.StatusRejected);
        var (attackerEmail, _, _) = await SeedApplicantAsync("upd-attacker", TeacherApplication.StatusRejected);
        var (client, _) = await AuthenticatedClientAsync(attackerEmail);

        var response = await client.PutAsync($"{Route}/{victimApplicationId}", EditForm("0900200004"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("APPLICATION_FORBIDDEN", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateMyApplication_UnknownApplicationId_Returns404()
    {
        var (email, _, _) = await SeedApplicantAsync("upd-404", TeacherApplication.StatusRejected);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsync($"{Route}/{Guid.NewGuid()}", EditForm("0900200005"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMyApplication_PendingApplication_Returns409()
    {
        // Only a Rejected application is editable.
        var (email, _, applicationId) = await SeedApplicantAsync("upd-pend", TeacherApplication.StatusPending);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsync(
            $"{Route}/{applicationId}",
            EditForm("0900200006", keptDocumentsUrls: ["https://cdn.test/cert-1.png"]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("APPLICATION_NOT_EDITABLE", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateMyApplication_DroppingEveryCertificate_Returns400()
    {
        // BR-05: an application must always carry at least one certificate.
        var (email, _, applicationId) = await SeedApplicantAsync("upd-nocert", TeacherApplication.StatusRejected);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsync($"{Route}/{applicationId}", EditForm("0900200007"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("AUTH_CERTIFICATE_INVALID", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateMyApplication_DuplicatePhoneNumber_Returns409()
    {
        const string phone = "0900200099";
        await SeedAccountAsync(
            $"{Unique("upd-phowner")}@mathinsight.test", Unique("upd_phowner"),
            AuthApiFactory.StudentRoleId, phoneNumber: phone);
        var (email, _, applicationId) = await SeedApplicantAsync("upd-ph", TeacherApplication.StatusRejected);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsync(
            $"{Route}/{applicationId}",
            EditForm(phone, keptDocumentsUrls: ["https://cdn.test/cert-1.png"]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("AUTH_PHONE_ALREADY_USED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateMyApplication_MalformedPhoneNumber_Returns400()
    {
        var (email, _, applicationId) = await SeedApplicantAsync("upd-phbad", TeacherApplication.StatusRejected);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsync(
            $"{Route}/{applicationId}",
            EditForm("not-a-phone", keptDocumentsUrls: ["https://cdn.test/cert-1.png"]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMyApplication_KeptUrlNotOnTheApplication_Returns400()
    {
        // A client cannot inject arbitrary URLs into DocumentsUrl through KeptDocumentsUrls.
        var (email, _, applicationId) = await SeedApplicantAsync("upd-inject", TeacherApplication.StatusRejected);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PutAsync(
            $"{Route}/{applicationId}",
            EditForm("0900200008", keptDocumentsUrls: ["https://evil.test/injected.png"]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ================================================================ POST /api/v1/teacher/application/{id}/resubmit

    [Fact]
    public async Task Resubmit_WithoutToken_Returns401()
    {
        var client = CreateClient();

        var response = await client.PostAsync($"{Route}/any-id/resubmit", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Resubmit_AsStudent_Returns403()
    {
        var email = $"{Unique("res-stu")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("res_stu"));
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PostAsync($"{Route}/any-id/resubmit", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resubmit_OwnRejectedApplication_Returns200AndSetsStatusPending()
    {
        var (email, _, applicationId) = await SeedApplicantAsync("res-ok", TeacherApplication.StatusRejected);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PostAsync($"{Route}/{applicationId}/resubmit", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var application = await LoadApplicationAsync(applicationId);
        Assert.Equal("Pending", application.Status);   // back in the Admin queue
    }

    [Fact]
    public async Task Resubmit_AlreadyPending_Returns409()
    {
        var (email, _, applicationId) = await SeedApplicantAsync("res-pend", TeacherApplication.StatusPending);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PostAsync($"{Route}/{applicationId}/resubmit", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("APPLICATION_NOT_EDITABLE", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Resubmit_AnotherTeachersApplication_Returns403()
    {
        var (_, _, victimApplicationId) = await SeedApplicantAsync("res-victim", TeacherApplication.StatusRejected);
        var (attackerEmail, _, _) = await SeedApplicantAsync("res-attacker", TeacherApplication.StatusRejected);
        var (client, _) = await AuthenticatedClientAsync(attackerEmail);

        var response = await client.PostAsync($"{Route}/{victimApplicationId}/resubmit", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var untouched = await LoadApplicationAsync(victimApplicationId);
        Assert.Equal("Rejected", untouched.Status);
    }

    [Fact]
    public async Task Resubmit_UnknownApplicationId_Returns404()
    {
        var (email, _, _) = await SeedApplicantAsync("res-404", TeacherApplication.StatusRejected);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.PostAsync($"{Route}/{Guid.NewGuid()}/resubmit", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
