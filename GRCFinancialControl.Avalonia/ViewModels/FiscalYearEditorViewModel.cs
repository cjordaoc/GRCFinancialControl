using System;
using System.Reactive;
using ReactiveUI;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class FiscalYearEditorViewModel : ViewModelBase
    {
        private string _description = string.Empty;
        private DateTimeOffset? _dateFrom;
        private DateTimeOffset? _dateTo;

        public string Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }

        public DateTimeOffset? DateFrom
        {
            get => _dateFrom;
            set => this.RaiseAndSetIfChanged(ref _dateFrom, value);
        }

        public DateTimeOffset? DateTo
        {
            get => _dateTo;
            set => this.RaiseAndSetIfChanged(ref _dateTo, value);
        }

        public ReactiveCommand<Unit, (string, DateTime, DateTime)?> SaveCommand { get; }

        public FiscalYearEditorViewModel()
        {
            var canSave = this.WhenAnyValue(
                x => x.Description,
                x => x.DateFrom,
                x => x.DateTo,
                (desc, from, to) =>
                    !string.IsNullOrWhiteSpace(desc) &&
                    from.HasValue &&
                    to.HasValue &&
                    from.Value <= to.Value);

            SaveCommand = ReactiveCommand.Create(() =>
            {
                return (Description, DateFrom!.Value.Date, DateTo!.Value.Date);
            }, canSave);
        }
    }
}