using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Scriptly.Models;
using Scriptly.Services;

namespace Scriptly.Windows;

public partial class ResultWindow : Window
{
    private readonly AiService _aiService;
    private readonly TextCaptureService _textCapture;
    private readonly SettingsService _settingsService;
    private CancellationTokenSource? _cts;
    private string _resultText = string.Empty;

    public ResultWindow(AiService aiService, TextCaptureService textCapture, SettingsService settingsService)
    {
        _aiService = aiService;
        _textCapture = textCapture;
        _settingsService = settingsService;

        InitializeComponent();
        // Result window does NOT auto-close on deactivation — user must
        // explicitly Copy, Replace, press Escape, or click X.
        Loaded += OnLoaded;
    }

    private void OnLoaded(object s, RoutedEventArgs e)
    {
        StartThinkingAnimation();
        AnimateOpen(); // Without this RootBorder stays at Opacity=0
    }

    public void ShowWithProcessing(ActionItem action, string selectedText)
    {
        ActionIcon.Text = action.Icon;
        ActionTitle.Text = action.Name;

        // Position: centre of screen, slightly above middle
        var screen = SystemParameters.WorkArea;
        Left = (screen.Width - Width) / 2;
        Top = (screen.Height - 400) / 2;

        Show();
        Activate();

        _ = ProcessAsync(action, selectedText);
    }

    private async Task ProcessAsync(ActionItem action, string selectedText)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            var result = await _aiService.ProcessAsync(action.Prompt!, selectedText, _cts.Token);
            _resultText = result.Trim();

            Dispatcher.Invoke(() =>
            {
                ThinkingPanel.Visibility = Visibility.Collapsed;
                ResultPanel.Visibility = Visibility.Visible;
                ResultText.Text = _resultText;
                ButtonPanel.Visibility = Visibility.Visible;
                AnimateResultIn();
            });
        }
        catch (OperationCanceledException)
        {
            // Window was closed before result
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                ThinkingPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
                ErrorText.Text = ex.Message;
                ButtonPanel.Visibility = Visibility.Visible;
            });
        }
    }

    private void AnimateResultIn()
    {
        var dur = new Duration(TimeSpan.FromMilliseconds(180));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var opAnim = new DoubleAnimation(0, 1, dur) { EasingFunction = ease };
        ResultPanel.BeginAnimation(OpacityProperty, opAnim);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_resultText))
            Clipboard.SetText(_resultText);
        AnimateClose(showCopied: true);
    }

    private void ReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        var text = _resultText;
        AnimateClose(onComplete: async () =>
        {
            await Task.Delay(150); // allow window to fully close & source app to regain focus
            await _textCapture.ReplaceSelectedTextAsync(text);
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => AnimateClose();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { AnimateClose(); e.Handled = true; }
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && !string.IsNullOrEmpty(_resultText))
        { Clipboard.SetText(_resultText); AnimateClose(); e.Handled = true; }
        base.OnKeyDown(e);
    }

    // ── Thinking bounce animation ────────────────────────────
    private void StartThinkingAnimation()
    {
        AnimateDot(DotT1, 0);
        AnimateDot(DotT2, 120);
        AnimateDot(DotT3, 240);
    }

    private static void AnimateDot(System.Windows.Media.TranslateTransform t, double delayMs)
    {
        var anim = new DoubleAnimation
        {
            From = 0, To = -5,
            Duration = new Duration(TimeSpan.FromMilliseconds(400)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        t.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, anim);
    }

    // ── Window animations ────────────────────────────────────
    private void AnimateOpen()
    {
        var dur = new Duration(TimeSpan.FromMilliseconds(220));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        RootBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur) { EasingFunction = ease });

        var scaleAnim = new DoubleAnimation(0.94, 1.0, dur) { EasingFunction = ease };
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);

        TranslateT.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(-10, 0, dur) { EasingFunction = ease });
    }

    private void AnimateClose(bool showCopied = false, Action? onComplete = null)
    {
        _cts?.Cancel();
        if (!IsVisible) { onComplete?.Invoke(); return; }

        var dur = new Duration(TimeSpan.FromMilliseconds(160));
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        var opAnim = new DoubleAnimation(1, 0, dur) { EasingFunction = ease };
        opAnim.Completed += (_, _) =>
        {
            Hide();
            onComplete?.Invoke();
        };
        RootBorder.BeginAnimation(OpacityProperty, opAnim);

        var scaleAnim = new DoubleAnimation(1.0, 0.95, dur) { EasingFunction = ease };
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
    }
}
