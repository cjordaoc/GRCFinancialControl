using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GRCFinancialControl.Avalonia.Views.Dialogs
{
    public partial class ManagerAssignmentEditorView : UserControl
    {
        public ManagerAssignmentEditorView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

