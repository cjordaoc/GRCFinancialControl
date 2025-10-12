using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementEditorViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;
        private readonly ICustomerService _customerService;
        private readonly IDialogService _dialogService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _engagementId = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        [ObservableProperty]
        private decimal _openingMargin;

        [ObservableProperty]
        private decimal _openingValue;

        [ObservableProperty]
        private EngagementStatus _status;

        [ObservableProperty]
        private double _totalPlannedHours;

        [ObservableProperty]
        private decimal? _marginPctEtcp;

        [ObservableProperty]
        private int? _etcpAgeDays;

        [ObservableProperty]
        private DateTime? _latestEtcDate;

        public IEnumerable<EngagementStatus> StatusOptions => Enum.GetValues<EngagementStatus>();

        [ObservableProperty]
        private ObservableCollection<EngagementPapd> _papdAssignments = new();

        public Engagement Engagement { get; }

        public EngagementEditorViewModel(Engagement engagement, IEngagementService engagementService, IPapdService papdService, ICustomerService customerService, IDialogService dialogService, IMessenger messenger)
        {
            Engagement = engagement;
            _engagementService = engagementService;
            _papdService = papdService;
            _customerService = customerService;
            _dialogService = dialogService;
            _messenger = messenger;

            EngagementId = engagement.EngagementId;
            Description = engagement.Description;
            OpeningMargin = engagement.OpeningMargin;
            OpeningValue = engagement.OpeningValue;
            Status = engagement.Status;
            TotalPlannedHours = engagement.TotalPlannedHours;
            MarginPctEtcp = engagement.MarginPctEtcp;
            EtcpAgeDays = engagement.EtcpAgeDays;
            LatestEtcDate = engagement.LatestEtcDate;
            PapdAssignments = new ObservableCollection<EngagementPapd>(engagement.EngagementPapds);

            _ = LoadCustomersAsync();
        }

        private async Task LoadCustomersAsync()
        {
            Customers = new ObservableCollection<Customer>(await _customerService.GetAllAsync());
            SelectedCustomer = Customers.FirstOrDefault(c => Engagement.CustomerId.HasValue && c.Id == Engagement.CustomerId)
                               ?? Customers.FirstOrDefault(c => c.Name == Engagement.CustomerKey);
        }

        [RelayCommand]
        private async Task Save()
        {
            Engagement.EngagementId = EngagementId;
            Engagement.Description = Description;
            Engagement.CustomerKey = SelectedCustomer?.Name ?? string.Empty;
            Engagement.CustomerId = SelectedCustomer?.Id;
            Engagement.OpeningMargin = OpeningMargin;
            Engagement.OpeningValue = OpeningValue;
            Engagement.Status = Status;
            Engagement.TotalPlannedHours = TotalPlannedHours;
            Engagement.MarginPctEtcp = MarginPctEtcp;
            Engagement.EtcpAgeDays = EtcpAgeDays;
            Engagement.LatestEtcDate = LatestEtcDate;
            Engagement.EngagementPapds = PapdAssignments;

            if (Engagement.Id == 0)
            {
                await _engagementService.AddAsync(Engagement);
            }
            else
            {
                await _engagementService.UpdateAsync(Engagement);
            }

            _messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }

        [RelayCommand]
        [RelayCommand]
        private async Task OpenPapdAssignmentDialog()
        {
            var papdAssignmentViewModel = new EngagementPapdAssignmentViewModel(
                new ObservableCollection<EngagementPapd>(PapdAssignments),
                await _papdService.GetAllAsync()
            );

            var result = await _dialogService.ShowDialogAsync(papdAssignmentViewModel);

            if (result is ObservableCollection<EngagementPapd> updatedAssignments)
            {
                PapdAssignments = updatedAssignments;
            }
        }
    }
}