using System.Windows;
using Scriptly.Models;
using Scriptly.Services;

namespace Scriptly.Windows;

public partial class CustomActionDialog : Window
{
    public CustomAction? Result { get; private set; }
    private readonly Guid _existingId;

    public CustomActionDialog(CustomAction? existing = null)
    {
        InitializeComponent();

        if (existing != null)
        {
            _existingId = existing.Id;
            IconBox.Text = existing.Icon;
            NameBox.Text = existing.Name;
            DescBox.Text = existing.Description;
            InstructionsBox.Text = existing.Instructions;
        }
        else
        {
            _existingId = Guid.NewGuid();
        }

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowGpuAnimationService.ResetOpenState(RootBorder, ScaleT, 0.94, TranslateT, 8);
        WindowGpuAnimationService.AnimateOpen(RootBorder, ScaleT, 0.94, TranslateT, 8);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(InstructionsBox.Text))
        {
            MessageBox.Show("Name and Instructions are required.", "Scriptly", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new CustomAction
        {
            Id = _existingId,
            Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? "✨" : IconBox.Text.Trim(),
            Name = NameBox.Text.Trim(),
            Description = DescBox.Text.Trim(),
            Instructions = InstructionsBox.Text.Trim()
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
