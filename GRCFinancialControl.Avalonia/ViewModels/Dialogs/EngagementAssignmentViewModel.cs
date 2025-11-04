using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRC.Shared.UI.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public sealed partial class EngagementAssignmentViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;
        private readonly IManagerService _managerService;
        private readonly LoggingService _loggingService;

        private int? _papdId;
        private int? _managerId;

        [ObservableProperty]
        private ObservableCollection<EngagementAssignmentItem> _engagements = new();

        [ObservableProperty]
        private bool _isBusy;

        public EngagementAssignmentViewModel(
            IEngagementService engagementService,
            IPapdService papdService,
            IManagerService managerService,
            LoggingService loggingService,
            IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService;
            _papdService = papdService;
            _managerService = managerService;
            _loggingService = loggingService;
        }

        public void Initialize(int papdId)
        {
            _papdId = papdId;
        }

        public void Initialize(int managerId, bool isManager)
        {
            _managerId = managerId;
        }

        public override async Task LoadDataAsync()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                var allEngagements = await _engagementService.GetAllAsync();

                if (_papdId.HasValue)
                {
                    var papd = await _papdService.GetByIdAsync(_papdId.Value);
                    if (papd is not null)
                    {
                        var assignedEngagementIds = papd.EngagementPapds
                            .Select(ep => ep.EngagementId)
                            .ToHashSet();
                        Engagements = new ObservableCollection<EngagementAssignmentItem>(
                            allEngagements.Select(e => new EngagementAssignmentItem(e, assignedEngagementIds.Contains(e.Id))));
                    }
                }
                else if (_managerId.HasValue)
                {
                    var manager = await _managerService.GetByIdAsync(_managerId.Value);
                    if (manager is not null)
                    {
                        var assignedEngagementIds = manager.EngagementAssignments.Select(e => e.EngagementId).ToHashSet();
                        Engagements = new ObservableCollection<EngagementAssignmentItem>(
                            allEngagements.Select(e => new EngagementAssignmentItem(e, assignedEngagementIds.Contains(e.Id))));
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanAssign))]
        private async Task AssignAsync()
        {
            try
            {
                IsBusy = true;
                var selectedIds = Engagements.Where(e => e.IsSelected).Select(e => e.Engagement.Id).ToList();

                if (_papdId.HasValue)
                {
                    await _papdService.AssignEngagementsAsync(_papdId.Value, selectedIds);
                }
                else if (_managerId.HasValue)
                {
                    await _managerService.AssignEngagementsAsync(_managerId.Value, selectedIds);
                }

                Messenger.Send(new CloseDialogMessage(true));
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanAssign()
        {
            return Engagements.Any(e => e.IsSelected);
        }
    }

    public sealed partial class EngagementAssignmentItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public Engagement Engagement { get; }

        public EngagementAssignmentItem(Engagement engagement, bool isSelected)
        {
            Engagement = engagement;
            _isSelected = isSelected;
        }
    }
}
