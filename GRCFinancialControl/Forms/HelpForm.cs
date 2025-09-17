using System.Windows.Forms;

namespace GRCFinancialControl.Forms
{
    public partial class HelpForm : Form
    {
        public HelpForm(string infoText, string readmeText)
        {
            InitializeComponent();
            txtInfo.Text = infoText;
            rtbReadme.Text = readmeText;
        }
    }
}
