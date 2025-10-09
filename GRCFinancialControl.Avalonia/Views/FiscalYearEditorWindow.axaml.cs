using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using GRCFinancialControl.Avalonia.ViewModels;
using ReactiveUI;
using System;

namespace GRCFinancialControl.Avalonia.Views
{
    public partial class FiscalYearEditorWindow : ReactiveWindow<FiscalYearEditorViewModel>
    {
        public FiscalYearEditorWindow()
        {
            InitializeComponent();
            this.WhenActivated(d => d(ViewModel!.SaveCommand.Subscribe(Close)));
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}