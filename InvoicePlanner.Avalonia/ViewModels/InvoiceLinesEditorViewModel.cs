using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Messages;
using InvoicePlanner.Avalonia.Services;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Core.Validation;
using Microsoft.Extensions.Logging;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class InvoiceLinesEditorViewModel : ViewModelBase
{
    private readonly PlanEditorViewModel _parentViewModel;

    public InvoiceLinesEditorViewModel(PlanEditorViewModel parentViewModel)
    {
        _parentViewModel = parentViewModel;

        SaveCommand = new RelayCommand(Save);
        CloseCommand = new RelayCommand(() => Messenger.Send(new CloseDialogMessage(false)));
    }

    public ObservableCollection<InvoicePlanLineViewModel> Items => _parentViewModel.Items;
    public decimal TotalPercentage => _parentViewModel.TotalPercentage;
    public decimal TotalAmount => _parentViewModel.TotalAmount;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CloseCommand { get; }

    private void Save()
    {
        _parentViewModel.SavePlanCommand.Execute(null);
        Messenger.Send(new CloseDialogMessage(true));
    }
}
