using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace GRC.Shared.UI.Controls;

public partial class SearchBox : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SearchBox, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<SearchBox, string?>(nameof(Placeholder));

    public static readonly StyledProperty<string> ClearButtonTextProperty =
        AvaloniaProperty.Register<SearchBox, string>(nameof(ClearButtonText), "Clear");

    public SearchBox()
    {
        InitializeComponent();
        ClearCommand = new RelayCommand(ClearText, () => !string.IsNullOrEmpty(Text));
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string ClearButtonText
    {
        get => GetValue(ClearButtonTextProperty);
        set => SetValue(ClearButtonTextProperty, value);
    }

    public IRelayCommand ClearCommand { get; }

    private void ClearText()
    {
        Text = string.Empty;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            ClearCommand.NotifyCanExecuteChanged();
        }
    }
}
