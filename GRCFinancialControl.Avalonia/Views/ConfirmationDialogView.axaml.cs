using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GRCFinancialControl.Avalonia.Views
{
    public partial class ConfirmationDialogView : UserControl
    {
        public ConfirmationDialogView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
