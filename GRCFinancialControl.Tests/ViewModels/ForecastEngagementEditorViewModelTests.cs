using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Xunit;

namespace GRCFinancialControl.Tests.ViewModels
{
    public sealed class ForecastEngagementEditorViewModelTests
    {
        [Fact]
        public void HasChangesReturnsToFalseWhenEditsReverted()
        {
            var summary = CreateSummary();
            var rows = CreateRows();
            var service = new StubForecastService();
            var messenger = new StrongReferenceMessenger();

            var viewModel = new ForecastEngagementEditorViewModel(summary, rows, service, messenger);

            Assert.False(viewModel.HasChanges);
            Assert.False(viewModel.SaveCommand.CanExecute(null));

            var originalHours = viewModel.Rows[0].Cells[0].ForecastHours;
            viewModel.Rows[0].Cells[0].ForecastHours = originalHours + 2m;

            Assert.True(viewModel.HasChanges);
            Assert.True(viewModel.SaveCommand.CanExecute(null));

            viewModel.Rows[0].Cells[0].ForecastHours = originalHours;

            Assert.False(viewModel.HasChanges);
            Assert.False(viewModel.SaveCommand.CanExecute(null));

            viewModel.Rows[0].Rank = "Senior   ";

            Assert.False(viewModel.HasChanges);
        }

        [Fact]
        public async Task SaveCommandClearsChangesAndNotifiesRefreshAsync()
        {
            var summary = CreateSummary();
            var rows = CreateRows();
            var service = new StubForecastService();
            var messenger = new StrongReferenceMessenger();
            var refreshCount = 0;

            messenger.Register<RefreshDataMessage>(this, (_, _) => refreshCount++);

            var viewModel = new ForecastEngagementEditorViewModel(summary, rows, service, messenger);

            var cell = viewModel.Rows[0].Cells[0];
            var originalHours = cell.ForecastHours;
            cell.ForecastHours = originalHours + 0.555m;

            Assert.True(viewModel.HasChanges);
            Assert.True(viewModel.SaveCommand.CanExecute(null));

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.False(viewModel.HasChanges);
            Assert.Equal("Previsão salva com sucesso.", viewModel.StatusMessage);
            Assert.Equal(1, refreshCount);

            var save = Assert.Single(service.Saves);
            Assert.Equal(summary.EngagementId, save.engagementId);

            var entry = Assert.Single(save.entries);
            Assert.Equal("Senior", entry.Rank);
            Assert.Equal(decimal.Round(originalHours + 0.555m, 2), entry.ForecastHours);

            cell.ForecastHours = entry.ForecastHours + 1m;

            Assert.True(viewModel.HasChanges);
            Assert.Null(viewModel.StatusMessage);
            var expectedRemaining = summary.InitialHoursBudget - (summary.ActualHours + cell.ForecastHours);
            Assert.Equal(expectedRemaining, viewModel.RemainingHours);
        }

        [Fact]
        public void RemainingHoursRecalculateWhenForecastChanges()
        {
            var summary = CreateSummary();
            var rows = CreateRows();
            var service = new StubForecastService();
            var messenger = new StrongReferenceMessenger();

            var viewModel = new ForecastEngagementEditorViewModel(summary, rows, service, messenger);

            var originalRemaining = viewModel.RemainingHours;

            viewModel.Rows[0].Cells[0].ForecastHours += 5m;

            Assert.Equal(originalRemaining - 5m, viewModel.RemainingHours);
            Assert.Empty(service.Saves);
        }

        [Fact]
        public void RemoveCommandPreventsDeletingRowsWithActuals()
        {
            var summary = CreateSummary();
            var rows = CreateRows();
            var service = new StubForecastService();
            var messenger = new StrongReferenceMessenger();

            var viewModel = new ForecastEngagementEditorViewModel(summary, rows, service, messenger);
            var row = viewModel.Rows[0];

            Assert.False(row.CanDelete);
            Assert.False(viewModel.RemoveRowCommand.CanExecute(row));

            viewModel.RemoveRowCommand.Execute(row);

            Assert.Single(viewModel.Rows);
        }

        private static EngagementForecastSummary CreateSummary()
        {
            return new EngagementForecastSummary(
                EngagementId: 1,
                EngagementCode: "ENG-001",
                EngagementName: "Test Engagement",
                InitialHoursBudget: 120m,
                ActualHours: 20m,
                ForecastHours: 40m,
                RemainingHours: 60m,
                FiscalYearCount: 1,
                RankCount: 1,
                RiskCount: 0,
                OverrunCount: 0);
        }

        private static IReadOnlyList<ForecastAllocationRow> CreateRows()
        {
            return new List<ForecastAllocationRow>
            {
                new ForecastAllocationRow(
                    EngagementId: 1,
                    EngagementCode: "ENG-001",
                    EngagementName: "Test Engagement",
                    FiscalYearId: 1,
                    FiscalYearName: "FY24",
                    Rank: "Senior",
                    BudgetHours: 80m,
                    ActualsHours: 20m,
                    ForecastHours: 40m,
                    AvailableHours: 40m,
                    AvailableToActuals: 60m,
                    Status: "OK")
            };
        }

        private sealed class StubForecastService : IStaffAllocationForecastService
        {
            public List<(int engagementId, IReadOnlyList<EngagementForecastUpdateEntry> entries)> Saves { get; } = new();

            public Task<IReadOnlyList<ForecastAllocationRow>> GetCurrentForecastAsync()
            {
                return Task.FromResult<IReadOnlyList<ForecastAllocationRow>>(Array.Empty<ForecastAllocationRow>());
            }

            public Task SaveEngagementForecastAsync(int engagementId, IReadOnlyList<EngagementForecastUpdateEntry> entries)
            {
                Saves.Add((engagementId, entries));
                return Task.CompletedTask;
            }

            public Task<StaffAllocationForecastUpdateResult> UpdateForecastAsync(IReadOnlyList<StaffAllocationTemporaryRecord> records)
            {
                throw new NotImplementedException();
            }
        }
    }
}
