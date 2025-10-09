using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using GRCFinancialControl.Avalonia.ViewModels;
using ReactiveUI;
using System.Threading.Tasks;

namespace GRCFinancialControl.Avalonia.Views
{
    public partial class FiscalYearMaintenanceWindow : ReactiveWindow<FiscalYearMaintenanceViewModel>
    {
        public FiscalYearMaintenanceWindow()
        {
            InitializeComponent();
            this.WhenActivated(d => d(ViewModel!.ShowDialog.RegisterHandler(DoShowDialogAsync)));
        }

        private async Task DoShowDialogAsync(InteractionContext<FiscalYearEditorViewModel, (string, System.DateTime, System.DateTime)?> interaction)
        {
            var dialog = new FiscalYearEditorWindow
            {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<(string, System.DateTime, System.DateTime)?>(this);
            interaction.SetOutput(result);
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}