using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Scriptly.Models;
using Scriptly.Services;

namespace Scriptly.Windows;

public partial class HistoryWindow : Window
{
    private readonly HistoryService _historyService;

    public HistoryWindow(HistoryService historyService)
    {
        _historyService = historyService;
        InitializeComponent();

        // Live-update the list if the window is already visible when a result finishes
        _historyService.Changed += () =>
        {
            if (IsVisible)
                Dispatcher.Invoke(RefreshList);
        };
    }

    /// <summary>Refresh entries and animate the window open. Safe to call repeatedly.</summary>
    public void ShowPanel()
    {
        RefreshList();

        // Reset any animation hold-overs from the previous close
        RootBorder.BeginAnimation(OpacityProperty, null);
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
        TranslateT.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);

        var screen = SystemParameters.WorkArea;
        Left = (screen.Width  - Width)  / 2;
        Top  = (screen.Height - Height) / 2;

        Show();
        Activate();
        AnimateOpen();
    }

    private void RefreshList()
    {
        EntriesScroll.ScrollToTop();

        var entries   = _historyService.Entries;
        bool hasItems = entries.Count > 0;

        EntriesList.ItemsSource  = null;
        EntriesList.ItemsSource  = entries;

        EmptyPanel.Visibility    = hasItems ? Visibility.Collapsed : Visibility.Visible;
        EntriesScroll.Visibility = hasItems ? Visibility.Visible   : Visibility.Collapsed;
        ClearAllButton.Visibility = hasItems ? Visibility.Visible  : Visibility.Collapsed;
        CountLabel.Text          = hasItems ? $"({entries.Count})" : string.Empty;
    }

    private void CopyEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HistoryEntry entry && !string.IsNullOrEmpty(entry.Result))
        {
            Clipboard.SetText(entry.Result);

            FooterHint.Text = "✓ Copied to clipboard";
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (_, _) => { FooterHint.Text = "Click Copy on any entry to copy the result"; timer.Stop(); };
            timer.Start();
        }
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _historyService.Clear();
        RefreshList();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => AnimateClose();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { AnimateClose(); e.Handled = true; }
        base.OnKeyDown(e);
    }

    // ── Animations ───────────────────────────────────────────────────────────

    private void AnimateOpen()
    {
        var dur  = new Duration(TimeSpan.FromMilliseconds(220));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        RootBorder.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, dur) { EasingFunction = ease });

        var scaleAnim = new DoubleAnimation(0.94, 1.0, dur) { EasingFunction = ease };
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);

        TranslateT.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(-10, 0, dur) { EasingFunction = ease });
    }

    private void AnimateClose()
    {
        if (!IsVisible) return;

        var dur  = new Duration(TimeSpan.FromMilliseconds(160));
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        var opAnim = new DoubleAnimation(1, 0, dur) { EasingFunction = ease };
        opAnim.Completed += (_, _) => Hide();
        RootBorder.BeginAnimation(OpacityProperty, opAnim);

        var scaleAnim = new DoubleAnimation(1.0, 0.95, dur) { EasingFunction = ease };
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
        ScaleT.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
    }
}
