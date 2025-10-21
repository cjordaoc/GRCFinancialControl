using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Xunit;

namespace GRCFinancialControl.Tests.ViewModels;

public sealed class HoursAllocationsViewModelTests
{
    [Fact]
    public async Task LoadDataAsync_PopulatesRowsAndTotals()
    {
        var engagementService = new FakeEngagementService();
        var hoursService = new FakeHoursAllocationService();
        var loggingService = new FakeLoggingService();
        var messenger = new StrongReferenceMessenger();

        hoursService.Snapshot = CreateSnapshot();

        var viewModel = new HoursAllocationsViewModel(
            engagementService,
            hoursService,
            loggingService,
            messenger);

        await viewModel.LoadDataAsync();

        Assert.NotNull(viewModel.SelectedEngagement);
        Assert.Single(viewModel.Engagements);
        Assert.Equal(1, viewModel.Rows.Count);
        Assert.Equal("Manager", viewModel.Rows[0].RankName);
        Assert.Equal(70m, viewModel.TotalBudgetHours);
        Assert.Equal(50m, viewModel.ActualHours);
        Assert.Equal(30m, viewModel.ToBeConsumedHours);

        var firstCell = viewModel.Rows[0].Cells[0];
        Assert.Equal(40m, firstCell.BudgetHours);
        Assert.Equal(20m, firstCell.ConsumedHours);
        Assert.False(firstCell.IsLocked);

        var secondCell = viewModel.Rows[0].Cells[1];
        Assert.True(secondCell.IsLocked);
    }

    [Fact]
    public async Task SaveCommand_PersistsChangesAndResetsDirtyState()
    {
        var engagementService = new FakeEngagementService();
        var hoursService = new FakeHoursAllocationService();
        var loggingService = new FakeLoggingService();
        var messenger = new StrongReferenceMessenger();

        hoursService.Snapshot = CreateSnapshot();
        hoursService.SaveSnapshot = CreateSnapshot(25m);

        var viewModel = new HoursAllocationsViewModel(
            engagementService,
            hoursService,
            loggingService,
            messenger);

        await viewModel.LoadDataAsync();

        var cell = viewModel.Rows[0].Cells[0];
        cell.ConsumedHours = 25m;

        Assert.True(viewModel.HasChanges);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasChanges);
        Assert.Equal(25m, viewModel.Rows[0].Cells[0].ConsumedHours);
        Assert.Contains(hoursService.LastUpdates, update => update.BudgetId == 1 && update.ConsumedHours == 25m);
        Assert.Equal("Changes saved successfully.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task EditingConsumedHoursRoundsAndUpdatesRemaining()
    {
        var engagementService = new FakeEngagementService();
        var hoursService = new FakeHoursAllocationService();
        var loggingService = new FakeLoggingService();
        var messenger = new StrongReferenceMessenger();

        hoursService.Snapshot = CreateSnapshot();

        var viewModel = new HoursAllocationsViewModel(
            engagementService,
            hoursService,
            loggingService,
            messenger);

        await viewModel.LoadDataAsync();

        var cell = viewModel.Rows[0].Cells[0];
        cell.ConsumedHours = 25.678m;

        Assert.Equal(25.68m, cell.ConsumedHours);
        Assert.Equal(14.32m, cell.RemainingHours);
        Assert.True(viewModel.HasChanges);
    }

    [Fact]
    public async Task AddRankCommand_AddsRankAndClearsInput()
    {
        var engagementService = new FakeEngagementService();
        var hoursService = new FakeHoursAllocationService();
        var loggingService = new FakeLoggingService();
        var messenger = new StrongReferenceMessenger();

        hoursService.Snapshot = CreateSnapshot();
        hoursService.AddRankSnapshot = CreateSnapshot(additionalRank: true);

        var viewModel = new HoursAllocationsViewModel(
            engagementService,
            hoursService,
            loggingService,
            messenger)
        {
            NewRankName = "Senior"
        };

        await viewModel.LoadDataAsync();
        await viewModel.AddRankCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.NewRankName);
        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Contains(viewModel.Rows, r => r.RankName == "Senior");
        Assert.Equal("Rank 'Senior' created for open fiscal years.", viewModel.StatusMessage);
    }

    private static HoursAllocationSnapshot CreateSnapshot(decimal consumed = 20m, bool additionalRank = false)
    {
        var fiscalYears = new List<FiscalYearAllocationInfo>
        {
            new(1, "FY2024", false),
            new(2, "FY2023", true)
        };

        var rows = new List<HoursAllocationRowSnapshot>
        {
            new("Manager", new List<HoursAllocationCellSnapshot>
            {
                new(1, 1, 40m, consumed, 40m - consumed, false),
                new(2, 2, 30m, 15m, 15m, true)
            })
        };

        if (additionalRank)
        {
            rows.Add(new HoursAllocationRowSnapshot("Senior", new List<HoursAllocationCellSnapshot>
            {
                new(null, 1, 0m, 0m, 0m, false)
            }));
        }

        return new HoursAllocationSnapshot(
            1,
            "E-100",
            "Test Engagement",
            70m,
            50m,
            30m,
            fiscalYears,
            rows);
    }

    private sealed class FakeEngagementService : IEngagementService
    {
        public Task<List<Engagement>> GetAllAsync()
        {
            var engagements = new List<Engagement>
            {
                new()
                {
                    Id = 1,
                    EngagementId = "E-100",
                    Description = "Test Engagement"
                }
            };

            return Task.FromResult(engagements);
        }

        public Task<Engagement?> GetByIdAsync(int id) => Task.FromResult<Engagement?>(null);
        public Task<Papd?> GetPapdForDateAsync(int engagementId, System.DateTime date) => Task.FromResult<Papd?>(null);
        public Task AddAsync(Engagement engagement) => Task.CompletedTask;
        public Task UpdateAsync(Engagement engagement) => Task.CompletedTask;
        public Task DeleteAsync(int id) => Task.CompletedTask;
        public Task DeleteDataAsync(int engagementId) => Task.CompletedTask;
    }

    private sealed class FakeHoursAllocationService : IHoursAllocationService
    {
        public HoursAllocationSnapshot Snapshot { get; set; } = null!;
        public HoursAllocationSnapshot? SaveSnapshot { get; set; }
        public HoursAllocationSnapshot? AddRankSnapshot { get; set; }
        public List<HoursAllocationCellUpdate> LastUpdates { get; } = new();

        public Task<HoursAllocationSnapshot> GetAllocationAsync(int engagementId)
            => Task.FromResult(Snapshot);

        public Task<HoursAllocationSnapshot> SaveAsync(int engagementId, IEnumerable<HoursAllocationCellUpdate> updates)
        {
            LastUpdates.Clear();
            LastUpdates.AddRange(updates);

            if (SaveSnapshot is not null)
            {
                Snapshot = SaveSnapshot;
            }

            return Task.FromResult(Snapshot);
        }

        public Task<HoursAllocationSnapshot> AddRankAsync(int engagementId, string rankName)
        {
            if (AddRankSnapshot is not null)
            {
                Snapshot = AddRankSnapshot;
            }

            return Task.FromResult(Snapshot);
        }

        public Task DeleteRankAsync(int engagementId, string rankName) => Task.CompletedTask;
    }

    private sealed class FakeLoggingService : ILoggingService
    {
        public event Action<string>? OnLogMessage;

        public void LogInfo(string message, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0)
            => OnLogMessage?.Invoke(message);

        public void LogWarning(string message, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0)
            => OnLogMessage?.Invoke(message);

        public void LogError(string message, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0)
            => OnLogMessage?.Invoke(message);
    }
}
