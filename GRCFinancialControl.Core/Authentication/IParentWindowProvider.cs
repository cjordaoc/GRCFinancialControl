using System;

namespace GRCFinancialControl.Core.Authentication;

/// <summary>
/// Provides a platform-agnostic way to obtain a handle to the parent window for authentication dialogs.
/// </summary>
public interface IParentWindowProvider
{
    /// <summary>
    /// Gets the handle (pointer) to the main window of the application.
    /// </summary>
    /// <returns>An <see cref="IntPtr"/> representing the window handle.</returns>
    IntPtr GetMainWindowHandle();
}