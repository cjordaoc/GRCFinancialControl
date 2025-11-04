using Avalonia.Controls;

namespace GRCFinancialControl.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Set ZIndex for toast notifications to appear above other content
            if (this.FindControl<ItemsControl>("ToastItemsControl") is { } toastControl)
            {
                toastControl.SetValue(Panel.ZIndexProperty, 10000);
            }
        }
    }
}
