using System;
using Avalonia;
using Avalonia.Skia;
using Avalonia.Win32;

namespace InvoicePlanner.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseWin32()
        .UseSkia()
        .WithInterFont()
        .LogToTrace();
}
