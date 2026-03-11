using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Scriptly.Services;

/// <summary>
/// Developer-only analytics via PostHog.
/// Tracks action usage, streaming chunk counts, durations, and error types.
/// No user text, no selected content, no PII is ever sent.
///
/// To enable: fill in PostHogApiKey below with your PostHog project API key.
/// Analytics are silently disabled (no-op) when the key is left empty.
/// </summary>
public sealed class AnalyticsService : IDisposable
{
    // ── Developer configuration ──────────────────────────────────────────────
    // Replace the empty string with your PostHog project API key.
    // This is a DEVELOPER key — never shown or accessible to end users.
    private const string PostHogApiKey = "phc_72TNmzOIpyvoNCkf90Ks84PXcEJbLaJqwgxNUR56JAj";
    private const string PostHogHost   = "https://eu.i.posthog.com";

    // ── State ────────────────────────────────────────────────────────────────
    private readonly HttpClient _http;
    private readonly string     _distinctId;   // anonymous per-installation GUID — no PII
    private readonly string     _appVersion;
    private readonly bool       _enabled;

    public AnalyticsService()
    {
        _enabled    = !string.IsNullOrWhiteSpace(PostHogApiKey);
        _http       = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _distinctId = GetOrCreateDeviceId();
        _appVersion = System.Reflection.Assembly
                          .GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    }

    // ── Public tracking methods ──────────────────────────────────────────────

    /// <summary>Called once when the application starts successfully.</summary>
    public void TrackAppStarted() =>
        Capture("app_started", new { app_version = _appVersion });

    /// <summary>Called when an AI action begins — before the HTTP request is sent.</summary>
    public void TrackActionStarted(
        string actionId, string actionName, bool isBuiltIn,
        string provider, string model) =>
        Capture("action_started", new
        {
            action_id   = actionId,
            action_name = actionName,
            is_builtin  = isBuiltIn,
            provider,
            model,
            app_version = _appVersion
        });

    /// <summary>
    /// Called when AI streaming completes successfully.
    /// streaming_chunks ≈ number of SSE delta events (rough proxy for output token count).
    /// result_chars is the total character length of the final output.
    /// </summary>
    public void TrackActionCompleted(
        string actionId, bool isBuiltIn,
        int streamingChunks, int resultChars, long durationMs,
        string provider) =>
        Capture("action_completed", new
        {
            action_id        = actionId,
            is_builtin       = isBuiltIn,
            streaming_chunks = streamingChunks,
            result_chars     = resultChars,
            duration_ms      = durationMs,
            provider,
            app_version      = _appVersion
        });

    /// <summary>
    /// Called when an action fails with an unhandled exception.
    /// Only the exception type name is sent — never the message (which may contain user data).
    /// </summary>
    public void TrackActionError(
        string actionId, bool isBuiltIn,
        string errorType, string provider) =>
        Capture("action_error", new
        {
            action_id  = actionId,
            is_builtin = isBuiltIn,
            error_type = errorType,   // e.g. "HttpRequestException" — no user text
            provider,
            app_version = _appVersion
        });

    // ── Internal ─────────────────────────────────────────────────────────────

    private void Capture<T>(string eventName, T properties)
    {
        if (!_enabled) return;

        // Fire-and-forget on a thread-pool thread.
        // Analytics must never throw, slow down, or block the UI.
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = new
                {
                    api_key     = PostHogApiKey,
                    @event      = eventName,
                    distinct_id = _distinctId,
                    properties,
                    timestamp   = DateTime.UtcNow.ToString("O")
                };

                var body    = JsonSerializer.Serialize(payload);
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                await _http.PostAsync($"{PostHogHost}/capture/", content).ConfigureAwait(false);
            }
            catch { /* analytics must never surface */ }
        });
    }

    /// <summary>
    /// Returns a stable anonymous device identifier stored in AppData.
    /// A random GUID is generated once per installation — no user-identifying data.
    /// </summary>
    private static string GetOrCreateDeviceId()
    {
        try
        {
            var dir  = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Scriptly");
            var path = Path.Combine(dir, "device_id");
            Directory.CreateDirectory(dir);

            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(existing)) return existing;
            }

            var id = Guid.NewGuid().ToString();
            File.WriteAllText(path, id);
            return id;
        }
        catch
        {
            return Guid.NewGuid().ToString(); // ephemeral fallback — no disk access
        }
    }

    public void Dispose() => _http.Dispose();
}
