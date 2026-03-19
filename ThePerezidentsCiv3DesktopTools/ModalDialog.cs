using Civ3Tools;
using QueryCiv3;

namespace ThePerezidentsCiv3DesktopTools
{
    public partial class ModalDialog : Form
    {
        public ModalDialog(string message, string title = "", string confirmText = "OK", string cancelText = "Cancel")
        {
            InitializeComponent();
            lblMessage.Text = message;
            Text = title;
            btnConfirm.Text = confirmText;
            btnCancel.Text = cancelText;
        }

        protected virtual void btnConfirm_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        protected virtual void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    public class ExportUnitsToCsvDialog : ModalDialog
    {
        private static string? ScenarioPath = null;
        private static List<string>? UnitLines = null;
        public ExportUnitsToCsvDialog() : base("No file selected", "Export Unit Data to CSV", "Load scenario file", "Export to CSV")
        {
        }

        public static new void Show()
        {
            using var dialog = new ExportUnitsToCsvDialog();
            dialog.ShowDialog();
        }

        protected override void btnConfirm_Click(object sender, EventArgs e)
        {
            string Civ3Path = Civ3Location.GetCiv3Path();
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Civ3 Scenario Files (*.biq)|*.biq";
            ofd.InitialDirectory = (Civ3Path != "/civ3/path/not/found") ? Civ3Path : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    UnitLines = GetUnitInfo.GetUnitListString(ofd.FileName);
                    ScenarioPath = ofd.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ScenarioPath = null;
                    UnitLines = null;
                }
                finally
                {
                    MessageBox.Show($"Scenario file loaded successfully. Ready to export to CSV.", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            if (ScenarioPath == null)
                lblMessage.Text = "No file selected";
            else
                lblMessage.Text = Path.GetFileName(ScenarioPath);
        }

        protected override void btnCancel_Click(object sender, EventArgs e)
        {
            if (ScenarioPath == null || UnitLines == null)
            {
                MessageBox.Show("No valid scenario file loaded. Please load a scenario file before exporting.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string Civ3Path = Civ3Location.GetCiv3Path();
            using SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
            saveFileDialog.InitialDirectory = (Civ3Path != "/civ3/path/not/found") ? Civ3Path : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(ScenarioPath) + "_units.csv";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllLines(saveFileDialog.FileName, UnitLines);
                    MessageBox.Show($"Unit data exported successfully to {saveFileDialog.FileName}", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting to CSV: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    public class ToggleHumanAIDialog : ModalDialog
    {
        private string? SavePath = null;
        private byte[]? SaveBytes = null;
        private readonly List<(int slot, RadioButton human, RadioButton ai)> playerRows = new();
        private readonly Panel playerPanel;

        public ToggleHumanAIDialog() : base("No file selected", "Toggle Human/AI Players", "Load SAV file", "Export Modified SAV")
        {
            FormBorderStyle = FormBorderStyle.FixedSingle;
            lblMessage.Size = new Size(360, 40);
            playerPanel = new Panel { Location = new Point(12, 60), Size = new Size(360, 0) };
            Controls.Add(playerPanel);
            UpdateLayout();
        }

        public static new void Show()
        {
            using var dialog = new ToggleHumanAIDialog();
            dialog.ShowDialog();
        }

        private void UpdateLayout()
        {
            btnConfirm.Location = new Point(12, playerPanel.Bottom + 8);
            btnCancel.Location = new Point(202, playerPanel.Bottom + 8);
            ClientSize = new Size(384, btnConfirm.Bottom + 12);
        }

        private void PopulatePlayerRows(bool[] humanFlags, bool[] remainingFlags)
        {
            playerPanel.Controls.Clear();
            playerRows.Clear();

            int lastSlot = 0;
            for (int i = 1; i < 32; i++)
                if (humanFlags[i] || remainingFlags[i]) lastSlot = i;

            for (int slot = 1; slot <= lastSlot; slot++)
            {
                var rowPanel = new Panel { Location = new Point(0, (slot - 1) * 28), Size = new Size(360, 28) };
                var lbl = new Label { Text = $"Player {slot}", Location = new Point(0, 4), Size = new Size(80, 20) };
                var rbHuman = new RadioButton { Text = "Human", Location = new Point(88, 4), Size = new Size(80, 20) };
                var rbAI = new RadioButton { Text = "AI", Location = new Point(176, 4), Size = new Size(60, 20) };
                rowPanel.Controls.Add(lbl);
                rowPanel.Controls.Add(rbHuman);
                rowPanel.Controls.Add(rbAI);
                // Set checked state after adding to container so mutual exclusion applies
                if (humanFlags[slot]) rbHuman.Checked = true;
                else rbAI.Checked = true;
                playerPanel.Controls.Add(rowPanel);
                playerRows.Add((slot, rbHuman, rbAI));
            }

            playerPanel.Size = new Size(360, Math.Max(lastSlot, 1) * 28);
            UpdateLayout();
        }

        protected override void btnConfirm_Click(object sender, EventArgs e)
        {
            string Civ3Path = Civ3Location.GetCiv3Path();
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Civ3 Save Files (*.sav)|*.sav";
            ofd.InitialDirectory = (Civ3Path != "/civ3/path/not/found") ? Civ3Path : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    byte[] loaded = Util.ReadFile(ofd.FileName);
                    bool[] humanFlags = PlayerToggler.GetHumanPlayers(loaded);
                    bool[] remainingFlags = PlayerToggler.GetRemainingPlayers(loaded);
                    SaveBytes = loaded;
                    SavePath = ofd.FileName;
                    PopulatePlayerRows(humanFlags, remainingFlags);
                    lblMessage.Text = Path.GetFileName(SavePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SaveBytes = null;
                    SavePath = null;
                }
            }
        }

        protected override void btnCancel_Click(object sender, EventArgs e)
        {
            if (SavePath == null || SaveBytes == null)
            {
                MessageBox.Show("No valid save file loaded. Please load a save file before exporting.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            byte[] modified = (byte[])SaveBytes.Clone();
            foreach (var (slot, rbHuman, _) in playerRows)
                PlayerToggler.SetHumanPlayer(modified, slot, rbHuman.Checked);

            string Civ3Path = Civ3Location.GetCiv3Path();
            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "SAV files (*.sav)|*.sav";
            sfd.InitialDirectory = (Civ3Path != "/civ3/path/not/found") ? Civ3Path : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            sfd.FileName = Path.GetFileNameWithoutExtension(SavePath) + "_TOGGLED.sav";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllBytes(sfd.FileName, modified);
                    MessageBox.Show($"SAV exported successfully to {sfd.FileName}", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting SAV file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    public class RevealMapDialog : ModalDialog
    {
        private static string? SavePath = null;
        private static byte[]? RevealedMapBytes = null;
        public RevealMapDialog() : base("No file selected", "Reveal Map for Player 1", "Load SAV file", "Export Revealed SAV File")
        {
        }
        public static new void Show()
        {
            using var dialog = new RevealMapDialog();
            dialog.ShowDialog();
        }
        protected override void btnConfirm_Click(object sender, EventArgs e)
        {
            string Civ3Path = Civ3Location.GetCiv3Path();
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Civ3 Save Files (*.sav)|*.sav";
            ofd.InitialDirectory = (Civ3Path != "/civ3/path/not/found") ? Civ3Path : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    RevealedMapBytes = MapRevealer.RevealedMap(ofd.FileName);
                    SavePath = ofd.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SavePath = null;
                    RevealedMapBytes = null;
                }
                finally
                {
                    MessageBox.Show($"Save file processed successfully. Ready to export SAV with revealed map for Player 1.", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            if (SavePath == null)
                lblMessage.Text = "No file selected";
            else
                lblMessage.Text = Path.GetFileName(SavePath);
        }

        protected override void btnCancel_Click(object sender, EventArgs e)
        {
            if (SavePath == null || RevealedMapBytes == null)
            {
                MessageBox.Show("No valid save file loaded. Please load a save file before exporting.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string Civ3Path = Civ3Location.GetCiv3Path();
            using SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "SAV files (*.sav)|*.sav";
            saveFileDialog.InitialDirectory = (Civ3Path != "/civ3/path/not/found") ? Civ3Path : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(SavePath) + "_REVEALED.sav";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllBytes(saveFileDialog.FileName, RevealedMapBytes);
                    MessageBox.Show($"SAV exported successfully to {saveFileDialog.FileName}", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting SAV file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}