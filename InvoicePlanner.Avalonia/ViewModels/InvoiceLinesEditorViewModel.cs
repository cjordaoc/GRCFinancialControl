using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class InvoiceLinesEditorViewModel : ViewModelBase
{
    private readonly PlanEditorViewModel _parentViewModel;
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _refreshCommand;

    [ObservableProperty]
    private string? _statusMessage;

    public InvoiceLinesEditorViewModel(PlanEditorViewModel parentViewModel)
    {
        _parentViewModel = parentViewModel;

        _parentViewModel.PropertyChanged += OnParentPropertyChanged;
        _parentViewModel.Items.CollectionChanged += OnItemsCollectionChanged;

        _saveCommand = new RelayCommand(Save, () => _parentViewModel.CanSavePlan);
        _refreshCommand = new RelayCommand(RefreshGrid);
        CloseCommand = new RelayCommand(Close);
        _saveCommand.NotifyCanExecuteChanged();

        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(TotalAmountDisplay));
        OnPropertyChanged(nameof(TotalPercentage));
        OnPropertyChanged(nameof(CurrencySymbol));
        OnPropertyChanged(nameof(HasCurrencySymbol));
        OnPropertyChanged(nameof(HasTotalsMismatch));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationMessage));

        // Trigger the UI to refresh and show the datagrid rows.
        OnPropertyChanged(nameof(Items));
    }

    public PlanEditorViewModel Editor => _parentViewModel;

    public ObservableCollection<InvoicePlanLineViewModel> Items => _parentViewModel.Items;
    public decimal TotalPercentage => _parentViewModel.TotalPercentage;
    public decimal TotalAmount => _parentViewModel.TotalAmount;
    public string TotalAmountDisplay => _parentViewModel.TotalAmountDisplay;
    public string CurrencySymbol => _parentViewModel.CurrencySymbol;
    public bool HasCurrencySymbol => _parentViewModel.HasCurrencySymbol;
    public bool HasTotalsMismatch => _parentViewModel.HasTotalsMismatch;
    public bool CanSavePlan => _parentViewModel.CanSavePlan;
    public string? ValidationMessage => _parentViewModel.ValidationMessage;
    public bool HasValidationMessage => _parentViewModel.HasValidationMessage;
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public IRelayCommand SaveCommand => _saveCommand;
    public IRelayCommand CloseCommand { get; }
    public IRelayCommand RefreshCommand => _refreshCommand;

    private void Save()
    {
        _parentViewModel.SavePlanCommand.Execute(null);
        _saveCommand.NotifyCanExecuteChanged();
        StatusMessage = _parentViewModel.StatusMessage;
    }

    private void Close()
    {
        CloseDialog(false);
    }

    private void CloseDialog(bool result)
    {
        _parentViewModel.PropertyChanged -= OnParentPropertyChanged;
        _parentViewModel.Items.CollectionChanged -= OnItemsCollectionChanged;
        Messenger.Send(new CloseDialogMessage(result));
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Items));
    }

    private void RefreshGrid()
    {
        OnPropertyChanged(nameof(Items));
        Messenger.Send(new RefreshViewMessage(RefreshTargets.InvoiceLinesGrid));
    }

    private void OnParentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlanEditorViewModel.TotalAmount):
                OnPropertyChanged(nameof(TotalAmount));
                OnPropertyChanged(nameof(TotalAmountDisplay));
                break;
            case nameof(PlanEditorViewModel.TotalAmountDisplay):
                OnPropertyChanged(nameof(TotalAmountDisplay));
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
            case nameof(PlanEditorViewModel.ValidationMessage):
                OnPropertyChanged(nameof(ValidationMessage));
                OnPropertyChanged(nameof(HasValidationMessage));
                break;
            case nameof(PlanEditorViewModel.StatusMessage):
                // The parent's status message is copied to the local property
                // after a save. When it changes again, we clear the local message.
                StatusMessage = null;
                break;
        }
    }
}
