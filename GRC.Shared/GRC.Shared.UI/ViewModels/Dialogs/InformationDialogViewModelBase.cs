using System;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GRC.Shared.UI.ViewModels.Dialogs;

/// <summary>
/// Base view model for informational dialogs with optional details and secondary action.
/// </summary>
public abstract partial class InformationDialogViewModelBase : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string? _details;
    [ObservableProperty] private string? _detailsHeaderText;
    [ObservableProperty] private Geometry? _iconData;
    [ObservableProperty] private string? _dismissButtonText;
    [ObservableProperty] private string? _secondaryButtonText;

    public IRelayCommand? SecondaryCommand { get; protected set; }
    public Action<bool>? CloseDialog { get; set; }
    protected Action? OnDismissed { get; set; }

    [RelayCommand]
    protected virtual void Dismiss()
    {
        OnDismissed?.Invoke();
        CloseDialog?.Invoke(true);
    }
}
