using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using GRCFinancialControl.Avalonia.ViewModels;

namespace GRCFinancialControl.Avalonia.Views
{
    public partial class ForecastEngagementEditorView : UserControl
    {
        private ForecastEngagementEditorViewModel? _viewModel;

        public ForecastEngagementEditorView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_viewModel is not null)
            {
                _viewModel.FiscalYears.CollectionChanged -= OnFiscalYearsChanged;
            }

            _viewModel = DataContext as ForecastEngagementEditorViewModel;

            if (_viewModel is not null)
            {
                _viewModel.FiscalYears.CollectionChanged += OnFiscalYearsChanged;
            }

            BuildColumns();
        }

        private void OnFiscalYearsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            BuildColumns();
        }

        private void BuildColumns()
        {
            if (ForecastGrid is null)
            {
                return;
            }

            ForecastGrid.Columns.Clear();

            var rankColumn = new DataGridTextColumn
            {
                Header = "Rank",
                Binding = new Binding(nameof(ForecastEngagementEditorViewModel.ForecastEngagementRowViewModel.Rank))
                {
                    Mode = BindingMode.TwoWay
                },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            ForecastGrid.Columns.Add(rankColumn);

            if (_viewModel is not null)
            {
                for (var index = 0; index < _viewModel.FiscalYears.Count; index++)
                {
                    var columnIndex = index;
                    var fiscalYear = _viewModel.FiscalYears[index];

                    var column = new DataGridTemplateColumn
                    {
                        Header = fiscalYear.Name,
                        CellTemplate = new FuncDataTemplate<ForecastEngagementEditorViewModel.ForecastEngagementRowViewModel>((row, _) =>
                        {
                            var textBox = new TextBox
                            {
                                HorizontalContentAlignment = HorizontalAlignment.Right,
                                Margin = new Thickness(4, 2, 4, 2)
                            };

                            textBox.Bind(TextBox.TextProperty, new Binding($"Cells[{columnIndex}].ForecastHours")
                            {
                                Mode = BindingMode.TwoWay,
                                StringFormat = "N2",
                                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                            });

                            textBox.Bind(ToolTip.TipProperty, new Binding($"Cells[{columnIndex}].ActualHours")
                            {
                                StringFormat = "Realizado: {0:N2} h"
                            });

                            return textBox;
                        })
                    };

                    ForecastGrid.Columns.Add(column);
                }

                var deleteColumn = new DataGridTemplateColumn
                {
                    Header = "Excluir",
                    Width = DataGridLength.Auto,
                    CellTemplate = new FuncDataTemplate<ForecastEngagementEditorViewModel.ForecastEngagementRowViewModel>((row, _) =>
                    {
                        var button = new Button
                        {
                            Content = "Remover",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(4, 0, 4, 0),
                            Command = _viewModel.RemoveRowCommand,
                            CommandParameter = row
                        };

                        button.Bind(Button.IsEnabledProperty, new Binding(nameof(ForecastEngagementEditorViewModel.ForecastEngagementRowViewModel.CanDelete)));

                        return button;
                    })
                };

                ForecastGrid.Columns.Add(deleteColumn);
            }
        }
    }
}
