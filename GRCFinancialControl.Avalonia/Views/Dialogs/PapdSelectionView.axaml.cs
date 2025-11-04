using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GRCFinancialControl.Avalonia.Views.Dialogs
{
    public partial class PapdSelectionView : UserControl
    {
        public PapdSelectionView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

