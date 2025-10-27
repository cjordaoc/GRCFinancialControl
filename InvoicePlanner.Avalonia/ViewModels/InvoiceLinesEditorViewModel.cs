using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Messages;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class InvoiceLinesEditorViewModel : ViewModelBase
{
    private readonly PlanEditorViewModel _parentViewModel;
    private readonly RelayCommand _saveCommand;

    public InvoiceLinesEditorViewModel(PlanEditorViewModel parentViewModel)
    {
        _parentViewModel = parentViewModel;

        _parentViewModel.PropertyChanged += OnParentPropertyChanged;

        _saveCommand = new RelayCommand(Save, () => _parentViewModel.CanSavePlan);
        CloseCommand = new RelayCommand(Close);
        _saveCommand.NotifyCanExecuteChanged();
    }

    public PlanEditorViewModel Editor => _parentViewModel;

    public ObservableCollection<InvoicePlanLineViewModel> Items => _parentViewModel.Items;
    public decimal TotalPercentage => _parentViewModel.TotalPercentage;
    public decimal TotalAmount => _parentViewModel.TotalAmount;
    public string CurrencySymbol => _parentViewModel.CurrencySymbol;
    public bool HasCurrencySymbol => _parentViewModel.HasCurrencySymbol;
    public bool HasTotalsMismatch => _parentViewModel.HasTotalsMismatch;
    public bool CanSavePlan => _parentViewModel.CanSavePlan;

    public IRelayCommand SaveCommand => _saveCommand;
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
            case nameof(PlanEditorViewModel.CurrencySymbol):
                OnPropertyChanged(nameof(CurrencySymbol));
                OnPropertyChanged(nameof(HasCurrencySymbol));
                break;
            case nameof(PlanEditorViewModel.HasTotalsMismatch):
                OnPropertyChanged(nameof(HasTotalsMismatch));
                OnPropertyChanged(nameof(CanSavePlan));
                _saveCommand.NotifyCanExecuteChanged();
                break;
            case nameof(PlanEditorViewModel.CanSavePlan):
                OnPropertyChanged(nameof(CanSavePlan));
                _saveCommand.NotifyCanExecuteChanged();
                break;
        }
    }
}
