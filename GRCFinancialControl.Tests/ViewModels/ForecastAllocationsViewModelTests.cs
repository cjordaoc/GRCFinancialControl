using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Xunit;

namespace GRCFinancialControl.Tests.ViewModels
{
    public sealed class ForecastAllocationsViewModelTests
    {
        [Fact]
        public async Task RefreshPreservesSelectedEngagementWhenStillPresent()
        {
            var service = new StubForecastService(new[]
            {
                CreateRows(1, 2),
                CreateRows(1, 2)
            });
            var export = new StubExportService();
            var logging = new StubLoggingService();
            var dialog = new StubDialogService();
            var messenger = new StrongReferenceMessenger();

            var viewModel = new ForecastAllocationsViewModel(service, export, logging, dialog, messenger);
            await viewModel.LoadDataAsync();

            viewModel.SelectedEngagement = viewModel.Engagements.First(e => e.EngagementId == 2);

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.Equal(2, viewModel.SelectedEngagement?.EngagementId);
        }

        [Fact]
        public async Task RefreshFallsBackToFirstEngagementWhenSelectionIsMissing()
        {
            var service = new StubForecastService(new[]
            {
                CreateRows(1, 2),
                CreateRows(1)
            });
            var export = new StubExportService();
            var logging = new StubLoggingService();
            var dialog = new StubDialogService();
            var messenger = new StrongReferenceMessenger();

            var viewModel = new ForecastAllocationsViewModel(service, export, logging, dialog, messenger);
            await viewModel.LoadDataAsync();

            viewModel.SelectedEngagement = viewModel.Engagements.First(e => e.EngagementId == 2);

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.Equal(1, viewModel.SelectedEngagement?.EngagementId);
        }

        [Fact]
        public async Task MessengerRefreshRequestTriggersReload()
        {
            var service = new StubForecastService(new[]
            {
                CreateRows(1),
                CreateRows(1)
            });
            var export = new StubExportService();
            var logging = new StubLoggingService();
            var dialog = new StubDialogService();
            var messenger = new StrongReferenceMessenger();

            var viewModel = new ForecastAllocationsViewModel(service, export, logging, dialog, messenger);
            await viewModel.LoadDataAsync();

            messenger.Send(new ForecastOperationRequestMessage(ForecastOperationRequestType.Refresh));

            Assert.True(SpinWait.SpinUntil(() => service.LoadCallCount >= 2, TimeSpan.FromMilliseconds(200)));
        }

        [Fact]
        public async Task MessengerTemplateRequestExportsData()
        {
            var rows = CreateRows(1, 2);
            var service = new StubForecastService(new[] { rows });
            var export = new StubExportService();
            var logging = new StubLoggingService();
            var dialog = new StubDialogService();
            var messenger = new StrongReferenceMessenger();

            var viewModel = new ForecastAllocationsViewModel(service, export, logging, dialog, messenger);
            await viewModel.LoadDataAsync();

            messenger.Send(new ForecastOperationRequestMessage(ForecastOperationRequestType.GenerateTemplateRetain));

            Assert.True(SpinWait.SpinUntil(() => export.CallCount > 0, TimeSpan.FromMilliseconds(200)));
            Assert.Equal("ForecastRetainTemplate", export.LastEntityName);
        }

        [Fact]
        public async Task MessengerPendingRequestExportsOnlyNonOkRows()
        {
            var rows = new List<ForecastAllocationRow>
            {
                new ForecastAllocationRow(1, "ENG-001", "Engagement 1", 1, "FY24", "Senior", 100m, 40m, 30m, 30m, 60m, "OK"),
                new ForecastAllocationRow(1, "ENG-001", "Engagement 1", 1, "FY24", "Associate", 100m, 20m, 40m, 40m, 80m, "Risco")
            };
            var service = new StubForecastService(new[] { rows });
            var export = new StubExportService();
            var logging = new StubLoggingService();
            var dialog = new StubDialogService();
            var messenger = new StrongReferenceMessenger();

            var viewModel = new ForecastAllocationsViewModel(service, export, logging, dialog, messenger);
            await viewModel.LoadDataAsync();

            messenger.Send(new ForecastOperationRequestMessage(ForecastOperationRequestType.ExportPending));

            Assert.True(SpinWait.SpinUntil(() => export.CallCount > 0, TimeSpan.FromMilliseconds(200)));
            Assert.Single(export.LastItems!);
            var pending = Assert.IsType<ForecastAllocationRow>(export.LastItems![0]);
            Assert.Equal("Risco", pending.Status);
        }

        private static IReadOnlyList<ForecastAllocationRow> CreateRows(params int[] engagementIds)
        {
            var rows = new List<ForecastAllocationRow>();
            foreach (var id in engagementIds)
            {
                rows.Add(new ForecastAllocationRow(
                    id,
                    $"ENG-{id:D3}",
                    $"Engagement {id}",
                    1,
                    "FY24",
                    "Senior",
                    120m,
                    40m,
                    60m,
                    20m,
                    80m,
                    "OK"));
            }

            return rows;
        }

        private sealed class StubForecastService : IStaffAllocationForecastService
        {
            private readonly Queue<IReadOnlyList<ForecastAllocationRow>> _results;

            public StubForecastService(IEnumerable<IReadOnlyList<ForecastAllocationRow>> results)
            {
                _results = new Queue<IReadOnlyList<ForecastAllocationRow>>(results);
            }

            public int LoadCallCount { get; private set; }

            public Task<IReadOnlyList<ForecastAllocationRow>> GetCurrentForecastAsync()
            {
                LoadCallCount++;
                if (_results.Count > 0)
                {
                    return Task.FromResult(_results.Dequeue());
                }

                return Task.FromResult<IReadOnlyList<ForecastAllocationRow>>(Array.Empty<ForecastAllocationRow>());
            }

            public Task SaveEngagementForecastAsync(int engagementId, IReadOnlyList<EngagementForecastUpdateEntry> entries)
            {
                return Task.CompletedTask;
            }

            public Task<StaffAllocationForecastUpdateResult> UpdateForecastAsync(IReadOnlyList<StaffAllocationTemporaryRecord> records)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class StubExportService : IExportService
        {
            public int CallCount { get; private set; }
            public string? LastEntityName { get; private set; }
            public List<object>? LastItems { get; private set; }

            public Task ExportToExcelAsync<T>(IEnumerable<T> data, string entityName)
            {
                CallCount++;
                LastEntityName = entityName;
                LastItems = data.Cast<object>().ToList();
                return Task.CompletedTask;
            }
        }

        private sealed class StubLoggingService : ILoggingService
        {
            public event Action<string>? OnLogMessage;

            public void LogError(string message, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0)
            {
                OnLogMessage?.Invoke(message);
            }

            public void LogInfo(string message, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0)
            {
                OnLogMessage?.Invoke(message);
            }

            public void LogWarning(string message, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0)
            {
                OnLogMessage?.Invoke(message);
            }
        }

        private sealed class StubDialogService : IDialogService
        {
            public Task<bool> ShowConfirmationAsync(string title, string message) => Task.FromResult(true);

            public Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null) => Task.FromResult(true);
        }
    }
}
