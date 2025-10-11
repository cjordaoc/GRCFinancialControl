using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementEditorViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _engagementId = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _customerKey = string.Empty;

        [ObservableProperty]
        private decimal _openingMargin;

        [ObservableProperty]
        private decimal _openingValue;

        [ObservableProperty]
        private string _status = string.Empty;

        [ObservableProperty]
        private double _totalPlannedHours;

        [ObservableProperty]
        private ObservableCollection<EngagementPapd> _papdAssignments = new();

        public Engagement Engagement { get; }

        public EngagementEditorViewModel(Engagement engagement, IEngagementService engagementService, IPapdService papdService, IMessenger messenger)
        {
            Engagement = engagement;
            _engagementService = engagementService;
            _papdService = papdService;
            _messenger = messenger;

            EngagementId = engagement.EngagementId;
            Description = engagement.Description;
            CustomerKey = engagement.CustomerKey;
            OpeningMargin = engagement.OpeningMargin;
            OpeningValue = engagement.OpeningValue;
            Status = engagement.Status;
            TotalPlannedHours = engagement.TotalPlannedHours;
            PapdAssignments = new ObservableCollection<EngagementPapd>(engagement.EngagementPapds);
        }

        [RelayCommand]
        private async Task Save()
        {
            Engagement.EngagementId = EngagementId;
            Engagement.Description = Description;
            Engagement.CustomerKey = CustomerKey;
            Engagement.OpeningMargin = OpeningMargin;
            Engagement.OpeningValue = OpeningValue;
            Engagement.Status = Status;
            Engagement.TotalPlannedHours = TotalPlannedHours;
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
        private void Cancel()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }

        [RelayCommand]
        private void AddAssignment()
        {
            // In a real app, this would open a dialog to select a PAPD and a date.
            // For now, we'll just add a placeholder.
            PapdAssignments.Add(new EngagementPapd { Papd = new Papd { Name = "New PAPD" }, EffectiveDate = DateTime.Today });
        }

        [RelayCommand]
        private void RemoveAssignment()
        {
            // Logic to remove the selected assignment
        }
    }
}