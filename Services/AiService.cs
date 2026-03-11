using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Scriptly.Models;

namespace Scriptly.Services;

public class AiService
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public AiService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<string> ProcessAsync(string prompt, string selectedText, CancellationToken ct = default)
    {
        var settings = _settingsService.Load();
        var fullPrompt = prompt.Replace("{text}", selectedText);

        return settings.ActiveProvider switch
        {
            "Groq" => await CallGroqAsync(settings.Groq, fullPrompt, ct),
            _ => await CallOpenRouterAsync(settings.OpenRouter, fullPrompt, ct)
        };
    }

    private async Task<string> CallOpenRouterAsync(OpenRouterSettings config, string prompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException("OpenRouter API key is not configured. Please open Settings and add your API key.");

        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException("OpenRouter model is not configured. Please open Settings and specify a model name.");

        var requestBody = new
        {
            model = config.Model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 4096
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
        request.Headers.Add("HTTP-Referer", "https://scriptly.app");
        request.Headers.Add("X-Title", "Scriptly");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var errDoc = JsonDocument.Parse(json);
            var errMsg = errDoc.RootElement.TryGetProperty("error", out var errEl)
                ? errEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : json
                : json;
            throw new HttpRequestException($"OpenRouter API error ({(int)response.StatusCode}): {errMsg}");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
                  .GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content")
                  .GetString() ?? string.Empty;
    }

    private async Task<string> CallGroqAsync(GroqSettings config, string prompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException("Groq API key is not configured. Please open Settings and add your API key.");

        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException("Groq model is not configured. Please open Settings and specify a model name.");

        var requestBody = new
        {
            model = config.Model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 4096
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var errDoc = JsonDocument.Parse(json);
            var errMsg = errDoc.RootElement.TryGetProperty("error", out var errEl)
                ? errEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : json
                : json;
            throw new HttpRequestException($"Groq API error ({(int)response.StatusCode}): {errMsg}");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
                  .GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content")
                  .GetString() ?? string.Empty;
    }

    // ── Streaming API ────────────────────────────────────────────────────────
    // Both OpenRouter and Groq implement the OpenAI SSE streaming protocol.
    // Each SSE line is:  data: {"choices":[{"delta":{"content":"token"}}]}
    // The stream ends with:  data: [DONE]

    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        string selectedText,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var settings   = _settingsService.Load();
        var fullPrompt = prompt.Replace("{text}", selectedText);

        var tokens = settings.ActiveProvider switch
        {
            "Groq" => StreamGroqAsync(settings.Groq, fullPrompt, ct),
            _      => StreamOpenRouterAsync(settings.OpenRouter, fullPrompt, ct)
        };

        await foreach (var token in tokens.WithCancellation(ct))
            yield return token;
    }

    private async IAsyncEnumerable<string> StreamOpenRouterAsync(
        OpenRouterSettings config,
        string prompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException("OpenRouter API key is not configured. Please open Settings and add your API key.");
        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException("OpenRouter model is not configured. Please open Settings and specify a model name.");

        var requestBody = new
        {
            model    = config.Model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 4096,
            stream   = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
        request.Headers.Add("HTTP-Referer", "https://scriptly.app");
        request.Headers.Add("X-Title", "Scriptly");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            string? errMsg = null;
            try
            {
                var errDoc = JsonDocument.Parse(errBody);
                errMsg = errDoc.RootElement.TryGetProperty("error", out var errEl)
                    ? errEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : errBody
                    : errBody;
            }
            catch { errMsg = errBody; }
            throw new HttpRequestException($"OpenRouter API error ({(int)response.StatusCode}): {errMsg}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            ct.ThrowIfCancellationRequested();
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") yield break;

            string? token = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                token = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("delta")
                    .TryGetProperty("content", out var contentEl)
                        ? contentEl.GetString()
                        : null;
            }
            catch { /* skip malformed SSE chunk */ }

            if (!string.IsNullOrEmpty(token))
                yield return token;
        }
    }

    private async IAsyncEnumerable<string> StreamGroqAsync(
        GroqSettings config,
        string prompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException("Groq API key is not configured. Please open Settings and add your API key.");
        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException("Groq model is not configured. Please open Settings and specify a model name.");

        var requestBody = new
        {
            model    = config.Model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 4096,
            stream   = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            string? errMsg = null;
            try
            {
                var errDoc = JsonDocument.Parse(errBody);
                errMsg = errDoc.RootElement.TryGetProperty("error", out var errEl)
                    ? errEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : errBody
                    : errBody;
            }
            catch { errMsg = errBody; }
            throw new HttpRequestException($"Groq API error ({(int)response.StatusCode}): {errMsg}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            ct.ThrowIfCancellationRequested();
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") yield break;

            string? token = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                token = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("delta")
                    .TryGetProperty("content", out var contentEl)
                        ? contentEl.GetString()
                        : null;
            }
            catch { /* skip malformed SSE chunk */ }

            if (!string.IsNullOrEmpty(token))
                yield return token;
        }
    }
}
