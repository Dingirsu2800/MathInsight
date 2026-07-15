using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// <see cref="IGoogleOAuthService"/> over a typed <see cref="HttpClient"/>. Uses the userinfo
/// endpoint (server-to-server over TLS) to read the profile, so no id_token signature validation
/// library is required.
/// </summary>
public class GoogleOAuthService : IGoogleOAuthService
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";

    private readonly HttpClient _httpClient;
    private readonly GoogleOAuthOptions _options;
    private readonly ILogger<GoogleOAuthService> _logger;

    public GoogleOAuthService(HttpClient httpClient, GoogleOAuthOptions options, ILogger<GoogleOAuthService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public string BuildAuthorizationUrl(string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "online",
            ["prompt"] = "select_account",
        };

        var queryString = string.Join("&", query
            .Where(pair => !string.IsNullOrEmpty(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));

        return $"{AuthorizationEndpoint}?{queryString}";
    }

    public async Task<GoogleUserProfile?> ExchangeCodeForProfileAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Exchange the authorization code for tokens.
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["redirect_uri"] = _options.RedirectUri,
                    ["grant_type"] = "authorization_code",
                }),
            };

            using var tokenResponse = await _httpClient.SendAsync(tokenRequest, cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token exchange failed with status {Status}.", tokenResponse.StatusCode);
                return null;
            }

            var tokens = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
            if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
            {
                return null;
            }

            // 2. Fetch the profile with the access token.
            using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

            using var userInfoResponse = await _httpClient.SendAsync(userInfoRequest, cancellationToken);
            if (!userInfoResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google userinfo request failed with status {Status}.", userInfoResponse.StatusCode);
                return null;
            }

            var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<GoogleUserInfo>(cancellationToken);
            if (userInfo is null ||
                string.IsNullOrWhiteSpace(userInfo.Sub) ||
                string.IsNullOrWhiteSpace(userInfo.Email))
            {
                return null;
            }

            return new GoogleUserProfile(
                userInfo.Sub,
                userInfo.Email,
                ReadBool(userInfo.EmailVerified),
                userInfo.GivenName,
                userInfo.FamilyName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google OAuth code exchange failed.");
            return null;
        }
    }

    // email_verified is normally a JSON boolean but can arrive as the string "true"; tolerate both.
    private static bool ReadBool(JsonElement element) =>
        element.ValueKind == JsonValueKind.True ||
        (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var parsed) && parsed);

    private sealed record GoogleTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
        [JsonPropertyName("id_token")] public string? IdToken { get; init; }
    }

    private sealed record GoogleUserInfo
    {
        [JsonPropertyName("sub")] public string? Sub { get; init; }
        [JsonPropertyName("email")] public string? Email { get; init; }
        [JsonPropertyName("email_verified")] public JsonElement EmailVerified { get; init; }
        [JsonPropertyName("given_name")] public string? GivenName { get; init; }
        [JsonPropertyName("family_name")] public string? FamilyName { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
    }
}
