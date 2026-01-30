using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GRC.Shared.UI.ViewModels.Dialogs;

/// <summary>
/// Base view model for confirmation dialogs with configurable texts and callbacks.
/// </summary>
public abstract partial class ConfirmationDialogViewModelBase : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string? _confirmButtonText;
    [ObservableProperty] private string? _cancelButtonText;
    [ObservableProperty] private object? _customContent;

    protected Action? OnConfirmed { get; set; }
    protected Action? OnCanceled { get; set; }
    public Action<bool>? CloseDialog { get; set; }

    [RelayCommand]
    protected virtual void Confirm()
    {
        OnConfirmed?.Invoke();
        CloseDialog?.Invoke(true);
    }

    [RelayCommand]
    protected virtual void Cancel()
    {
        OnCanceled?.Invoke();
        CloseDialog?.Invoke(false);
    }
}
