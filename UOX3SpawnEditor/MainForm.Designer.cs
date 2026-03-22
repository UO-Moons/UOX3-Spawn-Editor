using System;
using System.Drawing;
using System.Windows.Forms;

namespace UOX3SpawnEditor
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private PictureBox pictureBox1;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem loadRegionsToolStripMenuItem;
        private ToolStripMenuItem toggleRegionsToolStripMenuItem;
        private ToolStripMenuItem saveRegionsToolStripMenuItem;
        private ToolStripMenuItem themeToolStripMenuItem;
        private ToolStripMenuItem profilesToolStripMenuItem;
        private ToolStripMenuItem switchProfileToolStripMenuItem;
        private ToolStripMenuItem newProfileToolStripMenuItem;
        private ToolStripMenuItem renameProfileToolStripMenuItem;
        private ToolStripMenuItem deleteProfileToolStripMenuItem;
        private ToolStripMenuItem saveProfileToolStripMenuItem;
        private ToolStripMenuItem lightModeToolStripMenuItem;
        private ToolStripMenuItem darkModeToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripButton toggleLabelsToolStripButton;
        private ToolStripButton editSelectedToolStripButton;
        private ToolStripButton duplicateSelectedToolStripButton;
        private ToolStrip toolStrip1;
        private ToolStripButton zoomInToolStripButton;
        private ToolStripButton zoomOutToolStripButton;
        private ToolStripLabel zoomLabel;
        private ToolStripComboBox zoomComboBox;
        private CheckedListBox checkedListBoxRegions;
        private TextBox txtRegionSearch;
        private Panel panelRegionSidebar;
        private TableLayoutPanel mainLayout;
        private ContextMenuStrip regionContextMenu;
        private ToolStripMenuItem editTagsMenuItem;
        private ToolStripMenuItem compareTagsMenuItem;
        private ComboBox comboBoxRegionGroups;
        private ComboBox comboWorldFilter;
        private ComboBox comboBoxWorlds;
        private FlowLayoutPanel panelSidebarButtons;
        private Button buttonHideAll;
        private Button buttonShowAll;
        private Button buttonShowOnlySelected;
        private ToolStripButton undoToolStripButton;
        private ToolStripMenuItem undoToolStripMenuItem;
        private Panel panelSelectionDetails;
        private Label labelSelectedRegionHeader;
        private TableLayoutPanel tableLayoutSelectedRegionDetails;
        private TextBox txtSelectedName;
        private TextBox txtSelectedRegionNumber;
        private TextBox txtSelectedWorld;
        private TextBox txtSelectedSourceFile;
        private TextBox txtSelectedSpawnSource;
        private TextBox txtSelectedCounts;
        private TextBox txtSelectedRespawn;
        private TextBox txtSelectedZ;
        private TextBox txtSelectedBounds;
        private TextBox txtSelectedRaw;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            ApplyWindowTitleBarTheme();
            this.pictureBox1 = new PictureBox();
            this.menuStrip1 = new MenuStrip();
            this.fileToolStripMenuItem = new ToolStripMenuItem();
            this.openToolStripMenuItem = new ToolStripMenuItem();
            this.loadRegionsToolStripMenuItem = new ToolStripMenuItem();
            this.toggleRegionsToolStripMenuItem = new ToolStripMenuItem();
            this.saveRegionsToolStripMenuItem = new ToolStripMenuItem();
            this.themeToolStripMenuItem = new ToolStripMenuItem();
            this.profilesToolStripMenuItem = new ToolStripMenuItem();
            this.switchProfileToolStripMenuItem = new ToolStripMenuItem();
            this.newProfileToolStripMenuItem = new ToolStripMenuItem();
            this.renameProfileToolStripMenuItem = new ToolStripMenuItem();
            this.deleteProfileToolStripMenuItem = new ToolStripMenuItem();
            this.saveProfileToolStripMenuItem = new ToolStripMenuItem();
            this.lightModeToolStripMenuItem = new ToolStripMenuItem();
            this.darkModeToolStripMenuItem = new ToolStripMenuItem();
            this.helpToolStripMenuItem = new ToolStripMenuItem();
            this.toggleLabelsToolStripButton = new ToolStripButton();
            this.editSelectedToolStripButton = new ToolStripButton();
            this.duplicateSelectedToolStripButton = new ToolStripButton();

            this.undoToolStripButton = new ToolStripButton();
            this.undoToolStripMenuItem = new ToolStripMenuItem();

            this.panelSidebarButtons = new FlowLayoutPanel();
            this.buttonHideAll = new Button();
            this.buttonShowAll = new Button();
            this.buttonShowOnlySelected = new Button();

            this.toolStrip1 = new ToolStrip();
            this.zoomInToolStripButton = new ToolStripButton();
            this.zoomOutToolStripButton = new ToolStripButton();
            this.zoomLabel = new ToolStripLabel();
            this.zoomComboBox = new ToolStripComboBox();

            this.checkedListBoxRegions = new CheckedListBox();
            this.txtRegionSearch = new TextBox();
            this.panelRegionSidebar = new Panel();
            this.mainLayout = new TableLayoutPanel();
            this.panelSelectionDetails = new Panel();
            this.labelSelectedRegionHeader = new Label();
            this.tableLayoutSelectedRegionDetails = new TableLayoutPanel();
            this.txtSelectedName = new TextBox();
            this.txtSelectedRegionNumber = new TextBox();
            this.txtSelectedWorld = new TextBox();
            this.txtSelectedSourceFile = new TextBox();
            this.txtSelectedSpawnSource = new TextBox();
            this.txtSelectedCounts = new TextBox();
            this.txtSelectedRespawn = new TextBox();
            this.txtSelectedZ = new TextBox();
            this.txtSelectedBounds = new TextBox();
            this.txtSelectedRaw = new TextBox();

            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();

            // menuStrip1
            this.menuStrip1.Items.AddRange(new ToolStripItem[] {
                this.fileToolStripMenuItem,
                this.profilesToolStripMenuItem,
                this.themeToolStripMenuItem,
                this.helpToolStripMenuItem
            });
            this.menuStrip1.Location = new Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new Size(1000, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.BackColor = Color.LightBlue;

            this.fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.openToolStripMenuItem,
                this.loadRegionsToolStripMenuItem,
                this.toggleRegionsToolStripMenuItem,
                this.saveRegionsToolStripMenuItem,
                new ToolStripSeparator(),
                this.undoToolStripMenuItem
            });
            this.fileToolStripMenuItem.Text = "File";

            this.openToolStripMenuItem.Text = "Open Map Image";
            this.openToolStripMenuItem.Click += new EventHandler(this.openToolStripMenuItem_Click);

            this.loadRegionsToolStripMenuItem.Text = "Load Spawn File";
            this.loadRegionsToolStripMenuItem.Click += new EventHandler(this.loadRegionsToolStripMenuItem_Click);

            this.toggleRegionsToolStripMenuItem.Text = "Toggle All Spawn Regions";
            this.toggleRegionsToolStripMenuItem.Click += new EventHandler(this.toggleRegionsToolStripMenuItem_Click);

            this.saveRegionsToolStripMenuItem.Text = "Save Spawn File";
            this.saveRegionsToolStripMenuItem.Click += new EventHandler(this.saveRegionsToolStripMenuItem_Click);

            this.profilesToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.switchProfileToolStripMenuItem,
                new ToolStripSeparator(),
                this.newProfileToolStripMenuItem,
                this.renameProfileToolStripMenuItem,
                this.deleteProfileToolStripMenuItem,
                this.saveProfileToolStripMenuItem
            });
            this.profilesToolStripMenuItem.Text = "Profiles";

            this.switchProfileToolStripMenuItem.Text = "Switch Profile";

            this.newProfileToolStripMenuItem.Text = "New Profile";
            this.newProfileToolStripMenuItem.Click += new EventHandler(this.newProfileToolStripMenuItem_Click);

            this.renameProfileToolStripMenuItem.Text = "Rename Current Profile";
            this.renameProfileToolStripMenuItem.Click += new EventHandler(this.renameProfileToolStripMenuItem_Click);

            this.deleteProfileToolStripMenuItem.Text = "Delete Current Profile";
            this.deleteProfileToolStripMenuItem.Click += new EventHandler(this.deleteProfileToolStripMenuItem_Click);

            this.saveProfileToolStripMenuItem.Text = "Save Current Profile";
            this.saveProfileToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            this.saveProfileToolStripMenuItem.Click += new EventHandler(this.saveProfileToolStripMenuItem_Click);

            this.themeToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {

                this.lightModeToolStripMenuItem,
                this.darkModeToolStripMenuItem
            });
            this.themeToolStripMenuItem.Text = "Theme";

            this.lightModeToolStripMenuItem.Text = "Light Mode";
            this.lightModeToolStripMenuItem.Click += new EventHandler(this.lightModeToolStripMenuItem_Click);

            this.darkModeToolStripMenuItem.Text = "Dark Mode";
            this.darkModeToolStripMenuItem.Click += new EventHandler(this.darkModeToolStripMenuItem_Click);

            this.helpToolStripMenuItem.Text = "Help";
            this.helpToolStripMenuItem.Click += new EventHandler(this.helpToolStripMenuItem_Click);

            this.undoToolStripMenuItem.Text = "Undo";
            this.undoToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
            this.undoToolStripMenuItem.Click += new EventHandler(this.undoToolStripMenuItem_Click);

            this.undoToolStripButton.Text = "Undo";
            this.undoToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.undoToolStripButton.Click += new EventHandler(this.undoToolStripButton_Click);

            // toolStrip1
            this.toolStrip1.Items.AddRange(new ToolStripItem[] {
                this.undoToolStripButton,
                new ToolStripSeparator(),
                this.zoomInToolStripButton,
                this.zoomOutToolStripButton,
                this.zoomLabel,
                this.zoomComboBox,
                this.toggleLabelsToolStripButton,
                this.editSelectedToolStripButton,
                this.duplicateSelectedToolStripButton
            });
            this.toolStrip1.Location = new Point(0, 24);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new Size(1000, 25);
            this.toolStrip1.BackColor = Color.WhiteSmoke;

            this.zoomInToolStripButton.Text = "Zoom In +";
            this.zoomInToolStripButton.Click += new EventHandler(this.zoomInButton_Click);

            this.zoomOutToolStripButton.Text = "Zoom Out -";
            this.zoomOutToolStripButton.Click += new EventHandler(this.zoomOutButton_Click);

            this.zoomLabel.Text = "Zoom:";

            this.zoomComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.zoomComboBox.Items.AddRange(new object[] { "0.5x", "1.0x", "1.5x", "2.0x", "3.0x" });
            this.zoomComboBox.SelectedIndex = 1;
            this.zoomComboBox.SelectedIndexChanged += new EventHandler(this.zoomComboBox_SelectedIndexChanged);

            this.toggleLabelsToolStripButton.Text = "Hide Labels";
            this.toggleLabelsToolStripButton.Click += new EventHandler(this.toggleLabelsToolStripButton_Click);

            this.editSelectedToolStripButton.Text = "Edit Selected";
            this.editSelectedToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.editSelectedToolStripButton.Click += new EventHandler(this.editSelectedToolStripButton_Click);

            this.duplicateSelectedToolStripButton.Text = "Duplicate";
            this.duplicateSelectedToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.duplicateSelectedToolStripButton.Click += new EventHandler(this.duplicateSelectedToolStripButton_Click);

            this.panelSidebarButtons.Dock = DockStyle.Top;
            this.panelSidebarButtons.Height = 32;
            this.panelSidebarButtons.FlowDirection = FlowDirection.LeftToRight;
            this.panelSidebarButtons.WrapContents = false;
            this.panelSidebarButtons.Padding = new Padding(3, 3, 3, 3);

            this.buttonHideAll.Text = "Hide All";
            this.buttonHideAll.Width = 80;
            this.buttonHideAll.Click += new EventHandler(this.buttonHideAll_Click);

            this.buttonShowAll.Text = "Show All";
            this.buttonShowAll.Width = 80;
            this.buttonShowAll.Click += new EventHandler(this.buttonShowAll_Click);

            this.buttonShowOnlySelected.Text = "Show Only";
            this.buttonShowOnlySelected.Width = 80;
            this.buttonShowOnlySelected.Click += new EventHandler(this.buttonShowOnlySelected_Click);

            this.panelSidebarButtons.Controls.Add(this.buttonHideAll);
            this.panelSidebarButtons.Controls.Add(this.buttonShowAll);
            this.panelSidebarButtons.Controls.Add(this.buttonShowOnlySelected);

            this.comboBoxRegionGroups = new ComboBox();
            this.comboBoxRegionGroups.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboBoxRegionGroups.Width = 160;
            this.comboBoxRegionGroups.SelectedIndexChanged += new EventHandler(this.comboBoxRegionGroups_SelectedIndexChanged);

            ToolStripControlHost spawnGroupHost = new ToolStripControlHost(this.comboBoxRegionGroups);
            spawnGroupHost.Margin = new Padding(10, 0, 0, 0);
            this.toolStrip1.Items.Add(spawnGroupHost);

            this.comboWorldFilter = new ComboBox();
            this.comboWorldFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboWorldFilter.Width = 110;
            this.comboWorldFilter.SelectedIndexChanged += new EventHandler(this.comboWorldFilter_SelectedIndexChanged);

            ToolStripControlHost worldFilterHost = new ToolStripControlHost(this.comboWorldFilter);
            worldFilterHost.Margin = new Padding(10, 0, 0, 0);
            this.toolStrip1.Items.Add(worldFilterHost);

            this.comboBoxWorlds = new ComboBox();
            this.comboBoxWorlds.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboBoxWorlds.Width = 110;
            this.comboBoxWorlds.SelectedIndexChanged += new EventHandler(this.comboBoxWorlds_SelectedIndexChanged);

            ToolStripControlHost worldSelectorHost = new ToolStripControlHost(this.comboBoxWorlds);
            worldSelectorHost.Margin = new Padding(10, 0, 0, 0);
            this.toolStrip1.Items.Add(worldSelectorHost);

            // panelRegionSidebar
            this.txtRegionSearch.Dock = DockStyle.Top;
            this.txtRegionSearch.PlaceholderText = "Search spawn regions...";
            this.txtRegionSearch.TextChanged += new EventHandler(this.txtRegionSearch_TextChanged);

            this.checkedListBoxRegions.Dock = DockStyle.Fill;
            this.checkedListBoxRegions.FormattingEnabled = true;
            this.checkedListBoxRegions.Font = new Font("Segoe UI", 7.0f);
            this.checkedListBoxRegions.HorizontalScrollbar = true;

            this.panelRegionSidebar.Controls.Add(this.checkedListBoxRegions);
            this.panelRegionSidebar.Controls.Add(this.panelSidebarButtons);
            this.panelRegionSidebar.Controls.Add(this.txtRegionSearch);
            this.panelRegionSidebar.Dock = DockStyle.Fill;
            this.panelRegionSidebar.BackColor = Color.WhiteSmoke;

            this.regionContextMenu = new ContextMenuStrip();
            this.editTagsMenuItem = new ToolStripMenuItem("Edit Tags", null, editTagsMenuItem_Click);
            this.compareTagsMenuItem = new ToolStripMenuItem("Compare With...", null, compareTagsMenuItem_Click);

            this.regionContextMenu.Items.AddRange(new ToolStripItem[] {
                this.editTagsMenuItem,
                this.compareTagsMenuItem
            });

            this.checkedListBoxRegions.ContextMenuStrip = this.regionContextMenu;
            this.checkedListBoxRegions.MouseDown += checkedListBoxRegions_MouseDown;
            this.checkedListBoxRegions.SelectedIndexChanged += checkedListBoxRegions_SelectedIndexChanged;
            this.checkedListBoxRegions.DoubleClick += checkedListBoxRegions_DoubleClick;


            // panelSelectionDetails
            this.panelSelectionDetails.Dock = DockStyle.Fill;
            this.panelSelectionDetails.BackColor = Color.WhiteSmoke;
            this.panelSelectionDetails.Padding = new Padding(8);

            this.labelSelectedRegionHeader.Text = "Selected Spawn Region";
            this.labelSelectedRegionHeader.Dock = DockStyle.Top;
            this.labelSelectedRegionHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            this.labelSelectedRegionHeader.Height = 24;

            this.tableLayoutSelectedRegionDetails.Dock = DockStyle.Top;
            this.tableLayoutSelectedRegionDetails.AutoSize = true;
            this.tableLayoutSelectedRegionDetails.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.tableLayoutSelectedRegionDetails.ColumnCount = 1;
            this.tableLayoutSelectedRegionDetails.RowCount = 18;
            this.tableLayoutSelectedRegionDetails.Padding = new Padding(0, 4, 0, 0);

            AddDetailsField("Name", this.txtSelectedName);
            AddDetailsField("Region ID", this.txtSelectedRegionNumber);
            AddDetailsField("World", this.txtSelectedWorld);
            AddDetailsField("Source File", this.txtSelectedSourceFile);
            AddDetailsField("Spawn Source", this.txtSelectedSpawnSource);
            AddDetailsField("Counts", this.txtSelectedCounts);
            AddDetailsField("Respawn", this.txtSelectedRespawn);
            AddDetailsField("Z Values", this.txtSelectedZ);
            AddDetailsField("Bounds", this.txtSelectedBounds);

            Label rawLabel = new Label();
            rawLabel.Text = "Raw Spawn Text";
            rawLabel.AutoSize = true;
            rawLabel.Margin = new Padding(0, 8, 0, 4);
            rawLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            this.txtSelectedRaw.Dock = DockStyle.Fill;
            this.txtSelectedRaw.Multiline = true;
            this.txtSelectedRaw.ScrollBars = ScrollBars.Both;
            this.txtSelectedRaw.WordWrap = false;
            this.txtSelectedRaw.ReadOnly = true;
            this.txtSelectedRaw.Font = new Font("Consolas", 8.5F);
            this.txtSelectedRaw.BackColor = Color.White;
            this.txtSelectedRaw.Height = 260;

            this.panelSelectionDetails.Controls.Add(this.txtSelectedRaw);
            this.panelSelectionDetails.Controls.Add(rawLabel);
            this.panelSelectionDetails.Controls.Add(this.tableLayoutSelectedRegionDetails);
            this.panelSelectionDetails.Controls.Add(this.labelSelectedRegionHeader);

            // pictureBox1
            this.pictureBox1.Dock = DockStyle.Fill;
            this.pictureBox1.BackColor = Color.DimGray;
            this.pictureBox1.MouseWheel += pictureBox1_MouseWheel;
            this.pictureBox1.MouseDoubleClick += pictureBox1_MouseDoubleClick;

            // mainLayout
            this.mainLayout.Dock = DockStyle.Fill;
            this.mainLayout.ColumnCount = 3;
            this.mainLayout.RowCount = 3;
            this.mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            this.mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            this.mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            this.mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));

            this.mainLayout.Controls.Add(this.menuStrip1, 0, 0);
            this.mainLayout.SetColumnSpan(this.menuStrip1, 3);
            this.mainLayout.Controls.Add(this.toolStrip1, 0, 1);
            this.mainLayout.SetColumnSpan(this.toolStrip1, 3);
            this.mainLayout.Controls.Add(this.panelRegionSidebar, 0, 2);
            this.mainLayout.Controls.Add(this.pictureBox1, 1, 2);
            this.mainLayout.Controls.Add(this.panelSelectionDetails, 2, 2);


            // MainForm
            this.ClientSize = new Size(1320, 700);
            this.Controls.Add(this.mainLayout);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainForm";
            //this.Text = "UOX3 Spawn Editor v0.1.6.Alpha";

            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
        }

        private void AddDetailsField(string labelText, TextBox textBox)
        {
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Margin = new Padding(0, 6, 0, 2);
            label.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            textBox.ReadOnly = true;
            textBox.Dock = DockStyle.Top;
            textBox.BackColor = Color.White;
            textBox.Margin = new Padding(0, 0, 0, 2);

            this.tableLayoutSelectedRegionDetails.Controls.Add(label);
            this.tableLayoutSelectedRegionDetails.Controls.Add(textBox);
        }
    }
}