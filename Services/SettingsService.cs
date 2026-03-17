using System.IO;
using System.Text.Json;
using Scriptly.Models;

namespace Scriptly.Services;

public class SettingsService
{
    private static readonly string DefaultSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Scriptly", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private const string OpenRouterApiKeyName = "openrouter.apiKey";
    private const string GroqApiKeyName = "groq.apiKey";

    private readonly ISecretStore _secretStore;
    private readonly string _settingsPath;

    public SettingsService()
        : this(DefaultSettingsPath, new DpapiSecretStore())
    {
    }

    public SettingsService(ISecretStore secretStore)
        : this(DefaultSettingsPath, secretStore)
    {
    }

    public SettingsService(string settingsPath, ISecretStore secretStore)
    {
        _settingsPath = settingsPath;
        _secretStore = secretStore;
    }

    public AppSettings Load()
    {
        try
        {
            AppSettings settings;
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                           ?? new AppSettings();
            }
            else
            {
                settings = new AppSettings();
            }

            bool migratedFromPlaintext = HydrateSecrets(settings);

            if (settings.SettingsVersion < 2)
            {
                settings.SettingsVersion = 2;
                Save(settings);
            }
            else if (migratedFromPlaintext)
            {
                Save(settings);
            }

            return settings;
        }
        catch (Exception ex)
        {
            DebugLogService.LogError("SettingsService.Load", ex);
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            PersistSecrets(settings);
            var persisted = CloneWithoutSecrets(settings);

            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(persisted, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            DebugLogService.LogError("SettingsService.Save", ex);
        }
    }

    public void ClearStoredApiKeys()
    {
        _secretStore.Remove(OpenRouterApiKeyName);
        _secretStore.Remove(GroqApiKeyName);
    }

    private bool HydrateSecrets(AppSettings settings)
    {
        bool migrated = false;

        var openRouterFromStore = _secretStore.Get(OpenRouterApiKeyName);
        if (!string.IsNullOrWhiteSpace(openRouterFromStore))
        {
            settings.OpenRouter.ApiKey = openRouterFromStore;
        }
        else if (!string.IsNullOrWhiteSpace(settings.OpenRouter.ApiKey))
        {
            _secretStore.Set(OpenRouterApiKeyName, settings.OpenRouter.ApiKey);
            migrated = true;
        }

        var groqFromStore = _secretStore.Get(GroqApiKeyName);
        if (!string.IsNullOrWhiteSpace(groqFromStore))
        {
            settings.Groq.ApiKey = groqFromStore;
        }
        else if (!string.IsNullOrWhiteSpace(settings.Groq.ApiKey))
        {
            _secretStore.Set(GroqApiKeyName, settings.Groq.ApiKey);
            migrated = true;
        }

        return migrated;
    }

    private void PersistSecrets(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.OpenRouter.ApiKey))
            _secretStore.Set(OpenRouterApiKeyName, settings.OpenRouter.ApiKey);

        if (!string.IsNullOrWhiteSpace(settings.Groq.ApiKey))
            _secretStore.Set(GroqApiKeyName, settings.Groq.ApiKey);
    }

    private static AppSettings CloneWithoutSecrets(AppSettings settings)
    {
        return new AppSettings
        {
            SettingsVersion = 2,
            HotkeyModifiers = settings.HotkeyModifiers,
            HotkeyKey = settings.HotkeyKey,
            ActiveProvider = settings.ActiveProvider,
            OpenRouter = new OpenRouterSettings
            {
                ApiKey = string.Empty,
                Model = settings.OpenRouter.Model
            },
            Groq = new GroqSettings
            {
                ApiKey = string.Empty,
                Model = settings.Groq.Model
            },
            CustomActions = settings.CustomActions.Select(a => new CustomAction
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                Instructions = a.Instructions,
                Icon = a.Icon
            }).ToList(),
            StartWithWindows = settings.StartWithWindows,
            Theme = settings.Theme
        };
    }
}
