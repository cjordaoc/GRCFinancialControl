using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GRCFinancialControl.Avalonia.Views.Dialogs
{
    public partial class ManagerSelectionView : UserControl
    {
        public ManagerSelectionView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

