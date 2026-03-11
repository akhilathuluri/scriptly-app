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
    private readonly HistoryService _historyService;
    private CancellationTokenSource? _cts;
    private string _resultText = string.Empty;

    // Remembered for Regenerate and FullEditor
    private ActionItem? _currentAction;
    private string _currentSelectedText = string.Empty;

    public ResultWindow(
        AiService aiService,
        TextCaptureService textCapture,
        SettingsService settingsService,
        HistoryService historyService)
    {
        _aiService = aiService;
        _textCapture = textCapture;
        _settingsService = settingsService;
        _historyService = historyService;

        InitializeComponent();
    }

    // ── Entry point — safe to call multiple times on the same instance ───────

    public void ShowWithProcessing(ActionItem action, string selectedText)
    {
        // Cancel any in-flight request from a previous use
        _cts?.Cancel();
        _resultText = string.Empty;

        // Remember for Regenerate / Expand
        _currentAction       = action;
        _currentSelectedText = selectedText;

        // Reset all panels to the initial "thinking" state
        ThinkingPanel.Visibility  = Visibility.Visible;
        ResultPanel.Visibility    = Visibility.Collapsed;
        ErrorPanel.Visibility     = Visibility.Collapsed;
        ButtonPanel.Visibility    = Visibility.Collapsed;
        ExpandButton.Visibility   = Visibility.Collapsed;
        ResultText.Text           = string.Empty;
        ErrorText.Text            = string.Empty;

        // Clear animation holds from previous close so XAML base values restore:
        //   RootBorder.Opacity → 0, ScaleT → 0.94, TranslateT.Y → -10
        RootBorder.BeginAnimation(OpacityProperty, null);
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
        TranslateT.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
        ResultPanel.BeginAnimation(OpacityProperty, null);

        ActionIcon.Text  = action.Icon;
        ActionTitle.Text = action.Name;

        var screen = SystemParameters.WorkArea;
        Left = (screen.Width - Width) / 2;
        Top  = (screen.Height - 400) / 2;

        Show();
        Activate();

        // Start animations explicitly every time (Loaded only fires on first Show)
        StartThinkingAnimation();
        AnimateOpen();

        _ = ProcessAsync(action, selectedText);
    }

    // ── Streaming processor ──────────────────────────────────────────────────

    private async Task ProcessAsync(ActionItem action, string selectedText)
    {
        _cts = new CancellationTokenSource();
        bool firstToken = true;

        try
        {
            await foreach (var token in _aiService.StreamAsync(action.Prompt!, selectedText, _cts.Token))
            {
                var t = token;
                Dispatcher.Invoke(() =>
                {
                    // On the very first token: swap thinking panel → result panel
                    if (firstToken)
                    {
                        firstToken = false;
                        ThinkingPanel.Visibility = Visibility.Collapsed;
                        ResultPanel.Visibility   = Visibility.Visible;
                        ButtonPanel.Visibility   = Visibility.Visible;
                        ExpandButton.Visibility  = Visibility.Visible;
                        AnimateResultIn();
                    }
                    _resultText     += t;
                    ResultText.Text  = _resultText;
                    ResultText.ScrollToEnd();
                });
            }

            // Stream complete — ensure buttons are visible even if stream was empty
            Dispatcher.Invoke(() =>
            {
                ThinkingPanel.Visibility = Visibility.Collapsed;
                ButtonPanel.Visibility   = Visibility.Visible;
                if (firstToken) // zero tokens received
                {
                    ErrorPanel.Visibility = Visibility.Visible;
                    ErrorText.Text        = "No response received from the AI.";
                }
                else
                {
                    // Persist to history only when we have a complete result
                    _historyService.Add(new HistoryEntry
                    {
                        ActionId     = _currentAction!.Id,
                        ActionName   = _currentAction!.Name,
                        ActionIcon   = _currentAction!.Icon,
                        SelectedText = _currentSelectedText,
                        Result       = _resultText
                    });
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Window was closed or reused before stream completed — expected, ignore
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                ThinkingPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility    = Visibility.Visible;
                ErrorText.Text           = ex.Message;
                ButtonPanel.Visibility   = Visibility.Visible;
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

    private void RegenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAction != null)
            ShowWithProcessing(_currentAction, _currentSelectedText);
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAction == null || string.IsNullOrEmpty(_resultText)) return;
        var editor = new FullEditorWindow(_resultText, _currentAction, _textCapture);
        editor.Show();
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
