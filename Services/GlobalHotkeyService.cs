using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Scriptly.Services;

/// <summary>
/// Registers a system-wide hotkey and fires an event when it is pressed.
/// </summary>
public class GlobalHotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0xA100;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier flags
    private const uint MOD_NONE    = 0x0000;
    private const uint MOD_ALT     = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT   = 0x0004;
    private const uint MOD_WIN     = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    public event Action? HotkeyPressed;

    private HwndSource? _source;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _registered = false;

    public void Initialize(Window helperWindow)
    {
        _hwnd = new WindowInteropHelper(helperWindow).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public bool Register(string modifiers, string key)
    {
        if (_hwnd == IntPtr.Zero) return false;

        if (_registered)
            UnregisterHotKey(_hwnd, HOTKEY_ID);

        uint mods = MOD_NOREPEAT;
        var parts = modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            mods |= part.Trim().ToLower() switch
            {
                "ctrl" or "control" => MOD_CONTROL,
                "shift" => MOD_SHIFT,
                "alt" => MOD_ALT,
                "win" => MOD_WIN,
                _ => 0
            };
        }

        if (!Enum.TryParse<Key>(key.Trim(), true, out var wpfKey))
            return false;

        var vk = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
        _registered = RegisterHotKey(_hwnd, HOTKEY_ID, mods, vk);
        return _registered;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered && _hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, HOTKEY_ID);
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
    }
}
