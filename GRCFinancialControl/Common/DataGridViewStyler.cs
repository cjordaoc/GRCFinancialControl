using System;
using System.Windows.Forms;
using GRCFinancialControl.Uploads;

namespace GRCFinancialControl.Common
{
    public static class DataGridViewStyler
    {
        public static void ConfigureUploadSummaryGrid(DataGridView grid)
        {
            ArgumentNullException.ThrowIfNull(grid);

            grid.AutoGenerateColumns = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.RowHeadersVisible = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Columns.Clear();

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UploadFileSummary.FileName),
                HeaderText = "File",
                FillWeight = 40,
                MinimumWidth = 150
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UploadFileSummary.StatusText),
                HeaderText = "Status",
                FillWeight = 20,
                MinimumWidth = 80
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UploadFileSummary.Details),
                HeaderText = "Details",
                FillWeight = 40,
                MinimumWidth = 200
            });
        }
    }
}
