using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace App.Presentation.Views;

public partial class AuthenticationSplashWindow : Window
{
    public AuthenticationSplashWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
        Progress.IsVisible = true;
    }

    public void ShowError(string message)
    {
        StatusText.Text = message;
        Progress.IsVisible = false;
    }
}
