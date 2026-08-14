using System.Net;
using System.Net.Http.Json;
using MathInsight.WebAPI.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace MathInsight.WebAPI.IntegrationTests;

/// <summary>Verifies the test host boots and routes before the real suites run.</summary>
public class SmokeTest : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public SmokeTest(AuthApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Host_Boots_AndLoginRouteResponds()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { usernameOrEmail = "nobody@test", password = "whatever" });

        _output.WriteLine($"status={response.StatusCode}");
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
