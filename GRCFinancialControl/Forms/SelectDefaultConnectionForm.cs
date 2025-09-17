using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using GRCFinancialControl.Persistence;

namespace GRCFinancialControl.Forms
{
    public partial class SelectDefaultConnectionForm : Form
    {
        public ConnectionDefinition? SelectedConnection { get; private set; }

        public SelectDefaultConnectionForm(IReadOnlyList<ConnectionDefinition> connections, long? selectedId)
        {
            InitializeComponent();
            cmbConnections.DisplayMember = nameof(ConnectionDefinition.Name);
            foreach (var connection in connections)
            {
                cmbConnections.Items.Add(connection);
            }

            if (selectedId.HasValue)
            {
                var match = connections.FirstOrDefault(c => c.Id == selectedId.Value);
                if (match != null)
                {
                    cmbConnections.SelectedItem = match;
                }
            }

            if (cmbConnections.SelectedIndex < 0 && cmbConnections.Items.Count > 0)
            {
                cmbConnections.SelectedIndex = 0;
            }

            UpdateOkState();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (cmbConnections.SelectedItem is ConnectionDefinition definition)
            {
                SelectedConnection = definition;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void cmbConnections_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateOkState();
        }

        private void UpdateOkState()
        {
            btnOk.Enabled = cmbConnections.SelectedItem is ConnectionDefinition;
        }
    }
}
