using System;
using System.Windows.Forms;
using GRCFinancialControl.Persistence;

namespace GRCFinancialControl.Forms
{
    public partial class ConnectionEditorForm : Form
    {
        private readonly ConnectionDefinition _definition;

        public ConnectionDefinition? Connection { get; private set; }

        public ConnectionEditorForm(ConnectionDefinition? definition = null)
        {
            InitializeComponent();
            _definition = definition?.Clone() ?? new ConnectionDefinition();
            BindDefinition();
        }

        private void BindDefinition()
        {
            txtName.Text = _definition.Name;
            txtServer.Text = _definition.Server;
            txtDatabase.Text = _definition.Database;
            txtUsername.Text = _definition.Username;
            txtPassword.Text = _definition.Password;
            numPort.Value = Math.Clamp(_definition.Port, 1u, 65535u);
            chkUseSsl.Checked = _definition.UseSsl;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            var name = txtName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return;
            }

            var server = txtServer.Text?.Trim();
            if (string.IsNullOrWhiteSpace(server))
            {
                MessageBox.Show(this, "Server is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtServer.Focus();
                return;
            }

            var database = txtDatabase.Text?.Trim();
            if (string.IsNullOrWhiteSpace(database))
            {
                MessageBox.Show(this, "Database is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDatabase.Focus();
                return;
            }

            var username = txtUsername.Text?.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show(this, "Username is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUsername.Focus();
                return;
            }

            _definition.Name = name;
            _definition.Server = server;
            _definition.Database = database;
            _definition.Username = username;
            _definition.Password = txtPassword.Text ?? string.Empty;
            _definition.Port = (uint)numPort.Value;
            _definition.UseSsl = chkUseSsl.Checked;

            Connection = _definition;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
