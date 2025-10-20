using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace GRCFinancialControl.Avalonia.Tests.ViewModels;

public class DialogInvocationTests
{
    [Fact]
    public async Task ManagersViewModel_AddCommand_ShowsEditorDialog()
    {
        var dialogService = CreateDialogService();
        var messenger = new StrongReferenceMessenger();
        var managerService = Substitute.For<IManagerService>();
        var viewModel = new ManagersViewModel(managerService, dialogService, messenger);

        await viewModel.AddCommand.ExecuteAsync(null);

        await dialogService.Received(1)
            .ShowDialogAsync(Arg.Is<ViewModelBase>(vm => vm is ManagerEditorViewModel), Arg.Any<string?>());
    }

    [Fact]
    public async Task ManagersViewModel_EditCommand_ShowsEditorDialog()
    {
        var dialogService = CreateDialogService();
        var messenger = new StrongReferenceMessenger();
        var managerService = Substitute.For<IManagerService>();
        var viewModel = new ManagersViewModel(managerService, dialogService, messenger);
        var manager = new Manager { Id = 42, Name = "Existing" };

        await viewModel.EditCommand.ExecuteAsync(manager);

        await dialogService.Received(1)
            .ShowDialogAsync(Arg.Is<ViewModelBase>(vm => vm is ManagerEditorViewModel), Arg.Any<string?>());
    }

    [Fact]
    public async Task ClosingPeriodsViewModel_AddCommand_ShowsEditorDialog_WhenUnlockedFiscalYearExists()
    {
        var dialogService = CreateDialogService();
        var messenger = new StrongReferenceMessenger();
        var closingPeriods = Substitute.For<IClosingPeriodService>();
        var fiscalYears = Substitute.For<IFiscalYearService>();
        var viewModel = new ClosingPeriodsViewModel(
            closingPeriods,
            fiscalYears,
            dialogService,
            messenger)
        {
            FiscalYears = new ObservableCollection<FiscalYear>
            {
                new()
                {
                    Id = 7,
                    Name = "FY",
                    IsLocked = false,
                    StartDate = new DateTime(2024, 1, 1),
                    EndDate = new DateTime(2024, 12, 31)
                }
            }
        };

        Assert.True(viewModel.AddCommand.CanExecute(null));

        await viewModel.AddCommand.ExecuteAsync(null);

        await dialogService.Received(1)
            .ShowDialogAsync(Arg.Is<ViewModelBase>(vm => vm is ClosingPeriodEditorViewModel), Arg.Any<string?>());
    }

    [Fact]
    public async Task ClosingPeriodsViewModel_EditCommand_ShowsEditorDialog_ForUnlockedPeriod()
    {
        var dialogService = CreateDialogService();
        var messenger = new StrongReferenceMessenger();
        var closingPeriods = Substitute.For<IClosingPeriodService>();
        var fiscalYears = Substitute.For<IFiscalYearService>();
        var fiscalYear = new FiscalYear
        {
            Id = 3,
            Name = "Unlocked FY",
            IsLocked = false,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31)
        };

        var viewModel = new ClosingPeriodsViewModel(
            closingPeriods,
            fiscalYears,
            dialogService,
            messenger)
        {
            FiscalYears = new ObservableCollection<FiscalYear> { fiscalYear }
        };

        var period = new ClosingPeriod
        {
            Id = 1,
            Name = "P1",
            FiscalYearId = fiscalYear.Id,
            FiscalYear = fiscalYear,
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 31)
        };

        Assert.True(viewModel.EditCommand.CanExecute(period));

        await viewModel.EditCommand.ExecuteAsync(period);

        await dialogService.Received(1)
            .ShowDialogAsync(Arg.Is<ViewModelBase>(vm => vm is ClosingPeriodEditorViewModel), Arg.Any<string?>());
    }

    [Fact]
    public async Task EngagementsViewModel_AddCommand_ShowsEditorDialog()
    {
        var dialogService = CreateDialogService();
        var messenger = new StrongReferenceMessenger();
        var engagements = Substitute.For<IEngagementService>();
        var customers = Substitute.For<ICustomerService>();
        var closingPeriods = Substitute.For<IClosingPeriodService>();
        var viewModel = new EngagementsViewModel(engagements, customers, closingPeriods, dialogService, messenger);

        await viewModel.AddCommand.ExecuteAsync(null);

        await dialogService.Received(1)
            .ShowDialogAsync(Arg.Is<ViewModelBase>(vm => vm is EngagementEditorViewModel), Arg.Any<string?>());
    }

    [Fact]
    public async Task EngagementsViewModel_EditCommand_ShowsEditorDialog_WhenEngagementExists()
    {
        var dialogService = CreateDialogService();
        var messenger = new StrongReferenceMessenger();
        var engagements = Substitute.For<IEngagementService>();
        var customers = Substitute.For<ICustomerService>();
        var closingPeriods = Substitute.For<IClosingPeriodService>();
        var viewModel = new EngagementsViewModel(engagements, customers, closingPeriods, dialogService, messenger);
        var engagement = new Engagement { Id = 9, EngagementId = "E-9", Description = "Summary" };
        engagements.GetByIdAsync(engagement.Id).Returns(Task.FromResult<Engagement?>(engagement));

        await viewModel.EditCommand.ExecuteAsync(engagement);

        await dialogService.Received(1)
            .ShowDialogAsync(Arg.Is<ViewModelBase>(vm => vm is EngagementEditorViewModel), Arg.Any<string?>());
    }

    [Fact]
    public async Task CustomersViewModel_AddCommand_ShowsEditorDialog()
    {
        var dialogService = CreateDialogService();
        var messenger = new StrongReferenceMessenger();
        var customers = Substitute.For<ICustomerService>();
        var viewModel = new CustomersViewModel(customers, dialogService, messenger);

        await viewModel.AddCommand.ExecuteAsync(null);

        await dialogService.Received(1)
            .ShowDialogAsync(Arg.Is<ViewModelBase>(vm => vm is CustomerEditorViewModel), Arg.Any<string?>());
    }

    [Fact]
    public async Task CustomersViewModel_EditCommand_ShowsEditorDialog_WhenCustomerProvided()
    {
        var dialogService = CreateDialogService();
        var messenger = new StrongReferenceMessenger();
        var customers = Substitute.For<ICustomerService>();
        var viewModel = new CustomersViewModel(customers, dialogService, messenger);
        var customer = new Customer { Id = 5, Name = "Client" };

        await viewModel.EditCommand.ExecuteAsync(customer);

        await dialogService.Received(1)
            .ShowDialogAsync(Arg.Is<ViewModelBase>(vm => vm is CustomerEditorViewModel), Arg.Any<string?>());
    }

    private static IDialogService CreateDialogService()
    {
        var dialogService = Substitute.For<IDialogService>();
        dialogService
            .ShowDialogAsync(Arg.Any<ViewModelBase>(), Arg.Any<string?>())
            .Returns(Task.FromResult(true));
        dialogService
            .ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        return dialogService;
    }
}
