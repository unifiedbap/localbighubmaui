using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Plugin.Firebase.Auth;

namespace BigLocalHub.Services;

/// <summary>
/// Calls the scanCompanySeoHealth Cloud Function (unifiedbap/biglocalhub) to
/// trigger an on-demand SEO Health Score scan, rather than waiting for its
/// weekly schedule. Shared by SeoHealthViewModel's "Scan now" button and the
/// Dashboard SEO widget so there's exactly one place that knows the
/// function's URL, auth header, and response shape.
///
/// A plain HTTPS POST with a Firebase ID token, not a Plugin.Firebase Cloud
/// Functions callable — this app already has an HttpClient pattern
/// (GoogleCalendarBridge) and an ID token from Plugin.Firebase.Auth, so
/// this avoids a whole extra native plugin dependency for one endpoint.
///
/// Server-side rate-limited (one scan per company per 15 minutes) — a
/// failure surfaces as an exception with the server's plain-language message
/// (e.g. "You can scan again in 12 minutes."), not a generic HTTP error.
/// </summary>
public class SeoHealthScanService
{
    private const string FunctionUrl = "https://us-central1-big-local-ideas.cloudfunctions.net/scanCompanySeoHealth";

    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IFirebaseAuth _auth;

    public SeoHealthScanService(IFirebaseAuth auth) => _auth = auth;

    /// <summary>Triggers an immediate scan for one company. Throws with a user-presentable message on any failure.</summary>
    public async Task<SeoHealthScanResult> ScanNowAsync(string companyId)
    {
        var user = _auth.CurrentUser ?? throw new InvalidOperationException("You're signed out — sign in and try again.");
        var tokenResult = await user.GetIdTokenResultAsync(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, FunctionUrl)
        {
            Content = JsonContent.Create(new { companyId }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Token);

        using var response = await Http.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ScanResponse>(JsonOptions);

        if (!response.IsSuccessStatusCode || body is null || !body.Ok)
        {
            throw new InvalidOperationException(body?.Error ?? $"The scan failed ({(int)response.StatusCode}). Try again.");
        }

        return new SeoHealthScanResult(body.Score, body.ScoreLabel);
    }

    private record ScanResponse(bool Ok, int Score, string ScoreLabel, string? Error);
}

public record SeoHealthScanResult(int Score, string ScoreLabel);
