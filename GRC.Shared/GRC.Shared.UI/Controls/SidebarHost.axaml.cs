using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace GRC.Shared.UI.Controls;

public partial class SidebarHost : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SidebarHost, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<SidebarHost, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<SidebarHost, object?>(nameof(Header));

    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<SidebarHost, IDataTemplate?>(nameof(HeaderTemplate));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SidebarHost, bool>(nameof(IsExpanded), true, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> ExpandedWidthProperty =
        AvaloniaProperty.Register<SidebarHost, double>(nameof(ExpandedWidth), 220);

    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<SidebarHost, double>(nameof(CompactWidth), 56);

    public static readonly StyledProperty<string?> ExpandedLabelProperty =
        AvaloniaProperty.Register<SidebarHost, string?>(nameof(ExpandedLabel));

    public static readonly StyledProperty<string?> CollapsedLabelProperty =
        AvaloniaProperty.Register<SidebarHost, string?>(nameof(CollapsedLabel));

    public static readonly DirectProperty<SidebarHost, double> EffectiveWidthProperty =
        AvaloniaProperty.RegisterDirect<SidebarHost, double>(nameof(EffectiveWidth), o => o.EffectiveWidth);

    public static readonly DirectProperty<SidebarHost, string?> ToggleLabelProperty =
        AvaloniaProperty.RegisterDirect<SidebarHost, string?>(nameof(ToggleLabel), o => o.ToggleLabel);

    private Border? _sidebarBorder;
    private bool _isHoverExpanded;
    private double _effectiveWidth;
    private string? _toggleLabel;

    public SidebarHost()
    {
        InitializeComponent();
        UpdateEffectiveWidth();
        UpdateToggleLabel();
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public double ExpandedWidth
    {
        get => GetValue(ExpandedWidthProperty);
        set => SetValue(ExpandedWidthProperty, value);
    }

    public double CompactWidth
    {
        get => GetValue(CompactWidthProperty);
        set => SetValue(CompactWidthProperty, value);
    }

    public string? ExpandedLabel
    {
        get => GetValue(ExpandedLabelProperty);
        set => SetValue(ExpandedLabelProperty, value);
    }

    public string? CollapsedLabel
    {
        get => GetValue(CollapsedLabelProperty);
        set => SetValue(CollapsedLabelProperty, value);
    }

    public double EffectiveWidth
    {
        get => _effectiveWidth;
        private set => SetAndRaise(EffectiveWidthProperty, ref _effectiveWidth, value);
    }

    public string? ToggleLabel
    {
        get => _toggleLabel;
        private set => SetAndRaise(ToggleLabelProperty, ref _toggleLabel, value);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _sidebarBorder = this.FindControl<Border>("SidebarBorder");
        if (_sidebarBorder is not null)
        {
            _sidebarBorder.PointerEntered += OnSidebarPointerEntered;
            _sidebarBorder.PointerExited += OnSidebarPointerExited;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsExpandedProperty
            || change.Property == ExpandedWidthProperty
            || change.Property == CompactWidthProperty)
        {
            UpdateEffectiveWidth();
        }

        if (change.Property == IsExpandedProperty
            || change.Property == ExpandedLabelProperty
            || change.Property == CollapsedLabelProperty)
        {
            UpdateToggleLabel();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_sidebarBorder is not null)
        {
            _sidebarBorder.PointerEntered -= OnSidebarPointerEntered;
            _sidebarBorder.PointerExited -= OnSidebarPointerExited;
        }
    }

    private void OnSidebarPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!IsExpanded)
        {
            _isHoverExpanded = true;
            UpdateEffectiveWidth();
        }
    }

    private void OnSidebarPointerExited(object? sender, PointerEventArgs e)
    {
        if (_isHoverExpanded)
        {
            _isHoverExpanded = false;
            UpdateEffectiveWidth();
        }
    }

    private void UpdateEffectiveWidth()
    {
        var shouldExpand = IsExpanded || _isHoverExpanded;
        var targetWidth = shouldExpand ? ExpandedWidth : CompactWidth;
        if (!double.IsNaN(targetWidth) && !double.IsInfinity(targetWidth))
        {
            EffectiveWidth = targetWidth;
        }
    }

    private void UpdateToggleLabel()
    {
        var expanded = ExpandedLabel;
        var collapsed = CollapsedLabel;
        ToggleLabel = IsExpanded ? (expanded ?? collapsed) : (collapsed ?? expanded);
    }
}
