using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GRCFinancialControl.Avalonia.Views.Dialogs
{
    public partial class DialogWindow : Window
    {
        public DialogWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
