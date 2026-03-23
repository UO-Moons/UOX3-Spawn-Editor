using System;
using System.Drawing;
using System.Windows.Forms;

namespace UOX3SpawnEditor
{
    public partial class HelpForm : Form
    {
        public HelpForm(bool darkModeEnabled)
        {
            InitializeComponent();
            ApplyTheme(darkModeEnabled);
            LoadHelpText();
        }

        private void ApplyTheme(bool darkModeEnabled)
        {
            Color backColor;
            Color foreColor;
            Color buttonBackColor;
            Color buttonForeColor;
            Color textBackColor;
            Color textForeColor;

            if (darkModeEnabled)
            {
                backColor = Color.FromArgb(45, 45, 48);
                foreColor = Color.White;
                buttonBackColor = Color.FromArgb(63, 63, 70);
                buttonForeColor = Color.White;
                textBackColor = Color.FromArgb(30, 30, 30);
                textForeColor = Color.White;
            }
            else
            {
                backColor = SystemColors.Control;
                foreColor = SystemColors.ControlText;
                buttonBackColor = SystemColors.Control;
                buttonForeColor = SystemColors.ControlText;
                textBackColor = Color.White;
                textForeColor = Color.Black;
            }

            this.BackColor = backColor;
            this.ForeColor = foreColor;

            labelTitle.ForeColor = foreColor;

            richTextBoxHelp.BackColor = textBackColor;
            richTextBoxHelp.ForeColor = textForeColor;

            buttonClose.BackColor = buttonBackColor;
            buttonClose.ForeColor = buttonForeColor;
        }

        private void LoadHelpText()
        {
            richTextBoxHelp.Clear();

            AddHeader("Mouse Controls");
            AddEntry("Shift + Left Click + Drag", "Create a new spawn region");
            AddEntry("Left Click + Drag inside a region", "Move the selected spawn region rectangle");
            AddEntry("Left Click on a resize handle", "Resize the selected spawn region rectangle");
            AddEntry("Mouse Wheel", "Zoom in or out");
            AddEntry("Middle Mouse Drag", "Pan the map");
            AddEntry("Space + Left Drag", "Pan the map");
            AddEntry("Move mouse near map edge", "Pan the map without dragging");
            AddEntry("Right Click on a spawn region", "Rename, duplicate, delete, or edit tags");

            AddHeader("Keyboard Shortcuts");
            AddEntry("Delete", "Delete the selected spawn region");
            AddEntry("Ctrl + Z", "Undo the last edit");
            AddEntry("Home", "Reset the view");
            AddEntry("Ctrl + Shift + N", "Create a new spawn file for the current world");
            AddEntry("Ctrl + Shift + Delete", "Choose and delete a spawn file for the current world");

            AddHeader("Sidebar and Toolbar");
            AddEntry("Sidebar", "Shows visible spawn regions with checkboxes");
            AddEntry("Visibility button", "Show all, hide all, or show only the selected region");
            AddEntry("Go To button", "Center the map on the selected region");
            AddEntry("Labels button", "Cycle between selected labels, all labels, or no labels");

            AddHeader("Menus");
            AddEntry("File Menu", "Open map source, load spawn files, save spawn files, create a new spawn file, or toggle spawn regions");
            AddEntry("Open Map Source", "Load a UO client folder with map.mul files and radarcol.mul, or load a single image file");
            AddEntry("Theme Menu", "Switch between Light Mode and Dark Mode");

            richTextBoxHelp.SelectionStart = 0;
            richTextBoxHelp.SelectionLength = 0;
        }

        private void AddHeader(string headerText)
        {
            richTextBoxHelp.SelectionFont = new Font(richTextBoxHelp.Font, FontStyle.Bold);
            richTextBoxHelp.AppendText(headerText + Environment.NewLine);
            richTextBoxHelp.AppendText(new string('-', headerText.Length) + Environment.NewLine);
            richTextBoxHelp.AppendText(Environment.NewLine);
        }

        private void AddEntry(string title, string description)
        {
            richTextBoxHelp.SelectionFont = new Font(richTextBoxHelp.Font, FontStyle.Bold);
            richTextBoxHelp.AppendText(title + Environment.NewLine);

            richTextBoxHelp.SelectionFont = new Font(richTextBoxHelp.Font, FontStyle.Regular);
            richTextBoxHelp.AppendText("  " + description + Environment.NewLine);
            richTextBoxHelp.AppendText(Environment.NewLine);
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
