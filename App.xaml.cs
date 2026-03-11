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

    // Hidden message-only window to receive hotkey messages
    private HotkeyWindow? _hotkeyWindow;

    // Pre-created windows (hidden until needed)
    private ActionPanelWindow? _actionPanel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Enforce single instance
        var mutex = new System.Threading.Mutex(true, "Scriptly_SingleInstance", out bool created);
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

        // Tray
        _trayService = new TrayService();
        _trayService.OpenSettingsRequested += OpenSettings;
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

        // Pre-create action panel
        _actionPanel = new ActionPanelWindow(_actionsService, _aiService, _textCapture, _settingsService);

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
                // Re-register hotkey on save
                _hotkeyService?.Register(settings.HotkeyModifiers, settings.HotkeyKey);
            });
            win.Show();
            win.Activate();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayService?.Dispose();
        base.OnExit(e);
    }
}


