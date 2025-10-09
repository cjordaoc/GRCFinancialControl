using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Data;
using GRCFinancialControl.Core.Services;
using ReactiveUI;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class FiscalYearMaintenanceViewModel : ViewModelBase
    {
        private readonly FiscalYearService _service;
        private DimFiscalYear? _selectedFiscalYear;

        public ObservableCollection<DimFiscalYear> FiscalYears { get; } = new();

        public Interaction<FiscalYearEditorViewModel, (string, DateTime, DateTime)?> ShowDialog { get; }

        public DimFiscalYear? SelectedFiscalYear
        {
            get => _selectedFiscalYear;
            set => this.RaiseAndSetIfChanged(ref _selectedFiscalYear, value);
        }

        public ReactiveCommand<Unit, Unit> AddCommand { get; }
        public ReactiveCommand<Unit, Unit> EditCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
        public ReactiveCommand<Unit, Unit> ActivateCommand { get; }

        public FiscalYearMaintenanceViewModel(AppConfig config)
        {
            var context = DbContextFactory.CreateMySqlContext(config);
            _service = new FiscalYearService(context);

            ShowDialog = new Interaction<FiscalYearEditorViewModel, (string, DateTime, DateTime)?>();

            var canEdit = this.WhenAnyValue(x => x.SelectedFiscalYear, year => year != null);
            var canDelete = this.WhenAnyValue(x => x.SelectedFiscalYear, year => year != null && !year.IsActive);
            var canActivate = this.WhenAnyValue(x => x.SelectedFiscalYear, year => year != null);

            AddCommand = ReactiveCommand.CreateFromTask(Add);
            EditCommand = ReactiveCommand.CreateFromTask(Edit, canEdit);
            DeleteCommand = ReactiveCommand.Create(Delete, canDelete);
            ActivateCommand = ReactiveCommand.Create(Activate, canActivate);

            LoadFiscalYears();
        }

        private void LoadFiscalYears()
        {
            var selectedId = SelectedFiscalYear?.FiscalYearId;
            FiscalYears.Clear();
            var years = _service.GetAll();
            foreach (var year in years)
            {
                FiscalYears.Add(year);
            }
            if (selectedId.HasValue)
            {
                SelectedFiscalYear = FiscalYears.FirstOrDefault(y => y.FiscalYearId == selectedId.Value);
            }
        }

        private async Task Add()
        {
            var editorViewModel = new FiscalYearEditorViewModel();
            var result = await ShowDialog.Handle(editorViewModel);

            if (result.HasValue)
            {
                _service.Create(result.Value.Item1, result.Value.Item2, result.Value.Item3);
                LoadFiscalYears();
            }
        }

        private async Task Edit()
        {
            if (SelectedFiscalYear == null) return;

            var editorViewModel = new FiscalYearEditorViewModel
            {
                Description = SelectedFiscalYear.Description,
                DateFrom = SelectedFiscalYear.DateFrom,
                DateTo = SelectedFiscalYear.DateTo
            };

            var result = await ShowDialog.Handle(editorViewModel);

            if (result.HasValue)
            {
                _service.Update(SelectedFiscalYear.FiscalYearId, result.Value.Item1, result.Value.Item2, result.Value.Item3);
                LoadFiscalYears();
            }
        }

        private void Delete()
        {
            if (SelectedFiscalYear == null) return;
            _service.Delete(SelectedFiscalYear.FiscalYearId);
            LoadFiscalYears();
        }

        private void Activate()
        {
            if (SelectedFiscalYear == null) return;
            _service.Activate(SelectedFiscalYear.FiscalYearId);
            LoadFiscalYears();
        }
    }
}