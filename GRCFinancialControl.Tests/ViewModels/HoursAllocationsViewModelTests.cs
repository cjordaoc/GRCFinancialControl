using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;
using Moq;
using Xunit;

namespace GRCFinancialControl.Tests.ViewModels;

public sealed class HoursAllocationsViewModelTests
{
    [Fact]
    public async Task LoadDataAsync_PopulatesRowsAndTotals()
    {
        var snapshot = CreateSnapshot();
        var engagements = CreateEngagements();

        var engagementMock = new Mock<IEngagementService>();
        engagementMock.Setup(service => service.GetAllAsync()).ReturnsAsync(engagements);

        var currentSnapshot = snapshot;
        var hoursMock = new Mock<IHoursAllocationService>();
        hoursMock.Setup(service => service.GetAllocationAsync(It.IsAny<int>()))
            .ReturnsAsync(() => currentSnapshot);
        hoursMock.Setup(service => service.SaveAsync(It.IsAny<int>(), It.IsAny<IEnumerable<HoursAllocationCellUpdate>>()))
            .ReturnsAsync((int _, IEnumerable<HoursAllocationCellUpdate> _) => currentSnapshot);
        hoursMock.Setup(service => service.AddRankAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(() => currentSnapshot);
        hoursMock.Setup(service => service.DeleteRankAsync(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var importMock = new Mock<IImportService>();
        importMock.Setup(service => service.UpdateStaffAllocationsAsync(It.IsAny<string>()))
            .ReturnsAsync("Staff allocation update summary.");

        var filePickerMock = new Mock<IFilePickerService>();
        filePickerMock.Setup(service => service.OpenFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>()))
            .ReturnsAsync((string?)null);

        var loggingMock = new Mock<ILoggingService>();

        var viewModel = CreateViewModel(
            engagementMock.Object,
            hoursMock.Object,
            importMock.Object,
            filePickerMock.Object,
            loggingMock.Object);

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
        var snapshot = CreateSnapshot();
        var saveSnapshot = CreateSnapshot(25m);
        var engagements = CreateEngagements();

        var engagementMock = new Mock<IEngagementService>();
        engagementMock.Setup(service => service.GetAllAsync()).ReturnsAsync(engagements);

        var currentSnapshot = snapshot;
        var lastUpdates = new List<HoursAllocationCellUpdate>();
        var hoursMock = new Mock<IHoursAllocationService>();
        hoursMock.Setup(service => service.GetAllocationAsync(It.IsAny<int>()))
            .ReturnsAsync(() => currentSnapshot);
        hoursMock.Setup(service => service.SaveAsync(It.IsAny<int>(), It.IsAny<IEnumerable<HoursAllocationCellUpdate>>()))
            .ReturnsAsync((int _, IEnumerable<HoursAllocationCellUpdate> updates) =>
            {
                lastUpdates.Clear();
                lastUpdates.AddRange(updates);
                currentSnapshot = saveSnapshot;
                return currentSnapshot;
            });
        hoursMock.Setup(service => service.AddRankAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(() => currentSnapshot);
        hoursMock.Setup(service => service.DeleteRankAsync(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var importMock = new Mock<IImportService>();
        importMock.Setup(service => service.UpdateStaffAllocationsAsync(It.IsAny<string>()))
            .ReturnsAsync("Staff allocation update summary.");

        var filePickerMock = new Mock<IFilePickerService>();
        filePickerMock.Setup(service => service.OpenFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>()))
            .ReturnsAsync((string?)null);

        var loggingMock = new Mock<ILoggingService>();

        var viewModel = CreateViewModel(
            engagementMock.Object,
            hoursMock.Object,
            importMock.Object,
            filePickerMock.Object,
            loggingMock.Object);

        await viewModel.LoadDataAsync();

        var cell = viewModel.Rows[0].Cells[0];
        cell.ConsumedHours = 25m;

        Assert.True(viewModel.HasChanges);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasChanges);
        Assert.Equal(25m, viewModel.Rows[0].Cells[0].ConsumedHours);
        Assert.Contains(lastUpdates, update => update.BudgetId == 1 && update.ConsumedHours == 25m);
        Assert.Equal("Changes saved successfully.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task EditingConsumedHoursRoundsAndUpdatesRemaining()
    {
        var snapshot = CreateSnapshot();
        var engagements = CreateEngagements();

        var engagementMock = new Mock<IEngagementService>();
        engagementMock.Setup(service => service.GetAllAsync()).ReturnsAsync(engagements);

        var currentSnapshot = snapshot;
        var hoursMock = new Mock<IHoursAllocationService>();
        hoursMock.Setup(service => service.GetAllocationAsync(It.IsAny<int>()))
            .ReturnsAsync(() => currentSnapshot);
        hoursMock.Setup(service => service.SaveAsync(It.IsAny<int>(), It.IsAny<IEnumerable<HoursAllocationCellUpdate>>()))
            .ReturnsAsync((int _, IEnumerable<HoursAllocationCellUpdate> _) => currentSnapshot);
        hoursMock.Setup(service => service.AddRankAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(() => currentSnapshot);
        hoursMock.Setup(service => service.DeleteRankAsync(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var importMock = new Mock<IImportService>();
        importMock.Setup(service => service.UpdateStaffAllocationsAsync(It.IsAny<string>()))
            .ReturnsAsync("Staff allocation update summary.");

        var filePickerMock = new Mock<IFilePickerService>();
        filePickerMock.Setup(service => service.OpenFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>()))
            .ReturnsAsync((string?)null);

        var loggingMock = new Mock<ILoggingService>();

        var viewModel = CreateViewModel(
            engagementMock.Object,
            hoursMock.Object,
            importMock.Object,
            filePickerMock.Object,
            loggingMock.Object);

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
        var snapshot = CreateSnapshot();
        var addRankSnapshot = CreateSnapshot(additionalRank: true);
        var engagements = CreateEngagements();

        var engagementMock = new Mock<IEngagementService>();
        engagementMock.Setup(service => service.GetAllAsync()).ReturnsAsync(engagements);

        var currentSnapshot = snapshot;
        var hoursMock = new Mock<IHoursAllocationService>();
        hoursMock.Setup(service => service.GetAllocationAsync(It.IsAny<int>()))
            .ReturnsAsync(() => currentSnapshot);
        hoursMock.Setup(service => service.SaveAsync(It.IsAny<int>(), It.IsAny<IEnumerable<HoursAllocationCellUpdate>>()))
            .ReturnsAsync((int _, IEnumerable<HoursAllocationCellUpdate> _) => currentSnapshot);
        hoursMock.Setup(service => service.AddRankAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((int _, string _) =>
            {
                currentSnapshot = addRankSnapshot;
                return currentSnapshot;
            });
        hoursMock.Setup(service => service.DeleteRankAsync(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var importMock = new Mock<IImportService>();
        importMock.Setup(service => service.UpdateStaffAllocationsAsync(It.IsAny<string>()))
            .ReturnsAsync("Staff allocation update summary.");

        var filePickerMock = new Mock<IFilePickerService>();
        filePickerMock.Setup(service => service.OpenFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>()))
            .ReturnsAsync((string?)null);

        var loggingMock = new Mock<ILoggingService>();

        var viewModel = CreateViewModel(
            engagementMock.Object,
            hoursMock.Object,
            importMock.Object,
            filePickerMock.Object,
            loggingMock.Object);
        viewModel.NewRankName = "Senior";

        await viewModel.LoadDataAsync();
        await viewModel.AddRankCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.NewRankName);
        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Contains(viewModel.Rows, row => row.RankName == "Senior");
        Assert.Equal("Rank 'Senior' created for open fiscal years.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task UpdateAllocationsCommand_UsesImportServiceAndUpdatesStatus()
    {
        var snapshot = CreateSnapshot();
        var engagements = CreateEngagements();

        var engagementMock = new Mock<IEngagementService>();
        engagementMock.Setup(service => service.GetAllAsync()).ReturnsAsync(engagements);

        var currentSnapshot = snapshot;
        var hoursMock = new Mock<IHoursAllocationService>();
        hoursMock.Setup(service => service.GetAllocationAsync(It.IsAny<int>()))
            .ReturnsAsync(() => currentSnapshot);
        hoursMock.Setup(service => service.SaveAsync(It.IsAny<int>(), It.IsAny<IEnumerable<HoursAllocationCellUpdate>>()))
            .ReturnsAsync((int _, IEnumerable<HoursAllocationCellUpdate> _) => currentSnapshot);
        hoursMock.Setup(service => service.AddRankAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(() => currentSnapshot);
        hoursMock.Setup(service => service.DeleteRankAsync(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var importMock = new Mock<IImportService>();
        string? receivedPath = null;
        const string summary = "Staff allocation update summary: Updated 1.";
        importMock.Setup(service => service.UpdateStaffAllocationsAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) =>
            {
                receivedPath = path;
                return summary;
            });

        var filePickerMock = new Mock<IFilePickerService>();
        filePickerMock.Setup(service => service.OpenFileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>()))
            .ReturnsAsync("alloc.xlsx");

        var loggingMock = new Mock<ILoggingService>();

        var viewModel = CreateViewModel(
            engagementMock.Object,
            hoursMock.Object,
            importMock.Object,
            filePickerMock.Object,
            loggingMock.Object);

        await viewModel.LoadDataAsync();
        await viewModel.UpdateAllocationsCommand.ExecuteAsync(null);

        Assert.Equal("alloc.xlsx", receivedPath);
        Assert.Equal(summary, viewModel.StatusMessage);
    }

    private static HoursAllocationsViewModel CreateViewModel(
        IEngagementService engagementService,
        IHoursAllocationService hoursAllocationService,
        IImportService importService,
        IFilePickerService filePickerService,
        ILoggingService loggingService)
    {
        return new HoursAllocationsViewModel(
            engagementService,
            hoursAllocationService,
            importService,
            filePickerService,
            loggingService,
            new StrongReferenceMessenger());
    }

    private static List<Engagement> CreateEngagements()
    {
        return new List<Engagement>
        {
            new()
            {
                Id = 1,
                EngagementId = "E-100",
                Description = "Test Engagement"
            }
        };
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
}
