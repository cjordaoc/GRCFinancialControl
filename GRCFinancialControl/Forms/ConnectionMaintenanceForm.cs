using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using GRCFinancialControl.Persistence;

namespace GRCFinancialControl.Forms
{
    public partial class ConnectionMaintenanceForm : Form
    {
        private readonly LocalAppRepository _repository;
        private IReadOnlyList<ConnectionDefinition> _connections = Array.Empty<ConnectionDefinition>();

        public ConnectionMaintenanceForm(LocalAppRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            InitializeComponent();
            LoadConnections();
        }

        private void LoadConnections()
        {
            _connections = _repository.GetConnections();
            lstConnections.BeginUpdate();
            try
            {
                lstConnections.Items.Clear();
                foreach (var definition in _connections)
                {
                    lstConnections.Items.Add(definition);
                }
            }
            finally
            {
                lstConnections.EndUpdate();
            }

            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var hasSelection = lstConnections.SelectedItem is ConnectionDefinition;
            btnEdit.Enabled = hasSelection;
            btnDelete.Enabled = hasSelection;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            using var editor = new ConnectionEditorForm();
            if (editor.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var definition = editor.Connection;
            if (definition == null)
            {
                return;
            }

            try
            {
                var id = _repository.InsertConnection(definition);
                definition.Id = id;
                LoadConnections();
                SelectConnection(id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to add connection: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (lstConnections.SelectedItem is not ConnectionDefinition selected)
            {
                return;
            }

            using var editor = new ConnectionEditorForm(selected.Clone());
            if (editor.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var definition = editor.Connection;
            if (definition == null)
            {
                return;
            }

            try
            {
                _repository.UpdateConnection(definition);
                LoadConnections();
                SelectConnection(definition.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to update connection: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (lstConnections.SelectedItem is not ConnectionDefinition selected)
            {
                return;
            }

            var confirm = MessageBox.Show(this, $"Delete connection '{selected.Name}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _repository.DeleteConnection(selected.Id);
                LoadConnections();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to delete connection: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void lstConnections_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void SelectConnection(long id)
        {
            foreach (var item in lstConnections.Items.OfType<ConnectionDefinition>())
            {
                if (item.Id == id)
                {
                    lstConnections.SelectedItem = item;
                    return;
                }
            }
        }
    }
}
