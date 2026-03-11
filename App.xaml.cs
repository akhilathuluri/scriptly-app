using System.Windows;
using Scriptly.Services;
using Scriptly.Windows;

namespace Scriptly;

public partial class App : Application
{
    private TrayService? _trayService;
    private GlobalHotkeyService? _hotkeyService;
    private TextCaptureService? _textCapture;
    private SettingsService? _settingsService;
    private ActionsService? _actionsService;
    private AiService? _aiService;
    private HistoryService? _historyService;
    private System.Threading.Mutex? _mutex; // kept alive for the process lifetime

    // Hidden message-only window to receive hotkey messages
    private HotkeyWindow? _hotkeyWindow;

    // Pre-created windows (hidden until needed)
    private ActionPanelWindow? _actionPanel;
    private ResultWindow? _resultWindow;
    private HistoryWindow? _historyWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Enforce single instance — stored as a field so GC never collects it while the app runs
        _mutex = new System.Threading.Mutex(true, "Scriptly_SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show("Scriptly is already running. Look for the icon in the system tray.",
                "Scriptly", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Use WinForms message loop for NotifyIcon
        System.Windows.Forms.Application.EnableVisualStyles();

        // Services
        _settingsService = new SettingsService();
        _actionsService = new ActionsService(_settingsService);
        _aiService = new AiService(_settingsService);
        _textCapture = new TextCaptureService();
        _historyService = new HistoryService();

        // Tray
        _trayService = new TrayService();
        _trayService.OpenSettingsRequested += OpenSettings;
        _trayService.OpenHistoryRequested  += OpenHistory;
        _trayService.ExitRequested += () => Shutdown();
        _trayService.Initialize();

        // Helper window for hotkey registration (not shown, not in taskbar)
        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.Show();
        _hotkeyWindow.Hide();

        // Hotkey
        _hotkeyService = new GlobalHotkeyService();
        _hotkeyService.Initialize(_hotkeyWindow);
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        var settings = _settingsService.Load();
        bool registered = _hotkeyService.Register(settings.HotkeyModifiers, settings.HotkeyKey);

        if (!registered)
            _trayService.ShowBalloon("Scriptly — Hotkey conflict",
                $"Could not register {settings.HotkeyModifiers}+{settings.HotkeyKey}. " +
                "Another app may be using it. Open Settings to choose a different shortcut.",
                System.Windows.Forms.ToolTipIcon.Warning);

        // Apply startup-on-login preference
        StartupService.Apply(settings.StartWithWindows);

        // Pre-create windows (hidden) — reused on every hotkey press
        _resultWindow = new ResultWindow(_aiService, _textCapture, _settingsService, _historyService!);
        _actionPanel  = new ActionPanelWindow(_actionsService, _aiService, _textCapture, _settingsService, _resultWindow);
        _historyWindow = new HistoryWindow(_historyService!);

        // Show balloon on first start or if no API key set
        if (string.IsNullOrWhiteSpace(settings.OpenRouter.ApiKey) && string.IsNullOrWhiteSpace(settings.Groq.ApiKey))
        {
            _trayService.ShowBalloon("Scriptly is running",
                "Open Settings to add your AI API key. Then select text and press Ctrl+Shift+Space.");
        }
        else
        {
            _trayService.ShowBalloon("Scriptly is running",
                $"Select text anywhere and press {settings.HotkeyModifiers}+{settings.HotkeyKey}");
        }
    }

    private async void OnHotkeyPressed()
    {
        if (_textCapture == null || _actionPanel == null) return;

        var cursorPos = _textCapture.GetCursorPosition();
        var selectedText = await _textCapture.GetSelectedTextAsync();

        if (string.IsNullOrWhiteSpace(selectedText)) return;

        Dispatcher.Invoke(() =>
        {
            if (_actionPanel.IsVisible)
            {
                _actionPanel.Hide();
                return;
            }
            _actionPanel.ShowWithText(selectedText, cursorPos);
        });
    }

    private void OpenSettings()
    {
        Dispatcher.Invoke(() =>
        {
            var win = new SettingsWindow(_settingsService!, settings =>
            {
                // Invalidate cached actions so new custom actions are picked up immediately
                _actionsService?.InvalidateCache();

                // Re-register hotkey on save; warn if the new combo is already taken
                bool ok = _hotkeyService?.Register(settings.HotkeyModifiers, settings.HotkeyKey) ?? false;
                if (!ok)
                    _trayService?.ShowBalloon("Scriptly — Hotkey conflict",
                        $"Could not register {settings.HotkeyModifiers}+{settings.HotkeyKey}. " +
                        "Another app may be using it. Try a different shortcut.",
                        System.Windows.Forms.ToolTipIcon.Warning);
            });
            win.Show();
            win.Activate();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayService?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void OpenHistory()
    {
        Dispatcher.Invoke(() =>
        {
            if (_historyWindow!.IsVisible)
            {
                _historyWindow.Activate();
                return;
            }
            _historyWindow.ShowPanel();
        });
    }
}


