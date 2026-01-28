using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;
using GRC.Shared.Core.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class ViewAdditionalSalesViewModel : ViewModelBase
    {
        private readonly int _engagementId;
        private readonly IEngagementManagementFacade _engagementFacade;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<EngagementAdditionalSale> _additionalSales = new();

        [ObservableProperty]
        private EngagementAdditionalSale? _selectedAdditionalSale;

        public string Title => "Additional Sales";

        public bool CanDelete => SelectedAdditionalSale is not null;

        public ViewAdditionalSalesViewModel(
            int engagementId,
            IEngagementManagementFacade engagementFacade,
            IMessenger messenger)
            : base(messenger)
        {
            _engagementId = engagementId;
            _engagementFacade = engagementFacade;
            _messenger = messenger;
        }

        public override async Task LoadDataAsync()
        {
            var engagement = await _engagementFacade.GetEngagementAsync(_engagementId);
            if (engagement is not null)
            {
                AdditionalSales = new ObservableCollection<EngagementAdditionalSale>(
                    engagement.AdditionalSales.OrderBy(s => s.Description));
            }
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete()
        {
            if (SelectedAdditionalSale is null)
            {
                return;
            }

            try
            {
                var engagement = await _engagementFacade.GetEngagementAsync(_engagementId);
                if (engagement is null)
                {
                    return;
                }

                var saleToRemove = engagement.AdditionalSales
                    .FirstOrDefault(s => s.Id == SelectedAdditionalSale.Id);
                
                if (saleToRemove is not null)
                {
                    engagement.AdditionalSales.Remove(saleToRemove);
                    await _engagementFacade.UpdateEngagementAsync(engagement);
                    
                    AdditionalSales.Remove(SelectedAdditionalSale);
                    SelectedAdditionalSale = null;
                    
                    ToastService.ShowSuccess("Additional sale deleted successfully");
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowError("Failed to delete additional sale", ex.Message);
            }
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }

        partial void OnSelectedAdditionalSaleChanged(EngagementAdditionalSale? value)
        {
            OnPropertyChanged(nameof(CanDelete));
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }
}
