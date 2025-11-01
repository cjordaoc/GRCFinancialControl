using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using App.Presentation.Localization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class HomeViewModel : ViewModelBase
    {
        private readonly IFiscalYearService _fiscalYearService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly ISettingsService _settingsService;
        private readonly LoggingService _loggingService;

        private List<ClosingPeriod> _allClosingPeriods = new();
        private bool _isInitializing;
        private bool _readmeLoaded;

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        [ObservableProperty]
        private ObservableCollection<ClosingPeriod> _closingPeriods = new();

        [ObservableProperty]
        private FiscalYear? _selectedFiscalYear;

        [ObservableProperty]
        private ClosingPeriod? _selectedClosingPeriod;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private bool _isError;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _readmeContent;

        public HomeViewModel(
            IFiscalYearService fiscalYearService,
            IClosingPeriodService closingPeriodService,
            ISettingsService settingsService,
            LoggingService loggingService,
            IMessenger messenger)
            : base(messenger)
        {
            _fiscalYearService = fiscalYearService ?? throw new ArgumentNullException(nameof(fiscalYearService));
            _closingPeriodService = closingPeriodService ?? throw new ArgumentNullException(nameof(closingPeriodService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

        public bool HasFiscalYears => FiscalYears.Count > 0;

        public bool HasClosingPeriods => ClosingPeriods.Count > 0;

        [RelayCommand(CanExecute = nameof(CanConfirmSelection))]
        private async Task ConfirmSelectionAsync()
        {
            if (SelectedFiscalYear is null || SelectedClosingPeriod is null)
            {
                return;
            }

            try
            {
                IsBusy = true;
                IsError = false;
                StatusMessage = null;

                await _settingsService.SetDefaultFiscalYearIdAsync(SelectedFiscalYear.Id).ConfigureAwait(false);
                await _settingsService.SetDefaultClosingPeriodIdAsync(SelectedClosingPeriod.Id).ConfigureAwait(false);

                Messenger.Send(new ApplicationParametersChangedMessage(SelectedFiscalYear.Id, SelectedClosingPeriod.Id));

                StatusMessage = LocalizationRegistry.Get("Home.Status.Saved");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to persist application parameters: {ex.Message}");
                StatusMessage = LocalizationRegistry.Format("Home.Status.SaveError", ex.Message);
                IsError = true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanConfirmSelection()
        {
            return !IsBusy && SelectedFiscalYear is not null && SelectedClosingPeriod is not null;
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
                StatusMessage = null;
                IsError = false;

                var fiscalYears = await _fiscalYearService
                    .GetAllAsync()
                    .ConfigureAwait(false);

                var orderedFiscalYears = fiscalYears
                    .OrderBy(fy => fy.StartDate)
                    .ToList();

                var closingPeriods = await _closingPeriodService
                    .GetAllAsync()
                    .ConfigureAwait(false);

                _allClosingPeriods = closingPeriods
                    .OrderBy(period => period.PeriodStart)
                    .ToList();

                var defaultFiscalYearIdTask = _settingsService.GetDefaultFiscalYearIdAsync();
                var defaultClosingPeriodIdTask = _settingsService.GetDefaultClosingPeriodIdAsync();

                _isInitializing = true;

                FiscalYears = new ObservableCollection<FiscalYear>(orderedFiscalYears);

                var defaultFiscalYearId = await defaultFiscalYearIdTask.ConfigureAwait(false);
                var defaultClosingPeriodId = await defaultClosingPeriodIdTask.ConfigureAwait(false);

                SelectedFiscalYear = FiscalYears.FirstOrDefault(fy => fy.Id == defaultFiscalYearId)
                    ?? FiscalYears.FirstOrDefault();

                UpdateClosingPeriodsForSelectedFiscalYear(defaultClosingPeriodId);

                if (!HasFiscalYears)
                {
                    StatusMessage = LocalizationRegistry.Get("Home.Status.NoFiscalYears");
                    IsError = true;
                }
                else if (!HasClosingPeriods)
                {
                    StatusMessage = LocalizationRegistry.Get("Home.Status.NoClosingPeriods");
                    IsError = true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to load application parameters: {ex.Message}");
                StatusMessage = LocalizationRegistry.Format("Home.Status.LoadError", ex.Message);
                IsError = true;
            }
            finally
            {
                _isInitializing = false;
                IsBusy = false;
                NotifyCommandCanExecute(ConfirmSelectionCommand);
                await EnsureReadmeLoadedAsync().ConfigureAwait(false);
            }
        }

        private async Task EnsureReadmeLoadedAsync()
        {
            if (_readmeLoaded)
            {
                return;
            }

            _readmeLoaded = true;

            string? content = null;
            try
            {
                var assembly = typeof(HomeViewModel).Assembly;
                var resourceName = assembly
                    .GetManifestResourceNames()
                    .FirstOrDefault(name => name.EndsWith("README.md", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(resourceName))
                {
                    await using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream is not null)
                    {
                        using var reader = new StreamReader(stream);
                        content = await reader.ReadToEndAsync().ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                content = null;
            }

            var resolved = string.IsNullOrWhiteSpace(content)
                ? LocalizationRegistry.Get("Home.Markdown.LoadFailed")
                : content;

            if (Dispatcher.UIThread.CheckAccess())
            {
                ReadmeContent = resolved;
            }
            else
            {
                Dispatcher.UIThread.Post(() => ReadmeContent = resolved);
            }
        }

        private void UpdateClosingPeriodsForSelectedFiscalYear(int? preferredClosingPeriodId)
        {
            IEnumerable<ClosingPeriod> matchingPeriods = Array.Empty<ClosingPeriod>();

            if (SelectedFiscalYear is not null)
            {
                matchingPeriods = _allClosingPeriods
                    .Where(period => period.FiscalYearId == SelectedFiscalYear.Id)
                    .OrderBy(period => period.PeriodStart)
                    .ToList();
            }

            ClosingPeriods = new ObservableCollection<ClosingPeriod>(matchingPeriods);

            if (preferredClosingPeriodId.HasValue)
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault(cp => cp.Id == preferredClosingPeriodId.Value)
                    ?? ClosingPeriods.FirstOrDefault();
            }
            else
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault();
            }
        }

        partial void OnSelectedFiscalYearChanged(FiscalYear? value)
        {
            if (_isInitializing)
            {
                return;
            }

            UpdateClosingPeriodsForSelectedFiscalYear(null);

            if (value is null)
            {
                StatusMessage = LocalizationRegistry.Get("Home.Status.SelectFiscalYear");
                IsError = true;
            }
            else if (!HasClosingPeriods)
            {
                StatusMessage = LocalizationRegistry.Get("Home.Status.NoClosingPeriods");
                IsError = true;
            }
            else
            {
                StatusMessage = null;
                IsError = false;
            }

            NotifyCommandCanExecute(ConfirmSelectionCommand);
        }

        partial void OnSelectedClosingPeriodChanged(ClosingPeriod? value)
        {
            NotifyCommandCanExecute(ConfirmSelectionCommand);
        }

        partial void OnIsBusyChanged(bool value)
        {
            NotifyCommandCanExecute(ConfirmSelectionCommand);
        }

        partial void OnStatusMessageChanged(string? value)
        {
            OnPropertyChanged(nameof(HasStatusMessage));
        }

        partial void OnFiscalYearsChanging(ObservableCollection<FiscalYear> value)
        {
            if (_fiscalYears != null)
            {
                _fiscalYears.CollectionChanged -= OnFiscalYearsCollectionChanged;
            }
        }

        partial void OnFiscalYearsChanged(ObservableCollection<FiscalYear> value)
        {
            if (value != null)
            {
                value.CollectionChanged += OnFiscalYearsCollectionChanged;
            }

            OnPropertyChanged(nameof(HasFiscalYears));
            NotifyCommandCanExecute(ConfirmSelectionCommand);
        }

        private void OnFiscalYearsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasFiscalYears));
            NotifyCommandCanExecute(ConfirmSelectionCommand);
        }

        partial void OnClosingPeriodsChanging(ObservableCollection<ClosingPeriod> value)
        {
            if (_closingPeriods != null)
            {
                _closingPeriods.CollectionChanged -= OnClosingPeriodsCollectionChanged;
            }
        }

        partial void OnClosingPeriodsChanged(ObservableCollection<ClosingPeriod> value)
        {
            if (value != null)
            {
                value.CollectionChanged += OnClosingPeriodsCollectionChanged;
            }

            OnPropertyChanged(nameof(HasClosingPeriods));
            NotifyCommandCanExecute(ConfirmSelectionCommand);
        }

        private void OnClosingPeriodsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasClosingPeriods));
            NotifyCommandCanExecute(ConfirmSelectionCommand);
        }
    }
}
