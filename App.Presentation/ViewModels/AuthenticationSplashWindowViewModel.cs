using CommunityToolkit.Mvvm.ComponentModel;

namespace App.Presentation.ViewModels;

public partial class AuthenticationSplashWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "Preparing sign-in...";

    [ObservableProperty]
    private bool _isProgressVisible = true;
}