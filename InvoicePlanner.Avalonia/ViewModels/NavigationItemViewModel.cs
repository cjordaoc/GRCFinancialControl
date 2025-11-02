using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class NavigationItemViewModel : ObservableObject
{
    private readonly Action<NavigationItemViewModel> _activate;

    public NavigationItemViewModel(
        string key,
        string title,
        ViewModelBase targetViewModel,
        Action<NavigationItemViewModel> activate,
        string? icon = null)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        TargetViewModel = targetViewModel ?? throw new ArgumentNullException(nameof(targetViewModel));
        _activate = activate ?? throw new ArgumentNullException(nameof(activate));
        SelectCommand = new RelayCommand(() => _activate(this));
        _icon = string.IsNullOrWhiteSpace(icon) ? BuildDefaultIcon(title) : icon;
    }

    public string Key { get; }

    public string Title { get; }

    public ViewModelBase TargetViewModel { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _icon = string.Empty;

    public IRelayCommand SelectCommand { get; }

    private static string BuildDefaultIcon(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var segments = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = FindLeadingCharacter(segments.FirstOrDefault());
        if (first is null)
        {
            return string.Empty;
        }

        var second = segments.Skip(1).Select(FindLeadingCharacter).FirstOrDefault(c => c is not null);
        return second is null
            ? char.ToUpperInvariant(first.Value).ToString()
            : string.Concat(char.ToUpperInvariant(first.Value), char.ToUpperInvariant(second.Value));
    }

    private static char? FindLeadingCharacter(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return null;
        }

        foreach (var character in segment)
        {
            if (char.IsLetterOrDigit(character))
            {
                return character;
            }
        }

        return null;
    }
}
