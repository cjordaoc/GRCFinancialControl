using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Data;
using GRCFinancialControl.Core.Services;
using ReactiveUI;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class MeasurementPeriodViewModel : ViewModelBase
    {
        private readonly MeasurementPeriodService _service;
        private readonly ParametersService _parametersService;
        private MeasurementPeriod? _selectedPeriod;

        public ObservableCollection<MeasurementPeriod> Periods { get; } = new();

        public Interaction<MeasurementPeriodEditorViewModel, string?> ShowDialog { get; }

        public MeasurementPeriod? SelectedPeriod
        {
            get => _selectedPeriod;
            set => this.RaiseAndSetIfChanged(ref _selectedPeriod, value);
        }

        public ReactiveCommand<Unit, Unit> AddPeriodCommand { get; }
        public ReactiveCommand<MeasurementPeriod, Unit> SetActivePeriodCommand { get; }

        public MeasurementPeriodViewModel(MeasurementPeriodService service, ParametersService parametersService)
        {
            _service = service;
            _parametersService = parametersService;

            ShowDialog = new Interaction<MeasurementPeriodEditorViewModel, string?>();

            AddPeriodCommand = ReactiveCommand.CreateFromTask(AddPeriod);

            var canSetActive = this.WhenAnyValue(x => x.SelectedPeriod, p => p != null);
            SetActivePeriodCommand = ReactiveCommand.Create<MeasurementPeriod>(SetActivePeriod, canSetActive);

            LoadPeriods();
        }

        private void LoadPeriods()
        {
            var selectedId = SelectedPeriod?.PeriodId;
            Periods.Clear();
            var periods = _service.GetAll();
            foreach (var period in periods)
            {
                Periods.Add(period);
            }
            if (selectedId.HasValue)
            {
                SelectedPeriod = Periods.FirstOrDefault(p => p.PeriodId == selectedId.Value);
            }
        }

        private async Task AddPeriod()
        {
            var editorViewModel = new MeasurementPeriodEditorViewModel();
            var result = await ShowDialog.Handle(editorViewModel);

            if (!string.IsNullOrWhiteSpace(result))
            {
                _service.Create(result);
                LoadPeriods();
            }
        }

        private void SetActivePeriod(MeasurementPeriod period)
        {
            if (period == null) return;
            _parametersService.SetSelectedMeasurePeriodId(period.PeriodId.ToString());
        }
    }
}