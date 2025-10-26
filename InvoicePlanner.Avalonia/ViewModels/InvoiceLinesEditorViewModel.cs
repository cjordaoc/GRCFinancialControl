using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        _parentViewModel.PropertyChanged += OnParentPropertyChanged;

        SaveCommand = new RelayCommand(Save);
        CloseCommand = new RelayCommand(Close);
    }

    public PlanEditorViewModel Editor => _parentViewModel;

    public ObservableCollection<InvoicePlanLineViewModel> Items => _parentViewModel.Items;
    public decimal TotalPercentage => _parentViewModel.TotalPercentage;
    public decimal TotalAmount => _parentViewModel.TotalAmount;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CloseCommand { get; }

    private void Save()
    {
        _parentViewModel.SavePlanCommand.Execute(null);
        CloseDialog(true);
    }

    private void Close()
    {
        CloseDialog(false);
    }

    private void CloseDialog(bool result)
    {
        _parentViewModel.PropertyChanged -= OnParentPropertyChanged;
        Messenger.Send(new CloseDialogMessage(result));
    }

    private void OnParentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlanEditorViewModel.TotalAmount):
                OnPropertyChanged(nameof(TotalAmount));
                break;
            case nameof(PlanEditorViewModel.TotalPercentage):
                OnPropertyChanged(nameof(TotalPercentage));
                break;
            case nameof(PlanEditorViewModel.Items):
                OnPropertyChanged(nameof(Items));
                break;
        }
    }
}
