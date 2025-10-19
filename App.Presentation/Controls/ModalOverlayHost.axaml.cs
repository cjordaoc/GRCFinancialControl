using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace App.Presentation.Controls;

public partial class ModalOverlayHost : UserControl, IModalOverlayHost
{
    public static readonly StyledProperty<object?> OverlayContentProperty =
        AvaloniaProperty.Register<ModalOverlayHost, object?>(nameof(OverlayContent));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ModalOverlayHost, string?>(nameof(Title));

    public static readonly StyledProperty<bool> CanCloseProperty =
        AvaloniaProperty.Register<ModalOverlayHost, bool>(nameof(CanClose), true);

    public static readonly StyledProperty<ICommand?> PrimaryActionCommandProperty =
        AvaloniaProperty.Register<ModalOverlayHost, ICommand?>(nameof(PrimaryActionCommand));

    public static readonly StyledProperty<string?> PrimaryActionTextProperty =
        AvaloniaProperty.Register<ModalOverlayHost, string?>(nameof(PrimaryActionText));

    public static readonly StyledProperty<bool> IsPrimaryActionVisibleProperty =
        AvaloniaProperty.Register<ModalOverlayHost, bool>(nameof(IsPrimaryActionVisible));

    public static readonly DirectProperty<ModalOverlayHost, bool> IsOverlayOpenProperty =
        AvaloniaProperty.RegisterDirect<ModalOverlayHost, bool>(nameof(IsOverlayOpen), host => host.IsOverlayOpen);

    private bool _isOverlayOpen;
    private bool _isClosing;
    private TaskCompletionSource<bool?>? _completionSource;
    private Border? _dialogContainer;
    private ContentPresenter? _overlayPresenter;
    private Control? _currentOverlayControl;
    private IModalOverlayActionProvider? _actionProvider;
    private INotifyPropertyChanged? _actionProviderNotifier;

    public event EventHandler<ModalOverlayCloseRequestedEventArgs>? CloseRequested;

    public ModalOverlayHost()
    {
        InitializeComponent();

        this.PropertyChanged += OnHostPropertyChanged;
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
    }

    public object? OverlayContent
    {
        get => GetValue(OverlayContentProperty);
        set => SetValue(OverlayContentProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool CanClose
    {
        get => GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    public ICommand? PrimaryActionCommand
    {
        get => GetValue(PrimaryActionCommandProperty);
        private set => SetValue(PrimaryActionCommandProperty, value);
    }

    public string? PrimaryActionText
    {
        get => GetValue(PrimaryActionTextProperty);
        private set => SetValue(PrimaryActionTextProperty, value);
    }

    public bool IsPrimaryActionVisible
    {
        get => GetValue(IsPrimaryActionVisibleProperty);
        private set => SetValue(IsPrimaryActionVisibleProperty, value);
    }

    public bool IsOverlayOpen
    {
        get => _isOverlayOpen;
        private set => SetAndRaise(IsOverlayOpenProperty, ref _isOverlayOpen, value);
    }

    public Task<bool?> ShowModalAsync(UserControl content, string? title = null, bool canClose = true)
    {
        ArgumentNullException.ThrowIfNull(content);

        _completionSource?.TrySetResult(null);
        _completionSource = new TaskCompletionSource<bool?>();

        Dispatcher.UIThread.Post(() =>
        {
            Title = title;
            CanClose = canClose;
            OverlayContent = content;
        });

        return _completionSource.Task;
    }

    public void Close(bool? result = null)
    {
        if (!CanClose)
        {
            return;
        }

        PerformClose(result);
    }

    private void PerformClose(bool? result)
    {
        if (_isClosing)
        {
            return;
        }

        try
        {
            _isClosing = true;
            SetCurrentValue(OverlayContentProperty, null);
            Title = null;
            CanClose = true;

            if (_completionSource is { } tcs)
            {
                tcs.TrySetResult(result);
                _completionSource = null;
            }
        }
        finally
        {
            _isClosing = false;
        }
    }

    private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == BoundsProperty && change.NewValue is Rect bounds)
        {
            UpdateLayoutForBounds(bounds);
        }
        else if (change.Property == OverlayContentProperty)
        {
            OnOverlayContentChanged(change.NewValue);
        }
    }

    private void OnOverlayContentChanged(object? content)
    {
        if (content is null)
        {
            DetachCurrentContent();
            if (IsOverlayOpen)
            {
                IsOverlayOpen = false;
            }

            if (!_isClosing)
            {
                Title = null;
                CanClose = true;
                _completionSource?.TrySetResult(null);
                _completionSource = null;
            }

            return;
        }

        if (!ReferenceEquals(_currentOverlayControl, content))
        {
            DetachCurrentContent();
            if (content is Control control)
            {
                _currentOverlayControl = control;
                _currentOverlayControl.DataContextChanged += OnOverlayContentDataContextChanged;
            }
        }

        AttachActionProvider(GetActionProvider(content));

        if (!IsOverlayOpen)
        {
            IsOverlayOpen = true;
        }

        Dispatcher.UIThread.Post(FocusFirstElement, DispatcherPriority.Background);
    }

    private void UpdateLayoutForBounds(Rect rect)
    {
        if (_dialogContainer is null)
        {
            return;
        }

        var targetWidth = rect.Width * 0.9;
        var targetHeight = rect.Height * 0.9;

        _dialogContainer.MaxWidth = Math.Max(0, targetWidth);
        _dialogContainer.MaxHeight = Math.Max(0, targetHeight);
    }

    private void FocusFirstElement()
    {
        if (_overlayPresenter?.Content is not Control contentControl)
        {
            return;
        }

        var focusable = contentControl
            .GetVisualDescendants()
            .OfType<IInputElement>()
            .FirstOrDefault(element => element.Focusable && element.IsEffectivelyVisible && element.IsEffectivelyEnabled);

        focusable?.Focus();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && CanClose && IsOverlayOpen)
        {
            CloseRequested?.Invoke(this, new ModalOverlayCloseRequestedEventArgs(false));
            e.Handled = true;
        }
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        if (!CanClose)
        {
            return;
        }

        var handler = CloseRequested;
        if (handler is null)
        {
            Close(false);
        }
        else
        {
            handler(this, new ModalOverlayCloseRequestedEventArgs(false));
        }

        e.Handled = true;
    }

    private void DetachCurrentContent()
    {
        if (_currentOverlayControl is not null)
        {
            _currentOverlayControl.DataContextChanged -= OnOverlayContentDataContextChanged;
            _currentOverlayControl = null;
        }

        AttachActionProvider(null);
    }

    private void OnOverlayContentDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control control)
        {
            AttachActionProvider(GetActionProvider(control));
        }
    }

    private void AttachActionProvider(IModalOverlayActionProvider? provider)
    {
        if (ReferenceEquals(_actionProvider, provider))
        {
            RefreshActionProperties();
            return;
        }

        if (_actionProviderNotifier is not null)
        {
            _actionProviderNotifier.PropertyChanged -= OnActionProviderPropertyChanged;
        }

        _actionProvider = provider;
        _actionProviderNotifier = provider as INotifyPropertyChanged;
        if (_actionProviderNotifier is not null)
        {
            _actionProviderNotifier.PropertyChanged += OnActionProviderPropertyChanged;
        }

        RefreshActionProperties();
    }

    private void OnActionProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshActionProperties();
    }

    private IModalOverlayActionProvider? GetActionProvider(object? content)
    {
        if (content is IModalOverlayActionProvider provider)
        {
            return provider;
        }

        if (content is Control control)
        {
            if (control.DataContext is IModalOverlayActionProvider dataContextProvider)
            {
                return dataContextProvider;
            }

            return control as IModalOverlayActionProvider;
        }

        return null;
    }

    private void RefreshActionProperties()
    {
        if (_actionProvider is null)
        {
            PrimaryActionCommand = null;
            PrimaryActionText = null;
            IsPrimaryActionVisible = false;
            return;
        }

        var command = _actionProvider.PrimaryActionCommand;
        var isVisible = _actionProvider.IsPrimaryActionVisible && command is not null;

        PrimaryActionCommand = command;
        PrimaryActionText = _actionProvider.PrimaryActionText;
        IsPrimaryActionVisible = isVisible;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _dialogContainer = this.FindControl<Border>("DialogContainer");
        _overlayPresenter = this.FindControl<ContentPresenter>("OverlayContentPresenter");
    }
}
