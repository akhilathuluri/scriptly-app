using System.Runtime.InteropServices;
using System.Windows;

namespace Scriptly.Services;

/// <summary>
/// Captures selected text from any foreground application using
/// the clipboard + Ctrl+C approach (via SendInput), then restores the clipboard.
/// Also provides the cursor/caret position for popup placement.
/// </summary>
public class TextCaptureService
{
    // ── P/Invoke ────────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // ── Structures ──────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL, wParamH;
    }

    // ── Constants ───────────────────────────────────────────────────────────

    private const uint   INPUT_KEYBOARD = 1;
    private const uint   KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_SHIFT   = 0x10;
    private const ushort VK_ALT     = 0x12;
    private const ushort VK_LWIN    = 0x5B;
    private const ushort VK_RWIN    = 0x5C;
    private const ushort VK_C = 0x43;
    private const ushort VK_V = 0x56;

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static INPUT KeyDown(ushort vk) => new INPUT
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk } }
    };

    private static INPUT KeyUp(ushort vk) => new INPUT
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
    };

    private static void SendCtrlKey(ushort key)
    {
        SendInput(4, new[] { KeyDown(VK_CONTROL), KeyDown(key), KeyUp(key), KeyUp(VK_CONTROL) },
            Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Waits until Shift, Alt, and Win keys are all physically released (or until
    /// the timeout elapses). This prevents our injected Ctrl+C from combining with
    /// a held Shift to produce Ctrl+Shift+C — which opens DevTools in Chrome/Edge
    /// and triggers other unintended commands in VS Code, Telegram, etc.
    /// </summary>
    private static async Task WaitForModifiersReleasedAsync(int timeoutMs = 600)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            // GetAsyncKeyState: negative short = high bit set = key is physically held
            bool held = GetAsyncKeyState(VK_SHIFT) < 0 ||
                        GetAsyncKeyState(VK_ALT)   < 0 ||
                        GetAsyncKeyState(VK_LWIN)  < 0 ||
                        GetAsyncKeyState(VK_RWIN)  < 0;
            if (!held) break;
            await Task.Delay(15);
        }
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the current text selection from the focused application by
    /// temporarily sending Ctrl+C via SendInput and reading the clipboard.
    /// Returns null if nothing was selected.
    /// </summary>
    public async Task<string?> GetSelectedTextAsync()
    {
        // Capture the foreground window immediately (before any async gap)
        IntPtr sourceWindow = GetForegroundWindow();

        // Save current clipboard content
        string? previousClipboard = null;
        try
        {
            if (Clipboard.ContainsText())
                previousClipboard = Clipboard.GetText();
            Clipboard.Clear();
        }
        catch { }

        // Wait for the user to release Shift/Alt/Win before we inject Ctrl+C.
        // If we skip this, a still-held Shift turns our Ctrl+C into Ctrl+Shift+C,
        // which opens DevTools in Chromium browsers and fires IDE commands in VS Code.
        // Ctrl does NOT need to be released — we drive it independently via SendInput.
        await WaitForModifiersReleasedAsync(600);

        // Restore foreground to the source window in case focus shifted
        if (sourceWindow != IntPtr.Zero)
            SetForegroundWindow(sourceWindow);

        await Task.Delay(30);

        // Send a clean Ctrl+C — no stray modifiers can interfere now
        SendCtrlKey(VK_C);

        // Wait for the target app to process the copy and populate the clipboard
        await Task.Delay(200);

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
    /// the result on the clipboard and sending Ctrl+V via SendInput.
    /// </summary>
    public async Task ReplaceSelectedTextAsync(string newText)
    {
        try { Clipboard.SetText(newText); }
        catch { return; }

        await Task.Delay(80);

        SendCtrlKey(VK_V);

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
