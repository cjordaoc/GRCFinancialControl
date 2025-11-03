using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class NavigationItemViewModel : ObservableObject
{
    private readonly Action<NavigationItemViewModel> _activate;

    public NavigationItemViewModel(
        string key,
        string title,
        ViewModelBase targetViewModel,
        Action<NavigationItemViewModel> activate,
        Geometry? icon = null)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        TargetViewModel = targetViewModel ?? throw new ArgumentNullException(nameof(targetViewModel));
        _activate = activate ?? throw new ArgumentNullException(nameof(activate));
        SelectCommand = new RelayCommand(() => _activate(this));
        _icon = icon;
    }

    public string Key { get; }

    public string Title { get; }

    public ViewModelBase TargetViewModel { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Geometry? _icon;

    public IRelayCommand SelectCommand { get; }

}
