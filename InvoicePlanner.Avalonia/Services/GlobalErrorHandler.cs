using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace InvoicePlanner.Avalonia.Services;

public sealed class GlobalErrorHandler : IGlobalErrorHandler, IDisposable
{
    private readonly ILogger<GlobalErrorHandler> _logger;
    private IClassicDesktopStyleApplicationLifetime? _lifetime;
    private bool _isRegistered;

    public GlobalErrorHandler(ILogger<GlobalErrorHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Register(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        if (lifetime is null)
        {
            throw new ArgumentNullException(nameof(lifetime));
        }

        if (_isRegistered)
        {
            return;
        }

        _lifetime = lifetime;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;
        lifetime.Exit += OnExit;
        _isRegistered = true;
    }

    public void Dispose()
    {
        if (!_isRegistered)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        Dispatcher.UIThread.UnhandledException -= OnUiThreadUnhandledException;
        if (_lifetime is not null)
        {
            _lifetime.Exit -= OnExit;
        }

        _isRegistered = false;
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        Dispose();
    }

    private void OnUiThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        HandleException(e.Exception, "UI thread");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        HandleException(e.Exception, "TaskScheduler");
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            HandleException(exception, "AppDomain");
        }
    }

    private void HandleException(Exception exception, string source)
    {
        try
        {
            _logger.LogError(exception, "Unhandled exception from {Source}", source);
        }
        catch (Exception handlerEx)
        {
            _logger.LogError(handlerEx, "Failed to show error dialog for exception from {Source}", source);
        }
    }
}
