using System.Reactive;
using ReactiveUI;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class MeasurementPeriodEditorViewModel : ViewModelBase
    {
        private string _description = string.Empty;

        public string Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }

        public ReactiveCommand<Unit, string?> SaveCommand { get; }

        public MeasurementPeriodEditorViewModel()
        {
            var canSave = this.WhenAnyValue(
                x => x.Description,
                desc => !string.IsNullOrWhiteSpace(desc));

            SaveCommand = ReactiveCommand.Create(() => Description, canSave);
        }
    }
}