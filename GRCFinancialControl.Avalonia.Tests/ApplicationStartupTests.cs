using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using GRCFinancialControl.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GRCFinancialControl.Avalonia.Tests;

[TestClass]
public class ApplicationStartupTests
{
    [TestMethod]
    public async Task ApplicationCanBeInitializedAndMainWindowResolved()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            var app = (App)Application.Current!;
            var mainWindow = app.Services.GetRequiredService<MainWindow>();
            Assert.IsNotNull(mainWindow);
        }, CancellationToken.None);
    }
}
