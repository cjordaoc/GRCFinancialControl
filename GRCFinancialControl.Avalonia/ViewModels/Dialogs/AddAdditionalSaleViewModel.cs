using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class AddAdditionalSaleViewModel : ViewModelBase
    {
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string? _opportunityId;

        [ObservableProperty]
        private decimal _value;

        [ObservableProperty]
        private bool _hasChanges;

        public string Title => "Add Additional Sale";

        public bool CanSave => !string.IsNullOrWhiteSpace(Description) && Value > 0;

        public EngagementAdditionalSale? Result { get; private set; }

        public AddAdditionalSaleViewModel(IMessenger messenger)
            : base(messenger)
        {
            _messenger = messenger;
        }

        [RelayCommand]
        private void Save()
        {
            if (!CanSave)
            {
                return;
            }

            Result = new EngagementAdditionalSale
            {
                Description = Description.Trim(),
                OpportunityId = string.IsNullOrWhiteSpace(OpportunityId) ? null : OpportunityId.Trim(),
                Value = Value
            };

            _messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Cancel()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }

        partial void OnDescriptionChanged(string value)
        {
            HasChanges = true;
            OnPropertyChanged(nameof(CanSave));
        }

        partial void OnValueChanged(decimal value)
        {
            HasChanges = true;
            OnPropertyChanged(nameof(CanSave));
        }

        partial void OnOpportunityIdChanged(string? value)
        {
            HasChanges = true;
        }
    }
}
