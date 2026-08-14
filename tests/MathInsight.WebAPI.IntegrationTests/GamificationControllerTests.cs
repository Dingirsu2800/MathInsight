using System.Net;
using MathInsight.Modules.Gamification.Entities;
using MathInsight.WebAPI.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.WebAPI.IntegrationTests;

/// <summary>
/// L2 integration tests for GamificationController — GET /api/v1/gamification/streak (UC-81).
///
/// Two properties only this level can prove: the [Authorize(Roles = "Student")] gate, and that the
/// student id comes from the token's claims rather than from anything the caller can supply — there
/// is no route or query parameter that can point the query at another student's streak.
///
/// The UC-81 display rule (a lapsed streak shows 0 without the row being rewritten) is asserted
/// against the persisted StudyStreak row after the HTTP call, not just against the response body.
/// </summary>
public class GamificationControllerTests : AuthTestBase
{
    public GamificationControllerTests(AuthApiFactory factory) : base(factory) { }

    private const string Route = "/api/v1/gamification/streak";

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private Task SeedStreakAsync(string studentId, int current, int longest, DateOnly? lastActivity) =>
        Factory.WithGamificationDbAsync(async db =>
        {
            db.StudyStreaks.Add(new StudyStreak
            {
                StreakId = Guid.NewGuid().ToString(),
                StudentId = studentId,
                CurrentStreak = current,
                LongestStreak = longest,
                LastActivityDate = lastActivity
            });
            await db.SaveChangesAsync();
        });

    private Task<StudyStreak?> LoadStreakAsync(string studentId) =>
        Factory.FromGamificationDbAsync(db =>
            db.StudyStreaks.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == studentId));

    /// <summary>Seeds a Student account and signs it in through the real login endpoint.</summary>
    private async Task<(HttpClient Client, string AccountId)> StudentClientAsync(string prefix)
    {
        var email = $"{Unique(prefix)}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique(prefix.Replace('-', '_')));
        var (client, _) = await AuthenticatedClientAsync(email);
        return (client, accountId);
    }

    // ================================================================ auth & role

    [Fact]
    public async Task GetStreak_WithoutToken_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStreak_WithMalformedBearerToken_Returns401()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStreak_AsTeacher_Returns403()
    {
        // Role-gated: a valid token for the wrong role is forbidden, not unauthorized.
        var email = $"{Unique("gam-tch")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("gam_tch"), AuthApiFactory.TeacherRoleId);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetStreak_AsAdmin_Returns403()
    {
        // Streaks are a student concept; even an Admin token does not open this endpoint.
        var email = $"{Unique("gam-adm")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("gam_adm"), AuthApiFactory.AdminRoleId);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetStreak_AsExpert_Returns403()
    {
        var email = $"{Unique("gam-exp")}@mathinsight.test";
        await SeedAccountAsync(email, Unique("gam_exp"), AuthApiFactory.ExpertRoleId);
        var (client, _) = await AuthenticatedClientAsync(email);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ================================================================ happy paths

    [Fact]
    public async Task GetStreak_ActiveStreakToday_Returns200WithTheStoredCounters()
    {
        var (client, accountId) = await StudentClientAsync("gam-today");
        await SeedStreakAsync(accountId, current: 5, longest: 7, lastActivity: Today);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(5, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(7, body.GetProperty("longestStreak").GetInt32());
        Assert.Equal(Today.ToString("yyyy-MM-dd"), body.GetProperty("lastActivityDate").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task GetStreak_LastActivityYesterday_IsStillActive()
    {
        var (client, accountId) = await StudentClientAsync("gam-yday");
        await SeedStreakAsync(accountId, current: 3, longest: 9, lastActivity: Today.AddDays(-1));

        var body = await ReadJsonAsync(await client.GetAsync(Route));

        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.Equal(3, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(9, body.GetProperty("longestStreak").GetInt32());
    }

    [Fact]
    public async Task GetStreak_NoStreakRow_Returns200WithZeros()
    {
        // A student who has never had a qualifying activity is a zero streak, not an error.
        var (client, _) = await StudentClientAsync("gam-norow");

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(0, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(0, body.GetProperty("longestStreak").GetInt32());
        Assert.Null(StringOrNull(body, "lastActivityDate"));
        Assert.False(body.GetProperty("isActive").GetBoolean());
    }

    // ================================================================ UC-81 display rule

    [Fact]
    public async Task GetStreak_BrokenStreak_Displays0WithoutMutatingTheRow()
    {
        var (client, accountId) = await StudentClientAsync("gam-broken");
        var threeDaysAgo = Today.AddDays(-3);
        await SeedStreakAsync(accountId, current: 4, longest: 6, lastActivity: threeDaysAgo);

        var body = await ReadJsonAsync(await client.GetAsync(Route));

        Assert.False(body.GetProperty("isActive").GetBoolean());
        Assert.Equal(0, body.GetProperty("currentStreak").GetInt32());   // display value
        Assert.Equal(6, body.GetProperty("longestStreak").GetInt32());   // all-time best survives
        Assert.Equal(threeDaysAgo.ToString("yyyy-MM-dd"), body.GetProperty("lastActivityDate").GetString());

        // The read must not write the zero back — StreakService owns the real reset.
        var stored = await LoadStreakAsync(accountId);
        Assert.Equal(4, stored!.CurrentStreak);
        Assert.Equal(threeDaysAgo, stored.LastActivityDate);
    }

    [Fact]
    public async Task GetStreak_ExactlyTwoDaysAgo_IsBroken()
    {
        // The first day on the inactive side of the today/yesterday window.
        var (client, accountId) = await StudentClientAsync("gam-2days");
        await SeedStreakAsync(accountId, current: 8, longest: 8, lastActivity: Today.AddDays(-2));

        var body = await ReadJsonAsync(await client.GetAsync(Route));

        Assert.False(body.GetProperty("isActive").GetBoolean());
        Assert.Equal(0, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(8, body.GetProperty("longestStreak").GetInt32());
    }

    [Fact]
    public async Task GetStreak_RepeatedCalls_AreIdempotent()
    {
        // Guards against a display rule that "helpfully" persists the zero on read.
        var (client, accountId) = await StudentClientAsync("gam-idem");
        await SeedStreakAsync(accountId, current: 6, longest: 10, lastActivity: Today.AddDays(-4));

        var first = await (await client.GetAsync(Route)).Content.ReadAsStringAsync();
        var second = await (await client.GetAsync(Route)).Content.ReadAsStringAsync();

        Assert.Equal(first, second);
        var stored = await LoadStreakAsync(accountId);
        Assert.Equal(6, stored!.CurrentStreak);
        Assert.Equal(10, stored.LongestStreak);
    }

    [Fact]
    public async Task GetStreak_NullLastActivityDate_IsReportedAsBroken()
    {
        var (client, accountId) = await StudentClientAsync("gam-null");
        await SeedStreakAsync(accountId, current: 5, longest: 6, lastActivity: null);

        var body = await ReadJsonAsync(await client.GetAsync(Route));

        Assert.False(body.GetProperty("isActive").GetBoolean());
        Assert.Equal(0, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(6, body.GetProperty("longestStreak").GetInt32());
        Assert.Null(StringOrNull(body, "lastActivityDate"));
    }

    // ================================================================ ownership / scoping

    [Fact]
    public async Task GetStreak_ReturnsOnlyTheCallersOwnStreak()
    {
        var (client, callerId) = await StudentClientAsync("gam-mine");
        await SeedStreakAsync(callerId, current: 2, longest: 3, lastActivity: Today);

        var otherEmail = $"{Unique("gam-other")}@mathinsight.test";
        var otherId = await SeedAccountAsync(otherEmail, Unique("gam_other"));
        await SeedStreakAsync(otherId, current: 99, longest: 99, lastActivity: Today);

        var body = await ReadJsonAsync(await client.GetAsync(Route));

        Assert.Equal(2, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(3, body.GetProperty("longestStreak").GetInt32());   // never the other student's 99
    }

    [Fact]
    public async Task GetStreak_QueryStringStudentId_IsIgnored()
    {
        // There is no input binding for the student id — it comes from the token claim only.
        var (client, callerId) = await StudentClientAsync("gam-tamper");
        await SeedStreakAsync(callerId, current: 2, longest: 3, lastActivity: Today);

        var victimEmail = $"{Unique("gam-victim")}@mathinsight.test";
        var victimId = await SeedAccountAsync(victimEmail, Unique("gam_victim"));
        await SeedStreakAsync(victimId, current: 42, longest: 50, lastActivity: Today);

        var body = await ReadJsonAsync(await client.GetAsync($"{Route}?studentId={victimId}"));

        Assert.Equal(2, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(3, body.GetProperty("longestStreak").GetInt32());
    }

    [Fact]
    public async Task GetStreak_AnotherStudentsStreakIsUnreachableByAnyRouteVariant()
    {
        // /streak/{someoneElse} is simply not a route — no id-bearing variant of this endpoint exists.
        var (client, _) = await StudentClientAsync("gam-route");
        var victimEmail = $"{Unique("gam-vic2")}@mathinsight.test";
        var victimId = await SeedAccountAsync(victimEmail, Unique("gam_vic2"));
        await SeedStreakAsync(victimId, current: 42, longest: 50, lastActivity: Today);

        var response = await client.GetAsync($"{Route}/{victimId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStreak_TokenForADeletedAccount_Returns200WithZeros()
    {
        // Documents current behaviour: the query treats a missing streak row as a zero streak, so a
        // token whose account no longer exists yields zeros rather than 401 — the endpoint never
        // reads the Account table.
        var email = $"{Unique("gam-gone")}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique("gam_gone"));
        var (client, _) = await AuthenticatedClientAsync(email);
        await DeleteAccountAsync(accountId);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(0, body.GetProperty("currentStreak").GetInt32());
        Assert.False(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task GetStreak_AfterLogout_Returns401()
    {
        // BR-10: the access token is blacklisted at logout, so the session is genuinely closed.
        var email = $"{Unique("gam-out")}@mathinsight.test";
        var accountId = await SeedAccountAsync(email, Unique("gam_out"));
        await SeedStreakAsync(accountId, current: 5, longest: 5, lastActivity: Today);
        var (client, refreshToken) = await AuthenticatedClientAsync(email);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Route)).StatusCode);

        await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken });

        var afterLogout = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }
}
