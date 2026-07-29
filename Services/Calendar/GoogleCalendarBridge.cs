using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BigLocalHub.Services.Calendar;

/// <summary>
/// Credentials for the Google OAuth client.
///
/// These CANNOT be filled in from here — an OAuth client ID has to be created
/// in the Google Cloud console for the project that owns the Calendar API, and
/// tied to this app's bundle id. See README ("Google Calendar setup") for the
/// exact steps. Until <see cref="ClientId"/> is set the bridge reports
/// NotConfigured and the UI says so rather than failing at the auth screen.
///
/// The client id and its reversed-scheme redirect are NOT secrets — for
/// installed apps Google issues no client secret, and security comes from PKCE
/// plus the registered redirect URI, which is why this can live in source.
/// </summary>
public static class GoogleCalendarConfig
{
    /// <summary>e.g. "1234567890-abcdefg.apps.googleusercontent.com"</summary>
    public const string ClientId = "";

    /// <summary>
    /// Google's iOS convention: the client id with its dot-segments reversed.
    /// Must also be registered as a CFBundleURLScheme in Info.plist.
    /// </summary>
    public static string RedirectScheme =>
        string.IsNullOrEmpty(ClientId)
            ? string.Empty
            : string.Join('.', ClientId.Split('.').Reverse());

    public static string RedirectUri => $"{RedirectScheme}:/oauth2redirect";

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}

/// <summary>
/// Pushes hub events into the signed-in user's primary Google Calendar.
///
/// Uses the OAuth 2.0 authorization-code flow with PKCE — the flow Google
/// mandates for installed apps, and the reason there is no client secret here.
/// The refresh token is kept in <see cref="SecureStorage"/> (Keychain on iOS),
/// never in Preferences, because it is a long-lived credential.
/// </summary>
public class GoogleCalendarBridge : ICalendarBridge
{
    private const string AuthEndpoint  = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string ApiBase       = "https://www.googleapis.com/calendar/v3";
    private const string Scope         = "https://www.googleapis.com/auth/calendar.events";

    private const string RefreshTokenKey = "google_calendar_refresh_token";

    // One shared client for the lifetime of the app. HttpClient is designed to
    // be reused — a new instance per call leaks sockets under TIME_WAIT — and
    // this bridge is registered as a singleton, so a static isn't hiding state
    // that anything else could observe.
    private static readonly HttpClient Http = new();

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiry = DateTimeOffset.MinValue;

    public string Name => "Google Calendar";
    public string Description => "Syncs to your Google account's primary calendar.";

    public async Task<BridgeState> GetStateAsync()
    {
        if (!GoogleCalendarConfig.IsConfigured) return BridgeState.NotConfigured;
        var refresh = await SecureStorage.GetAsync(RefreshTokenKey);
        return string.IsNullOrEmpty(refresh) ? BridgeState.NeedsPermission : BridgeState.Ready;
    }

    public async Task<BridgeState> ConnectAsync()
    {
        if (!GoogleCalendarConfig.IsConfigured) return BridgeState.NotConfigured;

        try
        {
            // ── PKCE ────────────────────────────────────────────────────────
            var verifier = CreateCodeVerifier();
            var challenge = CreateCodeChallenge(verifier);

            var authUrl = $"{AuthEndpoint}" +
                $"?client_id={Uri.EscapeDataString(GoogleCalendarConfig.ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(GoogleCalendarConfig.RedirectUri)}" +
                $"&response_type=code" +
                $"&scope={Uri.EscapeDataString(Scope)}" +
                $"&code_challenge={challenge}" +
                $"&code_challenge_method=S256" +
                // Google only returns a refresh token when both are present.
                $"&access_type=offline&prompt=consent";

            var result = await WebAuthenticator.Default.AuthenticateAsync(
                new Uri(authUrl),
                new Uri(GoogleCalendarConfig.RedirectUri));

            if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
                return BridgeState.NeedsPermission;

            var token = await ExchangeCodeAsync(code, verifier);
            if (token?.RefreshToken is null) return BridgeState.NeedsPermission;

            await SecureStorage.SetAsync(RefreshTokenKey, token.RefreshToken);
            CacheAccessToken(token);
            return BridgeState.Ready;
        }
        catch (TaskCanceledException)
        {
            // User dismissed the browser sheet — not an error worth surfacing.
            return BridgeState.NeedsPermission;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[google-cal] connect failed: {ex}");
            return BridgeState.NeedsPermission;
        }
    }

    public async Task<SyncResult> ExportAsync(IReadOnlyList<CalendarEventDto> events)
    {
        if (!GoogleCalendarConfig.IsConfigured)
            return SyncResult.Error("Google Calendar isn't set up in this build yet.");

        string accessToken;
        try
        {
            accessToken = await GetAccessTokenAsync();
        }
        catch (Exception ex)
        {
            return SyncResult.Error($"Couldn't authenticate with Google: {ex.Message}");
        }

        // Set per request rather than on the shared client's default headers,
        // so a refreshed token can't race against an in-flight batch.

        int created = 0, updated = 0, failed = 0;

        foreach (var e in events)
        {
            try
            {
                // Google allows an app-defined id, so idempotency is exact
                // rather than heuristic: same SourceId means same event, and a
                // re-sync is a PUT rather than a duplicate POST.
                var id = ToGoogleEventId(e.SourceId);
                var body = new
                {
                    id,
                    summary = e.Title,
                    location = e.Location ?? string.Empty,
                    description = e.Notes ?? string.Empty,
                    start = ToGoogleTime(e.Start, e.IsAllDay),
                    end = ToGoogleTime(e.IsAllDay ? e.End.Date.AddDays(1) : e.End, e.IsAllDay),
                };

                var put = await SendJsonAsync(HttpMethod.Put,
                    $"{ApiBase}/calendars/primary/events/{id}", body, accessToken);
                if (put.IsSuccessStatusCode) { updated++; continue; }

                if (put.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var post = await SendJsonAsync(HttpMethod.Post,
                        $"{ApiBase}/calendars/primary/events", body, accessToken);
                    if (post.IsSuccessStatusCode) { created++; continue; }
                }

                failed++;
                System.Diagnostics.Debug.WriteLine($"[google-cal] {id}: {put.StatusCode}");
            }
            catch (Exception ex)
            {
                failed++;
                System.Diagnostics.Debug.WriteLine($"[google-cal] {e.SourceId}: {ex.Message}");
            }
        }

        return new SyncResult(created, updated, failed);
    }

    public Task DisconnectAsync()
    {
        SecureStorage.Remove(RefreshTokenKey);
        _accessToken = null;
        _accessTokenExpiry = DateTimeOffset.MinValue;
        return Task.CompletedTask;
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method, string url, object body, string accessToken)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await Http.SendAsync(request);
    }

    // ── Token handling ──────────────────────────────────────────────────────

    private async Task<string> GetAccessTokenAsync()
    {
        // 30s of slack so a token doesn't expire mid-batch.
        if (_accessToken is not null && _accessTokenExpiry > DateTimeOffset.UtcNow.AddSeconds(30))
            return _accessToken;

        var refresh = await SecureStorage.GetAsync(RefreshTokenKey)
            ?? throw new InvalidOperationException("Not connected to Google.");

        var response = await Http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(
        [
            new("client_id", GoogleCalendarConfig.ClientId),
            new("refresh_token", refresh),
            new("grant_type", "refresh_token"),
        ]));

        if (!response.IsSuccessStatusCode)
        {
            // A revoked or expired refresh token can't be recovered from —
            // drop it so the UI falls back to "Connect" instead of retrying
            // a credential that will never work again.
            await DisconnectAsync();
            throw new InvalidOperationException("Google sign-in expired. Connect again.");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>()
            ?? throw new InvalidOperationException("Malformed token response.");

        CacheAccessToken(token);
        return _accessToken!;
    }

    private async Task<TokenResponse?> ExchangeCodeAsync(string code, string verifier)
    {
        var response = await Http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(
        [
            new("client_id", GoogleCalendarConfig.ClientId),
            new("code", code),
            new("code_verifier", verifier),
            new("grant_type", "authorization_code"),
            new("redirect_uri", GoogleCalendarConfig.RedirectUri),
        ]));

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TokenResponse>()
            : null;
    }

    private void CacheAccessToken(TokenResponse token)
    {
        _accessToken = token.AccessToken;
        _accessTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url(bytes);
    }

    private static string CreateCodeChallenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Google event ids allow only lowercase a–v and 0–9, so the SourceId is
    /// hex-encoded rather than passed through — "job-abc123-start" would be
    /// rejected for both the dashes and the out-of-range letters.
    /// </summary>
    private static string ToGoogleEventId(string sourceId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sourceId));
        var sb = new StringBuilder("bl");
        // Cast to char explicitly: (b % 22 + 'a') is an int, and appending it
        // without the cast would write the number rather than the letter.
        foreach (var b in hash.Take(16)) sb.Append((char)(b % 22 + 'a'));
        return sb.ToString();
    }

    private static object ToGoogleTime(DateTime dt, bool allDay) =>
        allDay
            ? new { date = dt.ToString("yyyy-MM-dd") }
            : new { dateTime = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Local)).ToString("o") };

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]  public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")]    public int ExpiresIn { get; set; }
    }
}
