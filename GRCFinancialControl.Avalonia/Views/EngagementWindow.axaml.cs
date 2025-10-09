using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GRCFinancialControl.Avalonia.Views
{
    public partial class EngagementWindow : Window
    {
        public EngagementWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}