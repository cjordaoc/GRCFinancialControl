using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Moq;
using Xunit;

namespace GRCFinancialControl.Tests.ViewModels;

public sealed class TasksViewModelRetainTemplateTests
{
    [Fact]
    public async Task GenerateRetainTemplateAsync_WhenUserCancels_SetsCancelledMessage()
    {
        using var localization = new LocalizationScope(new Dictionary<string, string>
        {
            ["Tasks.Status.RetainTemplateCancelled"] = "Retain template generation cancelled."
        });

        var filePicker = new Mock<IFilePickerService>();
        filePicker
            .Setup(service => service.OpenFileAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]?>()))
            .ReturnsAsync((string?)null);

        var generator = new Mock<IRetainTemplateGenerator>(MockBehavior.Strict);

        var viewModel = new TasksViewModel(filePicker.Object, generator.Object);

        await viewModel.GenerateRetainTemplateCommand.ExecuteAsync(null);

        Assert.Equal("Retain template generation cancelled.", viewModel.StatusMessage);
        generator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateRetainTemplateAsync_WhenGenerationSucceeds_SetsSuccessMessage()
    {
        using var localization = new LocalizationScope(new Dictionary<string, string>
        {
            ["Tasks.Status.RetainTemplateSuccess"] = "Template successfully generated at {0}."
        });

        const string allocationPath = "planning.xlsx";
        const string outputPath = "RetainTemplate_20240101_010203.xlsx";

        var filePicker = new Mock<IFilePickerService>();
        filePicker
            .Setup(service => service.OpenFileAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]?>()))
            .ReturnsAsync(allocationPath);

        var generator = new Mock<IRetainTemplateGenerator>();
        generator
            .Setup(service => service.GenerateRetainTemplateAsync(allocationPath))
            .ReturnsAsync(outputPath);

        var viewModel = new TasksViewModel(filePicker.Object, generator.Object);

        await viewModel.GenerateRetainTemplateCommand.ExecuteAsync(null);

        Assert.Equal($"Template successfully generated at {outputPath}.", viewModel.StatusMessage);
        generator.Verify(service => service.GenerateRetainTemplateAsync(allocationPath), Times.Once);
    }

    [Fact]
    public async Task GenerateRetainTemplateAsync_WhenGenerationFails_SetsFailureMessage()
    {
        using var localization = new LocalizationScope(new Dictionary<string, string>
        {
            ["Tasks.Status.RetainTemplateFailure"] = "Failed to generate Retain template: {0}"
        });

        const string allocationPath = "planning.xlsx";

        var filePicker = new Mock<IFilePickerService>();
        filePicker
            .Setup(service => service.OpenFileAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]?>()))
            .ReturnsAsync(allocationPath);

        var generator = new Mock<IRetainTemplateGenerator>();
        generator
            .Setup(service => service.GenerateRetainTemplateAsync(allocationPath))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var viewModel = new TasksViewModel(filePicker.Object, generator.Object);

        await viewModel.GenerateRetainTemplateCommand.ExecuteAsync(null);

        Assert.Equal("Failed to generate Retain template: boom", viewModel.StatusMessage);
    }

    private sealed class LocalizationScope : IDisposable
    {
        private readonly ILocalizationProvider _previousProvider;

        public LocalizationScope(IDictionary<string, string> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            var providerField = typeof(LocalizationRegistry).GetField("_provider", BindingFlags.NonPublic | BindingFlags.Static)
                                ?? throw new InvalidOperationException("Localization provider field not found.");

            _previousProvider = (ILocalizationProvider)providerField.GetValue(null)!;
            LocalizationRegistry.Configure(new DictionaryLocalizationProvider(values));
        }

        public void Dispose()
        {
            LocalizationRegistry.Configure(_previousProvider);
        }
    }

    private sealed class DictionaryLocalizationProvider : ILocalizationProvider
    {
        private readonly IDictionary<string, string> _values;

        public DictionaryLocalizationProvider(IDictionary<string, string> values)
        {
            _values = values;
        }

        public string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (_values.TryGetValue(key, out var value))
            {
                return value;
            }

            return key;
        }

        public string Format(string key, params object[] arguments)
        {
            var format = Get(key);
            return string.Format(CultureInfo.CurrentCulture, format, arguments);
        }
    }
}
