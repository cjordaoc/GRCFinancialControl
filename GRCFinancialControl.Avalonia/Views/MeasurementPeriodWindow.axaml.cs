using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GRCFinancialControl.Avalonia.Views
{
    public partial class MeasurementPeriodWindow : Window
    {
        public MeasurementPeriodWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}