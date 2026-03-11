using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Scriptly.Models;
using Scriptly.Services;

namespace Scriptly.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private AppSettings _settings;
    private Action<AppSettings>? _onSaved;

    public SettingsWindow(SettingsService settingsService, Action<AppSettings>? onSaved = null)
    {
        _settingsService = settingsService;
        _onSaved = onSaved;
        _settings = settingsService.Load();

        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        // Provider
        ProviderCombo.SelectedIndex = _settings.ActiveProvider == "Groq" ? 1 : 0;

        // OpenRouter
        OpenRouterKeyBox.Text = _settings.OpenRouter.ApiKey;
        OpenRouterModelBox.Text = _settings.OpenRouter.Model;

        // Groq
        GroqKeyBox.Text = _settings.Groq.ApiKey;
        GroqModelBox.Text = _settings.Groq.Model;

        // Hotkey
        HotkeyModifiers.Text = _settings.HotkeyModifiers;
        HotkeyKey.Text = _settings.HotkeyKey;

        // General
        StartWithWindowsCheck.IsChecked = StartupService.IsEnabled();

        // Version
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = $"Scriptly v{v?.Major}.{v?.Minor}.{v?.Build ?? 0}";

        // Custom actions
        RefreshCustomActions();
    }

    private void RefreshCustomActions()
    {
        CustomActionsList.ItemsSource = null;
        CustomActionsList.ItemsSource = _settings.CustomActions;
    }

    private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProviderCombo.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString() ?? "OpenRouter";
            OpenRouterPanel.Visibility = tag == "OpenRouter" ? Visibility.Visible : Visibility.Collapsed;
            GroqPanel.Visibility = tag == "Groq" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.OpenRouter.ApiKey = OpenRouterKeyBox.Text.Trim();
        _settings.OpenRouter.Model = OpenRouterModelBox.Text.Trim();
        _settings.Groq.ApiKey = GroqKeyBox.Text.Trim();
        _settings.Groq.Model = GroqModelBox.Text.Trim();
        _settings.HotkeyModifiers = HotkeyModifiers.Text.Trim();
        _settings.HotkeyKey = HotkeyKey.Text.Trim();

        if (ProviderCombo.SelectedItem is ComboBoxItem item)
            _settings.ActiveProvider = item.Tag?.ToString() ?? "OpenRouter";

        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        StartupService.Apply(_settings.StartWithWindows);

        _settingsService.Save(_settings);
        _onSaved?.Invoke(_settings);

        StatusLabel.Text = "✓ Settings saved";
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { StatusLabel.Text = ""; timer.Stop(); };
        timer.Start();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void NewAction_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CustomActionDialog();
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            _settings.CustomActions.Add(dialog.Result);
            RefreshCustomActions();
        }
    }

    private void EditAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CustomAction action)
        {
            var dialog = new CustomActionDialog(action);
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                var idx = _settings.CustomActions.FindIndex(a => a.Id == action.Id);
                if (idx >= 0) _settings.CustomActions[idx] = dialog.Result;
                RefreshCustomActions();
            }
        }
    }

    private void DeleteAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CustomAction action)
        {
            _settings.CustomActions.RemoveAll(a => a.Id == action.Id);
            RefreshCustomActions();
        }
    }

    private void ReleasesButton_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "https://github.com/scriptly-app/scriptly/releases")
            { UseShellExecute = true });
    }
}
