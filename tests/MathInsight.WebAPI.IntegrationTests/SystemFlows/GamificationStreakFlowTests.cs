using System.Net;
using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Services;
using MathInsight.Shared.Events;
using MathInsight.WebAPI.IntegrationTests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace MathInsight.WebAPI.IntegrationTests.SystemFlows;

/// <summary>
/// L3 system tests — multi-step HTTP/business flows for the Gamification streak feature.
///
/// BF-GAM-01 walks the student journey: sign in over HTTP, record qualifying activity through the
/// real in-process event pipeline, then read the streak back over HTTP, checking the database
/// between every step.
///
/// BF-GAM-02 drives the BR-45 reminder sweep by calling the scan service the background timer would
/// call, and observes the events the real MediatR pipeline publishes.
///
/// Each [Fact] is one flow; the steps inside are ordered and each asserts before the next runs, so a
/// failure names the step that broke.
/// </summary>
public class GamificationStreakFlowTests : AuthTestBase
{
    private readonly ITestOutputHelper _output;

    public GamificationStreakFlowTests(AuthApiFactory factory, ITestOutputHelper output) : base(factory) =>
        _output = output;

    private const string StreakRoute = "/api/v1/gamification/streak";

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private Task<StudyStreak?> LoadStreakAsync(string studentId) =>
        Factory.FromGamificationDbAsync(db =>
            db.StudyStreaks.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == studentId));

    private Task<int> CountActivityLogsAsync(string studentId) =>
        Factory.FromGamificationDbAsync(db =>
            db.ActivityLogs.AsNoTracking().CountAsync(a => a.StudentId == studentId));

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

    /// <summary>
    /// Publishes the submission event exactly as Testing's SubmitSessionCommandHandler does, through
    /// the host's real MediatR — so TestSubmittedHandler, StreakService, BadgeService and the
    /// GamificationDbContext all run for real. SubmittedTime drives the streak's calendar date.
    /// </summary>
    private Task SubmitTestAsync(string studentId, DateTime submittedTimeUtc, string format = "Practice") =>
        Factory.WithScopedAsync<IMediator>(mediator => mediator.Publish(new TestSubmittedEvent
        {
            SessionId = Guid.NewGuid().ToString(),
            StudentId = studentId,
            TestId = Guid.NewGuid().ToString(),
            TestFormat = format,
            SubmissionType = "StudentSubmit",
            SubmittedTime = submittedTimeUtc
        }));

    // ================================================================================
    // BF-GAM-01 — Study activity advances streak
    // ================================================================================

    [Fact]
    public async Task BF_GAM_01_StudyActivityAdvancesStreak()
    {
        var email = $"{Unique("bf1")}@mathinsight.test";
        var studentId = await SeedAccountAsync(email, Unique("bf1_user"));

        // ---- Step 1: the student signs in ------------------------------------------------
        var client = CreateClient();
        var loginResponse = await LoginAsync(client, email, ValidPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await ReadJsonAsync(loginResponse);
        var accessToken = login.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        _output.WriteLine("Step 1 OK — signed in, bearer token issued.");

        // ---- Step 2: baseline — a brand-new student has no streak yet ---------------------
        var baseline = await client.GetAsync(StreakRoute);
        Assert.Equal(HttpStatusCode.OK, baseline.StatusCode);
        var baselineBody = await ReadJsonAsync(baseline);
        Assert.Equal(0, baselineBody.GetProperty("currentStreak").GetInt32());
        Assert.False(baselineBody.GetProperty("isActive").GetBoolean());
        Assert.Null(await LoadStreakAsync(studentId));   // no row exists yet
        _output.WriteLine("Step 2 OK — baseline zeros, no StudyStreak row.");

        // ---- Step 3: a qualifying activity yesterday creates the streak at day one --------
        var yesterday = DateTime.UtcNow.AddDays(-1);
        await SubmitTestAsync(studentId, yesterday);

        var afterFirst = await LoadStreakAsync(studentId);
        Assert.NotNull(afterFirst);
        Assert.Equal(1, afterFirst!.CurrentStreak);
        Assert.Equal(1, afterFirst.LongestStreak);
        Assert.Equal(DateOnly.FromDateTime(yesterday), afterFirst.LastActivityDate);
        Assert.Equal(1, await CountActivityLogsAsync(studentId));   // BR-40 append-only log
        _output.WriteLine("Step 3 OK — StudyStreak created at 1, ActivityLog written.");

        // ---- Step 4: the API reflects the new streak, still active (yesterday counts) -----
        var afterFirstResponse = await client.GetAsync(StreakRoute);
        Assert.Equal(HttpStatusCode.OK, afterFirstResponse.StatusCode);
        var afterFirstBody = await ReadJsonAsync(afterFirstResponse);
        Assert.Equal(1, afterFirstBody.GetProperty("currentStreak").GetInt32());
        Assert.Equal(1, afterFirstBody.GetProperty("longestStreak").GetInt32());
        Assert.True(afterFirstBody.GetProperty("isActive").GetBoolean());
        _output.WriteLine("Step 4 OK — GET /streak reports 1 and isActive.");

        // ---- Step 5: a second qualifying activity today continues the run ----------------
        await SubmitTestAsync(studentId, DateTime.UtcNow, format: "Exam");

        var afterSecond = await LoadStreakAsync(studentId);
        Assert.Equal(2, afterSecond!.CurrentStreak);
        Assert.Equal(2, afterSecond.LongestStreak);   // BR-42: longest grows with it
        Assert.Equal(Today, afterSecond.LastActivityDate);
        Assert.Equal(2, await CountActivityLogsAsync(studentId));
        _output.WriteLine("Step 5 OK — consecutive day advanced the streak to 2.");

        // ---- Step 6: the API agrees with the database ------------------------------------
        var afterSecondBody = await ReadJsonAsync(await client.GetAsync(StreakRoute));
        Assert.Equal(2, afterSecondBody.GetProperty("currentStreak").GetInt32());
        Assert.Equal(2, afterSecondBody.GetProperty("longestStreak").GetInt32());
        Assert.Equal(Today.ToString("yyyy-MM-dd"), afterSecondBody.GetProperty("lastActivityDate").GetString());
        Assert.True(afterSecondBody.GetProperty("isActive").GetBoolean());
        _output.WriteLine("Step 6 OK — GET /streak matches the persisted row.");

        // ---- Step 7: a second activity the SAME day must not double-count -----------------
        await SubmitTestAsync(studentId, DateTime.UtcNow);

        var afterRepeat = await LoadStreakAsync(studentId);
        Assert.Equal(2, afterRepeat!.CurrentStreak);   // BR-39 idempotency
        Assert.Equal(2, afterRepeat.LongestStreak);
        Assert.Equal(3, await CountActivityLogsAsync(studentId));   // but the log still records it
        var afterRepeatBody = await ReadJsonAsync(await client.GetAsync(StreakRoute));
        Assert.Equal(2, afterRepeatBody.GetProperty("currentStreak").GetInt32());
        _output.WriteLine("Step 7 OK — same-day repeat logged but streak unchanged.");
    }

    [Fact]
    public async Task BF_GAM_01_NonQualifyingActivityDoesNotAdvanceTheStreak()
    {
        // Alternative flow of BF-GAM-01: a DOWNLOAD_MATERIAL activity is recorded (BR-40) but must
        // never move the streak (BR-39).
        var email = $"{Unique("bf1b")}@mathinsight.test";
        var studentId = await SeedAccountAsync(email, Unique("bf1b_user"));
        var (client, _) = await AuthenticatedClientAsync(email);

        await Factory.WithScopedAsync<IMediator>(mediator => mediator.Publish(new ActivityLoggedEvent(
            StudentId: studentId,
            ActivityType: "DOWNLOAD_MATERIAL",
            LectureId: null,
            MaterialId: Guid.NewGuid().ToString(),
            DurationSeconds: 100_000)));

        Assert.Equal(1, await CountActivityLogsAsync(studentId));   // logged
        Assert.Null(await LoadStreakAsync(studentId));               // but no streak row

        var body = await ReadJsonAsync(await client.GetAsync(StreakRoute));
        Assert.Equal(0, body.GetProperty("currentStreak").GetInt32());
        Assert.False(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task BF_GAM_01_LectureViewEventCarriesZeroDurationAndCannotAdvanceTheStreak()
    {
        // Reproduces what the CURRENTLY CHECKED-OUT Learning module publishes:
        // GetLectureQueryHandler.cs:46 still sends DurationSeconds: 0, and BR-39 requires >= 300 for
        // VIEW_LECTURE. The awaited fix (LogLectureViewCommandHandler on feature/learning-lecture)
        // is not merged into main yet — until it is, a lecture view cannot advance a streak however
        // long the student actually watches.
        var email = $"{Unique("bf1c")}@mathinsight.test";
        var studentId = await SeedAccountAsync(email, Unique("bf1c_user"));

        await Factory.WithScopedAsync<IMediator>(mediator => mediator.Publish(new ActivityLoggedEvent(
            StudentId: studentId,
            ActivityType: "VIEW_LECTURE",
            LectureId: Guid.NewGuid().ToString(),
            MaterialId: null,
            DurationSeconds: 0)));   // exactly what GetLectureQueryHandler publishes today

        Assert.Equal(1, await CountActivityLogsAsync(studentId));
        Assert.Null(await LoadStreakAsync(studentId));   // BR-39 not satisfied → no streak
    }

    [Fact]
    public async Task BF_GAM_01_LectureViewOfFiveMinutes_AdvancesTheStreakEndToEnd()
    {
        // The consuming half of the fix: once Learning publishes a REAL duration, the whole chain
        // (ActivityLoggedHandler → StreakService → StudyStreak → GET /streak) must carry a lecture
        // view into an advanced streak. Publishing the event with 300s is exactly what
        // LogLectureViewCommandHandler does on feature/learning-lecture, so this proves the
        // Gamification side is ready and the remaining blocker is the un-merged Learning change.
        var email = $"{Unique("bf1d")}@mathinsight.test";
        var studentId = await SeedAccountAsync(email, Unique("bf1d_user"));
        var (client, _) = await AuthenticatedClientAsync(email);

        await Factory.WithScopedAsync<IMediator>(mediator => mediator.Publish(new ActivityLoggedEvent(
            StudentId: studentId,
            ActivityType: "VIEW_LECTURE",
            LectureId: Guid.NewGuid().ToString(),
            MaterialId: null,
            DurationSeconds: 300)));   // BR-39 boundary, exactly five minutes

        var streak = await LoadStreakAsync(studentId);
        Assert.NotNull(streak);
        Assert.Equal(1, streak!.CurrentStreak);
        Assert.Equal(1, streak.LongestStreak);
        Assert.Equal(Today, streak.LastActivityDate);
        Assert.Equal(1, await CountActivityLogsAsync(studentId));

        var body = await ReadJsonAsync(await client.GetAsync(StreakRoute));
        Assert.Equal(1, body.GetProperty("currentStreak").GetInt32());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        _output.WriteLine("A 300-second lecture view advanced the streak to 1 and is visible over HTTP.");
    }

    [Fact]
    public async Task BF_GAM_01_LectureViewJustUnderFiveMinutes_DoesNotAdvanceTheStreak()
    {
        // The other side of the BR-39 boundary, driven through the same real pipeline.
        var email = $"{Unique("bf1e")}@mathinsight.test";
        var studentId = await SeedAccountAsync(email, Unique("bf1e_user"));
        var (client, _) = await AuthenticatedClientAsync(email);

        await Factory.WithScopedAsync<IMediator>(mediator => mediator.Publish(new ActivityLoggedEvent(
            StudentId: studentId,
            ActivityType: "VIEW_LECTURE",
            LectureId: Guid.NewGuid().ToString(),
            MaterialId: null,
            DurationSeconds: 299)));

        Assert.Equal(1, await CountActivityLogsAsync(studentId));   // still logged (BR-40)
        Assert.Null(await LoadStreakAsync(studentId));              // but no streak

        var body = await ReadJsonAsync(await client.GetAsync(StreakRoute));
        Assert.Equal(0, body.GetProperty("currentStreak").GetInt32());
        Assert.False(body.GetProperty("isActive").GetBoolean());
    }

    // ================================================================================
    // BF-GAM-02 — Streak reminder sweep
    // ================================================================================

    [Fact]
    public async Task BF_GAM_02_StreakReminderSweep()
    {
        Factory.StreakReminders.Clear();

        // ---- Step 1: seed the population the sweep will scan ------------------------------
        var inactiveId = $"bf2-inactive-{Guid.NewGuid():N}";
        var activeId = $"bf2-active-{Guid.NewGuid():N}";
        var neverActiveId = $"bf2-null-{Guid.NewGuid():N}";

        await SeedStreakAsync(inactiveId, current: 4, longest: 6, lastActivity: Today.AddDays(-1));
        await SeedStreakAsync(activeId, current: 9, longest: 9, lastActivity: Today);
        await SeedStreakAsync(neverActiveId, current: 0, longest: 3, lastActivity: null);
        _output.WriteLine("Step 1 OK — three StudyStreak rows seeded (inactive, active, never-dated).");

        // ---- Step 2: run the scan the background timer would run --------------------------
        // Called directly, exactly as StreakReminderBackgroundService does inside its own scope —
        // the timer itself is deliberately not exercised.
        var published = await Factory.FromScopedAsync<IStreakReminderService, int>(
            service => service.SendRemindersAsync(Today));

        Assert.True(published >= 2, $"expected at least the two inactive students, got {published}");
        _output.WriteLine($"Step 2 OK — sweep completed, {published} reminder(s) published.");

        // ---- Step 3: the right students were reminded, and only them ----------------------
        var received = Factory.StreakReminders.Received;

        var inactiveReminder = Assert.Single(received, r => r.StudentId == inactiveId);
        Assert.Equal(4, inactiveReminder.CurrentStreak);
        Assert.Equal(Today.AddDays(-1), inactiveReminder.LastActivityDate);

        var neverActiveReminder = Assert.Single(received, r => r.StudentId == neverActiveId);
        Assert.Null(neverActiveReminder.LastActivityDate);

        Assert.DoesNotContain(received, r => r.StudentId == activeId);   // active today → no nag
        _output.WriteLine("Step 3 OK — inactive students reminded, the active student was not.");

        // ---- Step 4: the sweep is read-only ----------------------------------------------
        var untouched = await LoadStreakAsync(inactiveId);
        Assert.Equal(4, untouched!.CurrentStreak);
        Assert.Equal(6, untouched.LongestStreak);
        Assert.Equal(Today.AddDays(-1), untouched.LastActivityDate);

        var stillActive = await LoadStreakAsync(activeId);
        Assert.Equal(9, stillActive!.CurrentStreak);
        Assert.Equal(Today, stillActive.LastActivityDate);
        _output.WriteLine("Step 4 OK — no StudyStreak row was mutated by the sweep.");
    }

    [Fact]
    public async Task BF_GAM_02_ActivityTodayStopsTheReminder()
    {
        // End-to-end alternative flow: a student who WOULD have been reminded records a qualifying
        // activity first, and is then excluded from the same sweep.
        Factory.StreakReminders.Clear();

        var studentId = $"bf2b-{Guid.NewGuid():N}";
        await SeedStreakAsync(studentId, current: 4, longest: 6, lastActivity: Today.AddDays(-1));

        // Sweep before the activity → reminded.
        await Factory.FromScopedAsync<IStreakReminderService, int>(s => s.SendRemindersAsync(Today));
        Assert.Contains(Factory.StreakReminders.Received, r => r.StudentId == studentId);

        // The student studies today; the streak advances to 5.
        await SubmitTestAsync(studentId, DateTime.UtcNow);
        var advanced = await LoadStreakAsync(studentId);
        Assert.Equal(5, advanced!.CurrentStreak);
        Assert.Equal(Today, advanced.LastActivityDate);

        // Sweep again → this student is no longer a candidate.
        Factory.StreakReminders.Clear();
        await Factory.FromScopedAsync<IStreakReminderService, int>(s => s.SendRemindersAsync(Today));
        Assert.DoesNotContain(Factory.StreakReminders.Received, r => r.StudentId == studentId);
    }
}
