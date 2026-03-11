// ── Global type aliases to avoid WPF/WinForms namespace conflicts ──────────────
// WPF types win by default (most code is WPF). WinForms types used only in TrayService.

global using Application    = System.Windows.Application;
global using Clipboard      = System.Windows.Clipboard;
global using MessageBox     = System.Windows.MessageBox;
global using MessageBoxButton = System.Windows.MessageBoxButton;
global using MessageBoxImage  = System.Windows.MessageBoxImage;
global using Button         = System.Windows.Controls.Button;
global using KeyEventArgs   = System.Windows.Input.KeyEventArgs;
global using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

// WinForms aliases used in TrayService
global using WinFormsApp    = System.Windows.Forms.Application;
