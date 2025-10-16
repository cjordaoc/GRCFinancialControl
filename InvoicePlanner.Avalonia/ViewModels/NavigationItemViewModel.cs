using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class NavigationItemViewModel : ObservableObject
{
    private readonly Action<NavigationItemViewModel> _activate;

    public NavigationItemViewModel(string title, Action<NavigationItemViewModel> activate)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        _activate = activate ?? throw new ArgumentNullException(nameof(activate));
        SelectCommand = new RelayCommand(() => _activate(this));
    }

    public string Title { get; }

    [ObservableProperty]
    private bool _isSelected;

    public IRelayCommand SelectCommand { get; }
}
