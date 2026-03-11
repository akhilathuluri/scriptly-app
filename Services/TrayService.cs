using System.Drawing;
using System.Windows.Forms;

namespace Scriptly.Services;

/// <summary>
/// Manages the system tray icon and its context menu.
/// Uses Windows Forms NotifyIcon for native tray support.
/// </summary>
public class TrayService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;

    public event Action? OpenSettingsRequested;
    public event Action? ExitRequested;

    public void Initialize()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.BackColor = Color.FromArgb(22, 22, 30);
        _contextMenu.ForeColor = Color.FromArgb(224, 224, 240);
        _contextMenu.Font = new Font("Segoe UI", 9.5f);
        _contextMenu.RenderMode = ToolStripRenderMode.System;

        var titleItem = new ToolStripMenuItem("Scriptly")
        {
            Enabled = false,
            Font = new Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
        };
        _contextMenu.Items.Add(titleItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        var settingsItem = new ToolStripMenuItem("⚙  Settings");
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke();
        _contextMenu.Items.Add(settingsItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("✕  Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Text = "Scriptly — Select text and press Ctrl+Shift+Space",
            Visible = true,
            ContextMenuStrip = _contextMenu,
            Icon = CreateIcon()
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
    }

    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
    }

    private static Icon CreateIcon()
    {
        // Draw a simple "S" letter icon at 16x16 using GDI+
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Background circle
        using var bgBrush = new SolidBrush(Color.FromArgb(90, 74, 247));
        g.FillEllipse(bgBrush, 0, 0, 15, 15);

        // Letter "S"
        using var font = new Font("Segoe UI", 8f, System.Drawing.FontStyle.Bold, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("S", font, textBrush, new RectangleF(0, 0, 16, 16), sf);

        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
    }
}
