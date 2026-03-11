using System.Runtime.InteropServices;
using System.Windows;

namespace Scriptly.Services;

/// <summary>
/// Captures selected text from any foreground application using
/// the clipboard + Ctrl+C approach, then restores the clipboard.
/// Also provides the cursor/caret position for popup placement.
/// </summary>
public class TextCaptureService
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private const byte VK_C       = 0x43;
    private const byte VK_CONTROL = 0x11;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>
    /// Gets the current text selection from the focused application by
    /// temporarily sending Ctrl+C and reading the clipboard.
    /// Returns null if nothing was selected.
    /// </summary>
    public async Task<string?> GetSelectedTextAsync()
    {
        // Save current clipboard content
        string? previousClipboard = null;
        try
        {
            if (Clipboard.ContainsText())
                previousClipboard = Clipboard.GetText();

            // Clear clipboard so we can detect if anything was copied
            Clipboard.Clear();
        }
        catch { }

        // Send Ctrl+C
        keybd_event(VK_CONTROL, 0, 0, 0);
        keybd_event(VK_C, 0, 0, 0);
        keybd_event(VK_C, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);

        // Wait for clipboard to be populated
        await Task.Delay(150);

        string? selected = null;
        try
        {
            if (Clipboard.ContainsText())
            {
                selected = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(selected))
                    selected = null;
            }
        }
        catch { }

        // Restore previous clipboard content
        try
        {
            if (previousClipboard != null)
                Clipboard.SetText(previousClipboard);
            else
                Clipboard.Clear();
        }
        catch { }

        return selected;
    }

    /// <summary>
    /// Replaces the selected text in the source application by putting
    /// the result on the clipboard and sending Ctrl+V.
    /// </summary>
    public async Task ReplaceSelectedTextAsync(string newText)
    {
        try
        {
            Clipboard.SetText(newText);
        }
        catch { return; }

        await Task.Delay(80);

        // Send Ctrl+V
        keybd_event(VK_CONTROL, 0, 0, 0);
        const byte VK_V = 0x56;
        keybd_event(VK_V, 0, 0, 0);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);

        await Task.Delay(80);
    }

    /// <summary>
    /// Returns the current mouse cursor position for popup placement.
    /// </summary>
    public System.Windows.Point GetCursorPosition()
    {
        GetCursorPos(out var p);
        return new System.Windows.Point(p.X, p.Y);
    }
}
