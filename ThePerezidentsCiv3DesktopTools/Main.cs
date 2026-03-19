namespace ThePerezidentsCiv3DesktopTools
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
        }

        private void btnExportUnitsToCsv_Click(object sender, EventArgs e)
        {
            ExportUnitsToCsvDialog.Show();
        }

        private void btnRevealMap_Click(object sender, EventArgs e)
        {
            RevealMapDialog.Show();
        }

        private void btnToggleHumanAI_Click(object sender, EventArgs e)
        {
            ToggleHumanAIDialog.Show();
        }
    }
}
