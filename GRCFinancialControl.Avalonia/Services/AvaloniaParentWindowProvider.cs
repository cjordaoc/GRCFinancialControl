using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using GRCFinancialControl.Core.Authentication;

namespace GRCFinancialControl.Avalonia.Services;

/// <summary>
/// Provides the main window handle for the Avalonia application.
/// </summary>
public class AvaloniaParentWindowProvider : IParentWindowProvider
{
    /// <inheritdoc />
    public IntPtr GetMainWindowHandle()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return IntPtr.Zero;
        }

        var mainWindow = desktop.MainWindow;
        if (mainWindow is null)
        {
            return IntPtr.Zero;
        }

        return mainWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    }
}