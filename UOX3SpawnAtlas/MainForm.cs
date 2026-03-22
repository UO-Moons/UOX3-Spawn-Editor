using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UOX3SpawnAtlas
{
    public partial class MainForm : Form
    {
        private Bitmap originalImage;
        private float zoomFactor = 1.0f;
        private List<SpawnRegion> spawnRegions = new List<SpawnRegion>();
        private List<SpawnRegion> visibleSpawnRegions = new List<SpawnRegion>();
        private bool showSpawnRegions = true;
        private Point panOffset = new Point(0, 0);
        private Point mouseDownPos;
        private SpawnRegion selectedSpawnRegion = null;
        private Rectangle selectedRect = Rectangle.Empty;
        private Point regionDragStart;
        private bool isMovingRegion = false;
        private bool isCreatingRegion = false;
        private Rectangle newRegionRect;
        private bool isPanning = false;
        private bool isResizing = false;
        private ResizeHandle activeHandle = ResizeHandle.None;
        private string lastSpawnPath;
        private Dictionary<int, string> worldMapPaths = new Dictionary<int, string>();
        private Stack<List<SpawnRegion>> undoStack = new Stack<List<SpawnRegion>>();
        private int currentWorld = 0;
        private Dictionary<int, Image> worldMaps = new Dictionary<int, Image>();
        private string lastSpawnFolderPath;
        private bool loadedFromFolder = false;
        private Dictionary<int, string> activeSpawnFileByWorld = new Dictionary<int, string>();
        private LabelDisplayMode labelDisplayMode = LabelDisplayMode.SelectedOnly;
        private bool spacePanMode = false;
        private bool isLoadingSpawnData = false;
        private bool suppressWorldFilterReload = false;
        private StatusStrip loadingStatusStrip;
        private ToolStripStatusLabel loadingStatusLabel;
        private ToolStripProgressBar loadingProgressBar;
        private AppTheme currentTheme = AppTheme.Dark;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        private void ApplyWindowTitleBarTheme()
        {
            if (!IsHandleCreated)
                return;

            int useDarkMode = currentTheme == AppTheme.Dark ? 1 : 0;

            try
            {
                DwmSetWindowAttribute(
                    this.Handle,
                    DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref useDarkMode,
                    Marshal.SizeOf(typeof(int))
                );
            }
            catch
            {
            }
        }

        private enum ResizeHandle
        {
            None,
            TopLeft,
            Top,
            TopRight,
            Right,
            BottomRight,
            Bottom,
            BottomLeft,
            Left
        }

        private enum LabelDisplayMode
        {
            Hidden,
            SelectedOnly,
            AllVisible
        }

        private enum AppTheme
        {
            Light,
            Dark
        }

        private readonly Dictionary<int, string> worldNameMap = new Dictionary<int, string>
        {
            { 0, "Felucca" },
            { 1, "Trammel" },
            { 2, "Ilshenar" },
            { 3, "Malas" },
            { 4, "Tokuno" },
            { 5, "Ter Mur" }
        };

        private readonly Dictionary<int, Size> worldMapDimensions = new Dictionary<int, Size>
        {
            { 0, new Size(7168, 4096) }, // Felucca
            { 1, new Size(7168, 4096) }, // Trammel
            { 2, new Size(2304, 1600) }, // Ilshenar
            { 3, new Size(2560, 2048) }, // Malas
            { 4, new Size(1448, 1448) }, // Tokuno
            { 5, new Size(1280, 4096) }  // Ter Mur
        };

        private readonly Dictionary<string, string> tagDescriptions = new Dictionary<string, string>
        {
            {"NAME", "Internal name for the spawn region"},
            {"WORLD", "World number this spawn region belongs to"},
            {"INSTANCEID", "Instance ID for instanced spawn regions"},
            {"NPC", "NPC section ID to spawn from creatures.dfn"},
            {"NPCLIST", "NPC list section ID to spawn from spawn lists"},
            {"ITEM", "Item section ID to spawn"},
            {"ITEMLIST", "Item list section ID to spawn from item lists"},
            {"MAXNPCS", "Maximum number of NPCs allowed alive in this spawn region"},
            {"MAXITEMS", "Maximum number of items allowed in this spawn region"},
            {"MINTIME", "Minimum respawn delay"},
            {"MAXTIME", "Maximum respawn delay"},
            {"CALL", "Number of spawn attempts per respawn cycle"},
            {"DEFZ", "Default Z to use for spawns"},
            {"PREFZ", "Preferred Z to use for spawns"},
            {"ONLYOUTSIDE", "If 1, only spawn outdoors"},
            {"FORCESPAWN", "If 1, force spawn checks"},
            {"ISSPAWNER", "If 1, treated as spawner region"},
            {"ADDSCRIPT", "Extra script to attach to spawned objects"},
            {"VALIDLANDPOS", "If 1, validate land spawn positions"},
            {"VALIDWATERPOS", "If 1, validate water spawn positions"}
        };

        private readonly string settingsPath = Path.Combine(Application.StartupPath, "settings.json");
        private readonly string profilesPath = Path.Combine(Application.StartupPath, "profiles.json");
        private string currentProfileName = "Default";
        private bool suppressProfileMenuEvents = false;

        private class ProfileStore
        {
            public string ActiveProfile { get; set; } = "Default";
            public Dictionary<string, EditorSettings> Profiles { get; set; } = new Dictionary<string, EditorSettings>(StringComparer.OrdinalIgnoreCase);
        }

        private class EditorSettings
        {
            public Dictionary<int, string> MapPaths { get; set; } = new Dictionary<int, string>();
            public string SpawnPath { get; set; }
            public string SpawnFolderPath { get; set; }
            public bool LoadedFromFolder { get; set; }
            public bool ShowSpawnLabels { get; set; } = true;
            public int LabelDisplayMode { get; set; } = (int)MainForm.LabelDisplayMode.SelectedOnly;
            public List<string> HiddenSpawnRegions { get; set; } = new List<string>();
            public Dictionary<int, string> ActiveSpawnFileByWorld { get; set; } = new Dictionary<int, string>();
            public string Theme { get; set; } = AppTheme.Dark.ToString();
        }

        private class WorldItem
        {
            public int ID { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        private Size GetCurrentWorldDimensions()
        {
            Size worldSize;
            if (worldMapDimensions.TryGetValue(currentWorld, out worldSize))
                return worldSize;

            return new Size(7168, 4096);
        }

        public MainForm()
        {
            InitializeComponent();

            try
            {
                using (MemoryStream stream = new MemoryStream(Properties.Resources.UOX3SpawnAtlas))
                {
                    this.Icon = new Icon(stream);
                }
            }
            catch
            {
            }

            checkedListBoxRegions.ItemCheck += checkedListBoxRegions_ItemCheck;

            pictureBox1.MouseDown += pictureBox1_MouseDown;
            pictureBox1.MouseMove += pictureBox1_MouseMove;
            pictureBox1.MouseUp += pictureBox1_MouseUp;
            pictureBox1.MouseWheel += pictureBox1_MouseWheel;
            pictureBox1.Paint += pictureBox1_Paint;
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;

            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
            this.KeyUp += MainForm_KeyUp;

            this.Shown += MainForm_Shown;

            InitializeLoadingStatusStrip();
            ApplyTheme();
            AddNewSpawnFileMenuItem();
            PopulateProfileMenu();
            PopulateSpawnGroups();
            UpdateZoomUI();
            UpdateUndoUI();
            UpdateSpawnLabelButtonText();
            ClearSelectedRegionDetails();
        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            UpdateWindowTitle();

            BeginInvoke(new Action(async delegate
            {
                await Task.Delay(100);
                await LoadSettingsAsync();

                Timer startupUpdateTimer = new Timer();
                startupUpdateTimer.Interval = 1500;
                startupUpdateTimer.Tick += delegate (object timerSender, EventArgs timerArgs)
                {
                    startupUpdateTimer.Stop();
                    startupUpdateTimer.Dispose();

                    if (!isLoadingSpawnData)
                        CheckForUpdates();
                };
                startupUpdateTimer.Start();
            }));
        }

        private void InitializeLoadingStatusStrip()
        {
            loadingStatusStrip = new StatusStrip();
            loadingStatusStrip.Dock = DockStyle.Bottom;
            loadingStatusStrip.SizingGrip = false;
            loadingStatusStrip.Visible = false;

            loadingStatusLabel = new ToolStripStatusLabel();
            loadingStatusLabel.Spring = true;
            loadingStatusLabel.TextAlign = ContentAlignment.MiddleLeft;

            loadingProgressBar = new ToolStripProgressBar();
            loadingProgressBar.AutoSize = false;
            loadingProgressBar.Width = 260;
            loadingProgressBar.Minimum = 0;
            loadingProgressBar.Maximum = 100;
            loadingProgressBar.Style = ProgressBarStyle.Continuous;

            loadingStatusStrip.Items.Add(loadingStatusLabel);
            loadingStatusStrip.Items.Add(loadingProgressBar);

            Controls.Add(loadingStatusStrip);
            loadingStatusStrip.BringToFront();
        }

        private void AddNewSpawnFileMenuItem()
        {
            if (fileToolStripMenuItem == null)
                return;

            ToolStripMenuItem newSpawnFileMenuItem = null;
            ToolStripMenuItem deleteSpawnFileMenuItem = null;

            foreach (ToolStripItem item in fileToolStripMenuItem.DropDownItems)
            {
                ToolStripMenuItem existingItem = item as ToolStripMenuItem;
                if (existingItem == null)
                    continue;

                if (existingItem.Name == "newSpawnFileToolStripMenuItem")
                    newSpawnFileMenuItem = existingItem;
                else if (existingItem.Name == "deleteSpawnFileToolStripMenuItem")
                    deleteSpawnFileMenuItem = existingItem;
            }

            if (newSpawnFileMenuItem == null)
            {
                newSpawnFileMenuItem = new ToolStripMenuItem();
                newSpawnFileMenuItem.Name = "newSpawnFileToolStripMenuItem";
                newSpawnFileMenuItem.Text = "New Spawn File";
                newSpawnFileMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.N;
                newSpawnFileMenuItem.Click += new EventHandler(newSpawnFileToolStripMenuItem_Click);
            }

            if (deleteSpawnFileMenuItem == null)
            {
                deleteSpawnFileMenuItem = new ToolStripMenuItem();
                deleteSpawnFileMenuItem.Name = "deleteSpawnFileToolStripMenuItem";
                deleteSpawnFileMenuItem.Text = "Delete Spawn File";
                deleteSpawnFileMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.Delete;
                deleteSpawnFileMenuItem.Click += new EventHandler(deleteSpawnFileToolStripMenuItem_Click);
            }

            int saveIndex = -1;
            for (int index = 0; index < fileToolStripMenuItem.DropDownItems.Count; index++)
            {
                if (fileToolStripMenuItem.DropDownItems[index] == saveRegionsToolStripMenuItem)
                {
                    saveIndex = index;
                    break;
                }
            }

            if (saveIndex < 0)
                saveIndex = fileToolStripMenuItem.DropDownItems.Count;

            if (!fileToolStripMenuItem.DropDownItems.Contains(newSpawnFileMenuItem))
                fileToolStripMenuItem.DropDownItems.Insert(saveIndex, newSpawnFileMenuItem);

            saveIndex = fileToolStripMenuItem.DropDownItems.IndexOf(saveRegionsToolStripMenuItem);
            if (saveIndex < 0)
                saveIndex = fileToolStripMenuItem.DropDownItems.Count;

            if (!fileToolStripMenuItem.DropDownItems.Contains(deleteSpawnFileMenuItem))
                fileToolStripMenuItem.DropDownItems.Insert(saveIndex, deleteSpawnFileMenuItem);
        }

        private void SetLoadingState(bool isVisible, string statusText, int currentValue, int maximumValue, bool marquee)
        {
            if (loadingStatusStrip == null)
                return;

            loadingStatusStrip.Visible = isVisible;
            loadingStatusLabel.Text = statusText;
            loadingProgressBar.Style = marquee ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;

            if (!marquee)
            {
                if (maximumValue < 1)
                    maximumValue = 1;

                if (currentValue < 0)
                    currentValue = 0;

                if (currentValue > maximumValue)
                    currentValue = maximumValue;

                loadingProgressBar.Maximum = maximumValue;
                loadingProgressBar.Value = currentValue;
            }

            UseWaitCursor = isVisible;
        }

        private void UpdateLoadingProgressSafe(int currentFile, int totalFiles, string fileName)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, int, string>(UpdateLoadingProgressSafe), currentFile, totalFiles, fileName);
                return;
            }

            string statusText = "Loading DFN files...";
            if (!string.IsNullOrWhiteSpace(fileName))
                statusText = "Loading " + fileName + " (" + currentFile + "/" + totalFiles + ")";

            SetLoadingState(true, statusText, currentFile, totalFiles, false);
        }

        private void ApplyLoadedSpawnRegions(List<SpawnRegion> loadedRegions, bool loadedFolder, string sourcePath)
        {
            spawnRegions = loadedRegions ?? new List<SpawnRegion>();
            selectedSpawnRegion = null;
            selectedRect = Rectangle.Empty;

            loadedFromFolder = loadedFolder;

            if (loadedFolder)
            {
                lastSpawnFolderPath = sourcePath;
                lastSpawnPath = null;
            }
            else
            {
                lastSpawnPath = sourcePath;
                lastSpawnFolderPath = null;
            }

            UpdateVisibleSpawnRegions();
            UpdateSpawnRegionListUI();
            PopulateSpawnGroups();
            ClearSelectedRegionDetails();
            pictureBox1.Invalidate();
            undoStack.Clear();
            UpdateUndoUI();
        }

        private async Task LoadSpawnFileAsync(string spawnFilePath)
        {
            if (isLoadingSpawnData)
                return;

            try
            {
                isLoadingSpawnData = true;
                SetLoadingState(true, "Loading " + Path.GetFileName(spawnFilePath) + "...", 0, 1, true);

                List<SpawnRegion> loadedRegions = await Task.Run(delegate
                {
                    return SpawnRegionParser.LoadSpawnRegions(spawnFilePath, -1, delegate (int currentFile, int totalFiles, string fileName)
                    {
                        UpdateLoadingProgressSafe(currentFile, totalFiles, fileName);
                    });
                });

                ApplyLoadedSpawnRegions(loadedRegions, false, spawnFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load spawn data:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isLoadingSpawnData = false;
                SetLoadingState(false, string.Empty, 0, 1, false);
            }
        }

        private async Task LoadSpawnFolderAsync(string spawnFolderPath)
        {
            if (isLoadingSpawnData)
                return;

            try
            {
                isLoadingSpawnData = true;
                SetLoadingState(true, "Scanning DFN files...", 0, 1, true);

                List<SpawnRegion> loadedRegions = await Task.Run(delegate
                {
                    return SpawnRegionParser.LoadSpawnRegionsFromFolder(spawnFolderPath, -1, true, delegate (int currentFile, int totalFiles, string fileName)
                    {
                        UpdateLoadingProgressSafe(currentFile, totalFiles, fileName);
                    });
                });

                ApplyLoadedSpawnRegions(loadedRegions, true, spawnFolderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load spawn folder:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isLoadingSpawnData = false;
                SetLoadingState(false, string.Empty, 0, 1, false);
            }
        }

        private string GetSpawnRegionVisibilityKey(SpawnRegion spawnRegion)
        {
            if (spawnRegion == null)
                return string.Empty;

            string sourceFilePath = spawnRegion.SourceFilePath ?? string.Empty;
            return spawnRegion.World + "|" + spawnRegion.RegionNum + "|" + sourceFilePath.ToLowerInvariant();
        }

        private EditorSettings CaptureCurrentEditorSettings()
        {
            return new EditorSettings
            {
                MapPaths = new Dictionary<int, string>(worldMapPaths),
                SpawnPath = lastSpawnPath,
                SpawnFolderPath = lastSpawnFolderPath,
                LoadedFromFolder = loadedFromFolder,
                ShowSpawnLabels = labelDisplayMode != LabelDisplayMode.Hidden,
                LabelDisplayMode = (int)labelDisplayMode,
                HiddenSpawnRegions = spawnRegions
                    .Where(region => !region.Visible)
                    .Select(region => GetSpawnRegionVisibilityKey(region))
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ActiveSpawnFileByWorld = new Dictionary<int, string>(activeSpawnFileByWorld),
                Theme = currentTheme.ToString()
            };
        }

        private EditorSettings CreateDefaultEditorSettings()
        {
            return new EditorSettings();
        }

        private ProfileStore LoadProfileStore()
        {
            try
            {
                if (File.Exists(profilesPath))
                {
                    string profilesJson = File.ReadAllText(profilesPath);
                    ProfileStore existingStore = JsonConvert.DeserializeObject<ProfileStore>(profilesJson);
                    if (existingStore == null)
                        existingStore = new ProfileStore();

                    if (existingStore.Profiles == null)
                        existingStore.Profiles = new Dictionary<string, EditorSettings>(StringComparer.OrdinalIgnoreCase);

                    if (existingStore.Profiles.Count == 0)
                        existingStore.Profiles["Default"] = CreateDefaultEditorSettings();

                    if (string.IsNullOrWhiteSpace(existingStore.ActiveProfile) || !existingStore.Profiles.ContainsKey(existingStore.ActiveProfile))
                        existingStore.ActiveProfile = existingStore.Profiles.Keys.First();

                    return existingStore;
                }

                ProfileStore migratedStore = new ProfileStore();

                if (File.Exists(settingsPath))
                {
                    string legacyJson = File.ReadAllText(settingsPath);
                    EditorSettings legacySettings = JsonConvert.DeserializeObject<EditorSettings>(legacyJson);

                    if (legacySettings != null)
                        migratedStore.Profiles["Default"] = legacySettings;
                    else
                        migratedStore.Profiles["Default"] = CreateDefaultEditorSettings();
                }
                else
                {
                    migratedStore.Profiles["Default"] = CreateDefaultEditorSettings();
                }

                migratedStore.ActiveProfile = "Default";
                SaveProfileStore(migratedStore);
                return migratedStore;
            }
            catch
            {
                ProfileStore fallbackStore = new ProfileStore();
                fallbackStore.Profiles["Default"] = CreateDefaultEditorSettings();
                fallbackStore.ActiveProfile = "Default";
                return fallbackStore;
            }
        }

        private void SaveProfileStore(ProfileStore profileStore)
        {
            if (profileStore == null)
                return;

            if (profileStore.Profiles == null)
                profileStore.Profiles = new Dictionary<string, EditorSettings>(StringComparer.OrdinalIgnoreCase);

            if (profileStore.Profiles.Count == 0)
                profileStore.Profiles["Default"] = CreateDefaultEditorSettings();

            if (string.IsNullOrWhiteSpace(profileStore.ActiveProfile) || !profileStore.Profiles.ContainsKey(profileStore.ActiveProfile))
                profileStore.ActiveProfile = profileStore.Profiles.Keys.First();

            string json = JsonConvert.SerializeObject(profileStore, Formatting.Indented);
            File.WriteAllText(profilesPath, json);
        }

        private void SaveSettings()
        {
            ProfileStore profileStore = LoadProfileStore();

            if (!profileStore.Profiles.ContainsKey(currentProfileName))
                profileStore.Profiles[currentProfileName] = CreateDefaultEditorSettings();

            profileStore.Profiles[currentProfileName] = CaptureCurrentEditorSettings();
            profileStore.ActiveProfile = currentProfileName;
            SaveProfileStore(profileStore);
            PopulateProfileMenu();
        }

        private void DisposeLoadedMaps()
        {
            foreach (Image mapImage in worldMaps.Values)
                mapImage.Dispose();

            worldMaps.Clear();
            worldMapPaths.Clear();
        }

        private void ResetLoadedProfileState()
        {
            DisposeLoadedMaps();

            originalImage = null;
            pictureBox1.Image = null;
            spawnRegions = new List<SpawnRegion>();
            visibleSpawnRegions = new List<SpawnRegion>();
            selectedSpawnRegion = null;
            selectedRect = Rectangle.Empty;
            lastSpawnPath = null;
            lastSpawnFolderPath = null;
            loadedFromFolder = false;
            activeSpawnFileByWorld = new Dictionary<int, string>();
            currentWorld = 0;
            panOffset = new Point(0, 0);
            zoomFactor = 1.0f;
            undoStack.Clear();

            suppressWorldFilterReload = true;
            if (comboWorldFilter != null)
                comboWorldFilter.Items.Clear();
            suppressWorldFilterReload = false;

            if (comboBoxWorlds != null)
                comboBoxWorlds.Items.Clear();

            ClearSelectedRegionDetails();
            UpdateZoomUI();
            UpdateUndoUI();
            PopulateSpawnGroups();
            UpdateSpawnRegionListUI();
            pictureBox1.Invalidate();
        }

        private async Task ApplyEditorSettingsAsync(EditorSettings settings)
        {
            if (settings == null)
                settings = CreateDefaultEditorSettings();

            ResetLoadedProfileState();

            if (!string.IsNullOrWhiteSpace(settings.Theme))
            {
                AppTheme loadedTheme;
                if (Enum.TryParse(settings.Theme, true, out loadedTheme))
                    currentTheme = loadedTheme;
                else
                    currentTheme = AppTheme.Dark;
            }
            else
            {
                currentTheme = AppTheme.Dark;
            }

            ApplyTheme();

            if (Enum.IsDefined(typeof(LabelDisplayMode), settings.LabelDisplayMode))
                labelDisplayMode = (LabelDisplayMode)settings.LabelDisplayMode;
            else
                labelDisplayMode = settings.ShowSpawnLabels ? LabelDisplayMode.SelectedOnly : LabelDisplayMode.Hidden;

            UpdateSpawnLabelButtonText();

            if (settings.MapPaths != null)
            {
                foreach (KeyValuePair<int, string> entry in settings.MapPaths)
                {
                    if (!File.Exists(entry.Value))
                        continue;

                    Image mapImage = Image.FromFile(entry.Value);
                    worldMaps[entry.Key] = mapImage;
                    worldMapPaths[entry.Key] = entry.Value;
                }

                RebuildWorldMapSelector();

                if (worldMaps.Count > 0)
                {
                    currentWorld = worldMaps.Keys.Min();
                    pictureBox1.Image = worldMaps[currentWorld];
                    originalImage = worldMaps[currentWorld] as Bitmap;
                }
            }

            lastSpawnPath = settings.SpawnPath;
            lastSpawnFolderPath = settings.SpawnFolderPath;
            loadedFromFolder = settings.LoadedFromFolder;
            activeSpawnFileByWorld = settings.ActiveSpawnFileByWorld != null
                ? new Dictionary<int, string>(settings.ActiveSpawnFileByWorld)
                : new Dictionary<int, string>();

            if (loadedFromFolder && !string.IsNullOrWhiteSpace(lastSpawnFolderPath) && Directory.Exists(lastSpawnFolderPath))
            {
                PopulateWorldFilterFromFolder(lastSpawnFolderPath);
                await LoadSpawnFolderAsync(lastSpawnFolderPath);
            }
            else if (!string.IsNullOrWhiteSpace(lastSpawnPath) && File.Exists(lastSpawnPath))
            {
                PopulateWorldFilter(lastSpawnPath);
                await LoadSpawnFileAsync(lastSpawnPath);
            }

            if (settings.HiddenSpawnRegions != null && settings.HiddenSpawnRegions.Count > 0)
            {
                HashSet<string> hiddenRegionKeys = new HashSet<string>(settings.HiddenSpawnRegions, StringComparer.OrdinalIgnoreCase);
                HashSet<string> legacyHiddenRegionNames = new HashSet<string>(
                    settings.HiddenSpawnRegions
                        .Where(entry => !string.IsNullOrWhiteSpace(entry) && entry.IndexOf('|') < 0),
                    StringComparer.OrdinalIgnoreCase
                );

                foreach (SpawnRegion spawnRegion in spawnRegions)
                {
                    string visibilityKey = GetSpawnRegionVisibilityKey(spawnRegion);
                    bool isHidden = hiddenRegionKeys.Contains(visibilityKey) || legacyHiddenRegionNames.Contains(spawnRegion.Name);
                    spawnRegion.Visible = !isHidden;
                }

                UpdateVisibleSpawnRegions();
                UpdateSpawnRegionListUI();
                pictureBox1.Invalidate();
            }

            UpdateWindowTitle();
            PopulateProfileMenu();
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                ProfileStore profileStore = await Task.Run(delegate
                {
                    return LoadProfileStore();
                });

                currentProfileName = profileStore.ActiveProfile;
                PopulateProfileMenu();

                EditorSettings activeSettings;
                if (!profileStore.Profiles.TryGetValue(currentProfileName, out activeSettings))
                    activeSettings = CreateDefaultEditorSettings();

                await ApplyEditorSettingsAsync(activeSettings);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load settings: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateProfileMenu()
        {
            if (profilesToolStripMenuItem == null || switchProfileToolStripMenuItem == null)
                return;

            suppressProfileMenuEvents = true;
            switchProfileToolStripMenuItem.DropDownItems.Clear();

            ProfileStore profileStore = LoadProfileStore();

            foreach (string profileName in profileStore.Profiles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                ToolStripMenuItem profileMenuItem = new ToolStripMenuItem(profileName);
                profileMenuItem.Tag = profileName;
                profileMenuItem.Checked = string.Equals(profileName, currentProfileName, StringComparison.OrdinalIgnoreCase);
                profileMenuItem.Click += switchProfileMenuItem_Click;
                switchProfileToolStripMenuItem.DropDownItems.Add(profileMenuItem);
            }

            switchProfileToolStripMenuItem.Enabled = switchProfileToolStripMenuItem.DropDownItems.Count > 0;
            renameProfileToolStripMenuItem.Enabled = !string.IsNullOrWhiteSpace(currentProfileName);
            deleteProfileToolStripMenuItem.Enabled = profileStore.Profiles.Count > 1;
            saveProfileToolStripMenuItem.Enabled = !string.IsNullOrWhiteSpace(currentProfileName);
            profilesToolStripMenuItem.Text = "Profiles (" + currentProfileName + ")";
            suppressProfileMenuEvents = false;
        }

        private string PromptForProfileName(string titleText, string promptText, string defaultValue)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox(promptText, titleText, defaultValue ?? string.Empty);
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return input.Trim();
        }

        private async Task SwitchToProfileAsync(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                return;

            if (string.Equals(profileName, currentProfileName, StringComparison.OrdinalIgnoreCase))
                return;

            SaveSettings();

            ProfileStore profileStore = LoadProfileStore();
            EditorSettings targetSettings;
            if (!profileStore.Profiles.TryGetValue(profileName, out targetSettings))
                return;

            currentProfileName = profileName;
            profileStore.ActiveProfile = profileName;
            SaveProfileStore(profileStore);
            await ApplyEditorSettingsAsync(targetSettings);
        }

        private async void newProfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string newProfileName = PromptForProfileName("New Profile", "Enter a name for the new profile.", "New Profile");
            if (string.IsNullOrWhiteSpace(newProfileName))
                return;

            ProfileStore profileStore = LoadProfileStore();
            if (profileStore.Profiles.ContainsKey(newProfileName))
            {
                MessageBox.Show("A profile with that name already exists.", "Profile Exists", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveSettings();

            profileStore = LoadProfileStore();
            profileStore.Profiles[newProfileName] = CreateDefaultEditorSettings();
            profileStore.ActiveProfile = newProfileName;
            SaveProfileStore(profileStore);

            currentProfileName = newProfileName;
            PopulateProfileMenu();
            await ApplyEditorSettingsAsync(profileStore.Profiles[newProfileName]);
        }

        private void saveProfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSettings();
            MessageBox.Show("Saved profile: " + currentProfileName, "Profile Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void renameProfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProfileStore profileStore = LoadProfileStore();
            if (string.IsNullOrWhiteSpace(currentProfileName) || !profileStore.Profiles.ContainsKey(currentProfileName))
                return;

            string renamedProfileName = PromptForProfileName("Rename Profile", "Enter a new profile name.", currentProfileName);
            if (string.IsNullOrWhiteSpace(renamedProfileName))
                return;

            if (string.Equals(renamedProfileName, currentProfileName, StringComparison.OrdinalIgnoreCase))
                return;

            if (profileStore.Profiles.ContainsKey(renamedProfileName))
            {
                MessageBox.Show("A profile with that name already exists.", "Profile Exists", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveSettings();
            profileStore = LoadProfileStore();

            EditorSettings currentSettings = profileStore.Profiles[currentProfileName];
            profileStore.Profiles.Remove(currentProfileName);
            profileStore.Profiles[renamedProfileName] = currentSettings;
            profileStore.ActiveProfile = renamedProfileName;
            SaveProfileStore(profileStore);

            currentProfileName = renamedProfileName;
            PopulateProfileMenu();
            UpdateWindowTitle();

            await Task.CompletedTask;
        }

        private async void deleteProfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProfileStore profileStore = LoadProfileStore();
            if (profileStore.Profiles.Count <= 1)
            {
                MessageBox.Show("You must keep at least one profile.", "Delete Profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(currentProfileName) || !profileStore.Profiles.ContainsKey(currentProfileName))
                return;

            DialogResult result = MessageBox.Show(
                "Delete profile '" + currentProfileName + "'?",
                "Delete Profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result != DialogResult.Yes)
                return;

            string profileToDelete = currentProfileName;
            string fallbackProfileName = profileStore.Profiles.Keys
                .Where(name => !string.Equals(name, profileToDelete, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .First();

            profileStore.Profiles.Remove(profileToDelete);
            profileStore.ActiveProfile = fallbackProfileName;
            SaveProfileStore(profileStore);

            currentProfileName = fallbackProfileName;
            PopulateProfileMenu();
            await ApplyEditorSettingsAsync(profileStore.Profiles[fallbackProfileName]);
        }

        private async void switchProfileMenuItem_Click(object sender, EventArgs e)
        {
            if (suppressProfileMenuEvents)
                return;

            ToolStripMenuItem clickedMenuItem = sender as ToolStripMenuItem;
            if (clickedMenuItem == null)
                return;

            string targetProfileName = clickedMenuItem.Tag as string;
            await SwitchToProfileAsync(targetProfileName);
        }

        private void PushUndo()
        {
            if (spawnRegions == null || spawnRegions.Count == 0)
                return;

            undoStack.Push(spawnRegions.Select(region => region.Clone()).ToList());
            UpdateUndoUI();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z))
            {
                UndoLastAction();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.N))
            {
                CreateNewSpawnFileForCurrentWorld();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.Delete))
            {
                DeleteSpawnFileForCurrentWorld();
                return true;
            }

            if (keyData == Keys.F2)
            {
                RenameSelectedSpawnRegion();
                return true;
            }

            if (keyData == (Keys.Control | Keys.D))
            {
                DuplicateSelectedSpawnRegion();
                return true;
            }

            if (keyData == Keys.Delete)
            {
                if (selectedSpawnRegion != null)
                {
                    DeleteSelectedSpawnRegion();
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void UndoLastAction()
        {
            if (undoStack.Count <= 0)
                return;

            spawnRegions = undoStack.Pop();
            selectedSpawnRegion = null;
            selectedRect = Rectangle.Empty;
            ClearSelectedRegionDetails();

            UpdateVisibleSpawnRegions();
            UpdateSpawnRegionListUI();
            PopulateSpawnGroups();
            pictureBox1.Invalidate();
            UpdateUndoUI();
        }

        private void checkedListBoxRegions_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (suppressRegionCheckEvents)
                return;

            if (e.Index < 0 || e.Index >= visibleSpawnRegions.Count)
                return;

            SpawnRegion changedRegion = visibleSpawnRegions[e.Index];
            bool newVisibleState = (e.NewValue == CheckState.Checked);

            changedRegion.Visible = newVisibleState;

            BeginInvoke((Action)(() =>
            {
                if (selectedSpawnRegion == changedRegion && !changedRegion.Visible)
                {
                    selectedSpawnRegion = null;
                    selectedRect = Rectangle.Empty;
                    ClearSelectedRegionDetails();
                }

                pictureBox1.Invalidate();
                SaveSettings();
            }));
        }

        private void UpdateVisibleSpawnRegions()
        {
            string searchFilter = string.Empty;
            if (txtRegionSearch != null && txtRegionSearch.Text != null)
                searchFilter = txtRegionSearch.Text.Trim().ToLowerInvariant();

            string selectedGroup = comboBoxRegionGroups != null && comboBoxRegionGroups.SelectedItem != null
                ? comboBoxRegionGroups.SelectedItem.ToString()
                : "All Spawn Regions";

            visibleSpawnRegions = new List<SpawnRegion>();

            foreach (SpawnRegion spawnRegion in spawnRegions)
            {
                if (spawnRegion.World != currentWorld)
                    continue;

                bool groupMatch = selectedGroup == "All Spawn Regions" ||
                                  (selectedGroup == "NPC Spawns" && !string.IsNullOrWhiteSpace(spawnRegion.Npc)) ||
                                  (selectedGroup == "NPC List Spawns" && !string.IsNullOrWhiteSpace(spawnRegion.NpcList)) ||
                                  (selectedGroup == "Item Spawns" && !string.IsNullOrWhiteSpace(spawnRegion.Item)) ||
                                  (selectedGroup == "Item List Spawns" && !string.IsNullOrWhiteSpace(spawnRegion.ItemList)) ||
                                  (selectedGroup == "Missing Source" && spawnRegion.GetSpawnSource() == "(No Spawn Source)") ||
                                  (selectedGroup == "Towns" && IsTownSpawn(spawnRegion)) ||
                                  (selectedGroup == "Dungeons" && IsDungeonSpawn(spawnRegion)) ||
                                  (selectedGroup == "Wilderness" && IsWildernessSpawn(spawnRegion)) ||
                                  selectedGroup == ("World " + spawnRegion.World);

                string displayName = BuildDisplayName(spawnRegion).ToLowerInvariant();
                bool searchMatch = string.IsNullOrWhiteSpace(searchFilter) || displayName.Contains(searchFilter);

                if (groupMatch && searchMatch)
                    visibleSpawnRegions.Add(spawnRegion);
            }

            visibleSpawnRegions = visibleSpawnRegions
                .OrderBy(region => region.GetShortSourceFileName())
                .ThenBy(region => region.Name)
                .ThenBy(region => region.RegionNum)
                .ToList();
        }

        private bool suppressRegionCheckEvents = false;

        private void UpdateSpawnRegionListUI()
        {
            UpdateVisibleSpawnRegions();

            suppressRegionCheckEvents = true;

            checkedListBoxRegions.BeginUpdate();
            checkedListBoxRegions.Items.Clear();

            foreach (SpawnRegion spawnRegion in visibleSpawnRegions)
                checkedListBoxRegions.Items.Add(BuildDisplayName(spawnRegion), spawnRegion.Visible);

            int maxWidth = 0;
            using (Graphics graphics = checkedListBoxRegions.CreateGraphics())
            {
                foreach (object item in checkedListBoxRegions.Items)
                {
                    SizeF size = graphics.MeasureString(item.ToString(), checkedListBoxRegions.Font);
                    if ((int)size.Width > maxWidth)
                        maxWidth = (int)size.Width;
                }
            }

            checkedListBoxRegions.HorizontalExtent = maxWidth + 20;
            checkedListBoxRegions.EndUpdate();

            suppressRegionCheckEvents = false;
        }

        private string BuildDisplayName(SpawnRegion spawnRegion)
        {
            string sourceFile = spawnRegion.GetShortSourceFileName();

            if (string.IsNullOrWhiteSpace(sourceFile))
                return "[" + spawnRegion.RegionNum + "] " + spawnRegion.Name + " - " + spawnRegion.GetSpawnSource();

            return "[" + spawnRegion.RegionNum + "] " + spawnRegion.Name + " - " + spawnRegion.GetSpawnSource() + " - {" + sourceFile + "}";
        }

        private void LoadImage(string filePath)
        {
            originalImage?.Dispose();

            using (Bitmap tempImage = new Bitmap(filePath))
            {
                int maxDimension = 3000;
                float scale = Math.Min((float)maxDimension / tempImage.Width, (float)maxDimension / tempImage.Height);
                int newWidth = (int)(tempImage.Width * scale);
                int newHeight = (int)(tempImage.Height * scale);
                originalImage = new Bitmap(tempImage, newWidth, newHeight);
            }

            zoomFactor = 1.0f;
            UpdateZoomUI();
            pictureBox1.Invalidate();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (!worldMaps.ContainsKey(currentWorld))
                return;

            originalImage = worldMaps[currentWorld] as Bitmap;
            if (originalImage == null)
                return;

            e.Graphics.Clear(pictureBox1.BackColor);

            System.Drawing.Drawing2D.Matrix originalTransform = e.Graphics.Transform;
            System.Drawing.Drawing2D.Matrix transform = new System.Drawing.Drawing2D.Matrix();

            transform.Translate(panOffset.X, panOffset.Y);
            transform.Scale(zoomFactor, zoomFactor);
            e.Graphics.Transform = transform;

            e.Graphics.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);

            Size currentWorldSize = GetCurrentWorldDimensions();
            float scaleX = (float)originalImage.Width / currentWorldSize.Width;
            float scaleY = (float)originalImage.Height / currentWorldSize.Height;

            if (showSpawnRegions && visibleSpawnRegions != null)
            {
                using (Font drawFont = new Font("Arial", 10f / zoomFactor, FontStyle.Bold))
                using (Pen selectedPen = new Pen(Color.Lime, 2f / zoomFactor))
                using (Pen previewPen = new Pen(Color.Orange, 1f / zoomFactor))
                {
                    previewPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

                    foreach (SpawnRegion spawnRegion in visibleSpawnRegions)
                    {
                        if (!spawnRegion.Visible)
                            continue;

                        if (spawnRegion.World != currentWorld)
                            continue;

                        using (Pen regionPen = new Pen(GetSpawnRegionColor(spawnRegion), 1f / zoomFactor))
                        {
                            foreach (Rectangle rect in spawnRegion.Bounds)
                            {
                                RectangleF scaledRect = new RectangleF(
                                    rect.X * scaleX,
                                    rect.Y * scaleY,
                                    rect.Width * scaleX,
                                    rect.Height * scaleY
                                );

                                if (spawnRegion == selectedSpawnRegion && rect == selectedRect)
                                    e.Graphics.DrawRectangle(selectedPen, scaledRect.X, scaledRect.Y, scaledRect.Width, scaledRect.Height);
                                else
                                    e.Graphics.DrawRectangle(regionPen, scaledRect.X, scaledRect.Y, scaledRect.Width, scaledRect.Height);

                                if (spawnRegion == selectedSpawnRegion && rect == selectedRect)
                                {
                                    float handleSize = 6f / zoomFactor;

                                    DrawResizeHandle(e.Graphics, scaledRect.Left, scaledRect.Top, handleSize);
                                    DrawResizeHandle(e.Graphics, scaledRect.Left + scaledRect.Width / 2f, scaledRect.Top, handleSize);
                                    DrawResizeHandle(e.Graphics, scaledRect.Right, scaledRect.Top, handleSize);
                                    DrawResizeHandle(e.Graphics, scaledRect.Right, scaledRect.Top + scaledRect.Height / 2f, handleSize);
                                    DrawResizeHandle(e.Graphics, scaledRect.Right, scaledRect.Bottom, handleSize);
                                    DrawResizeHandle(e.Graphics, scaledRect.Left + scaledRect.Width / 2f, scaledRect.Bottom, handleSize);
                                    DrawResizeHandle(e.Graphics, scaledRect.Left, scaledRect.Bottom, handleSize);
                                    DrawResizeHandle(e.Graphics, scaledRect.Left, scaledRect.Top + scaledRect.Height / 2f, handleSize);
                                }

                                if (ShouldDrawLabelForRegion(spawnRegion))
                                {
                                    string labelText = "[" + spawnRegion.RegionNum + "] " + spawnRegion.Name;
                                    SizeF textSize = e.Graphics.MeasureString(labelText, drawFont);
                                    Brush labelBrush = spawnRegion == selectedSpawnRegion ? Brushes.LightGreen : Brushes.Yellow;
                                    e.Graphics.DrawString(labelText, drawFont, labelBrush, scaledRect.X, scaledRect.Y - textSize.Height);
                                }
                            }
                        }
                    }

                    if (isCreatingRegion)
                    {
                        RectangleF previewRect = new RectangleF(
                            newRegionRect.X * scaleX,
                            newRegionRect.Y * scaleY,
                            newRegionRect.Width * scaleX,
                            newRegionRect.Height * scaleY
                        );

                        e.Graphics.DrawRectangle(previewPen, previewRect.X, previewRect.Y, previewRect.Width, previewRect.Height);
                    }
                }
            }

            e.Graphics.Transform = originalTransform;
        }

        private Color GetSpawnRegionColor(SpawnRegion spawnRegion)
        {
            if (!string.IsNullOrWhiteSpace(spawnRegion.Npc))
                return Color.Red;

            if (!string.IsNullOrWhiteSpace(spawnRegion.NpcList))
                return Color.DarkMagenta;

            if (!string.IsNullOrWhiteSpace(spawnRegion.Item))
                return Color.DodgerBlue;

            if (!string.IsNullOrWhiteSpace(spawnRegion.ItemList))
                return Color.Teal;

            return Color.Orange;
        }

        private void RebuildWorldMapSelector()
        {
            comboBoxWorlds.Items.Clear();

            foreach (int worldNumber in worldMaps.Keys.OrderBy(world => world))
            {
                string worldName = worldNameMap.ContainsKey(worldNumber)
                    ? worldNameMap[worldNumber]
                    : "World " + worldNumber;

                comboBoxWorlds.Items.Add(new WorldItem
                {
                    ID = worldNumber,
                    Name = worldName
                });
            }

            WorldItem matchingWorld = comboBoxWorlds.Items
                .OfType<WorldItem>()
                .FirstOrDefault(item => item.ID == currentWorld);

            if (matchingWorld != null)
                comboBoxWorlds.SelectedItem = matchingWorld;
            else if (comboBoxWorlds.Items.Count > 0)
                comboBoxWorlds.SelectedIndex = 0;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "Map Images|*.bmp;*.png;*.jpg";
                openDialog.Title = "Load Map Image";

                if (openDialog.ShowDialog() != DialogResult.OK)
                    return;

                string input = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter world number for this map (for example: 0, 1, 2)",
                    "Assign World"
                );

                int worldNum;
                if (!int.TryParse(input, out worldNum))
                    return;

                Image mapImage = Image.FromFile(openDialog.FileName);
                worldMaps[worldNum] = mapImage;
                worldMapPaths[worldNum] = openDialog.FileName;

                currentWorld = worldNum;
                originalImage = mapImage as Bitmap;

                RebuildWorldMapSelector();
                UpdateSpawnRegionListUI();
                pictureBox1.Invalidate();
                SaveSettings();
            }
        }

        private void comboBoxWorlds_SelectedIndexChanged(object sender, EventArgs e)
        {
            WorldItem selectedWorld = comboBoxWorlds.SelectedItem as WorldItem;
            if (selectedWorld == null)
                return;

            currentWorld = selectedWorld.ID;

            if (worldMaps.ContainsKey(currentWorld))
            {
                pictureBox1.Image = worldMaps[currentWorld];
                originalImage = worldMaps[currentWorld] as Bitmap;
            }

            SelectWorldFilterItem(currentWorld);
            UpdateSpawnRegionListUI();
            pictureBox1.Invalidate();
        }

        private void zoomInButton_Click(object sender, EventArgs e)
        {
            zoomFactor += 0.1f;
            ClampZoom();
            UpdateZoomUI();
            pictureBox1.Invalidate();
        }

        private void zoomOutButton_Click(object sender, EventArgs e)
        {
            zoomFactor -= 0.1f;
            ClampZoom();
            UpdateZoomUI();
            pictureBox1.Invalidate();
        }

        private void zoomComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (zoomComboBox.SelectedItem == null)
                return;

            string selected = zoomComboBox.SelectedItem.ToString().Replace("x", string.Empty);

            float parsedZoom;
            if (!float.TryParse(selected, out parsedZoom))
                return;

            zoomFactor = parsedZoom;
            ClampZoom();
            pictureBox1.Invalidate();
        }

        private void ClampZoom()
        {
            if (zoomFactor < 0.1f)
                zoomFactor = 0.1f;

            if (zoomFactor > 5.0f)
                zoomFactor = 5.0f;
        }

        private void UpdateZoomUI()
        {
            string zoomString = Math.Round(zoomFactor, 1).ToString("0.0") + "x";

            if (!zoomComboBox.Items.Contains(zoomString))
                zoomComboBox.Items.Add(zoomString);

            zoomComboBox.SelectedItem = zoomString;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            originalImage?.Dispose();
            base.OnFormClosing(e);
        }

        private async void LoadSpawnFile(string spawnFilePath)
        {
            await LoadSpawnFileAsync(spawnFilePath);
        }

        private async void LoadSpawnFolder(string spawnFolderPath)
        {
            await LoadSpawnFolderAsync(spawnFolderPath);
        }

        private int GetSelectedWorldFilterValue()
        {
            if (comboWorldFilter.SelectedItem != null)
            {
                string selectedText = comboWorldFilter.SelectedItem.ToString();
                if (selectedText.StartsWith("World "))
                {
                    int parsedWorld;
                    if (int.TryParse(selectedText.Split(' ')[1], out parsedWorld))
                        return parsedWorld;
                }
            }

            return -1;
        }

        private string GetActiveSpawnFilePathForWorld(int worldNumber)
        {
            string targetFilePath;

            if (activeSpawnFileByWorld.TryGetValue(worldNumber, out targetFilePath))
            {
                if (!string.IsNullOrWhiteSpace(targetFilePath))
                    return targetFilePath;
            }

            SpawnRegion firstSameWorldRegion = spawnRegions.FirstOrDefault(region =>
                region.World == worldNumber && !string.IsNullOrWhiteSpace(region.SourceFilePath));

            if (firstSameWorldRegion != null)
                return firstSameWorldRegion.SourceFilePath;

            if (!string.IsNullOrWhiteSpace(lastSpawnFolderPath) && Directory.Exists(lastSpawnFolderPath))
                return Path.Combine(lastSpawnFolderPath, "world" + worldNumber + "_custom.dfn");

            if (!string.IsNullOrWhiteSpace(lastSpawnPath))
                return lastSpawnPath;

            return string.Empty;
        }

        private string GetActiveSpawnFilePathForCurrentWorld()
        {
            return GetActiveSpawnFilePathForWorld(currentWorld);
        }

        private void CreateNewSpawnFileForCurrentWorld()
        {
            string baseFolder = null;

            if (!string.IsNullOrWhiteSpace(lastSpawnFolderPath) && Directory.Exists(lastSpawnFolderPath))
                baseFolder = lastSpawnFolderPath;
            else if (!string.IsNullOrWhiteSpace(lastSpawnPath) && File.Exists(lastSpawnPath))
                baseFolder = Path.GetDirectoryName(lastSpawnPath);

            if (string.IsNullOrWhiteSpace(baseFolder) || !Directory.Exists(baseFolder))
            {
                MessageBox.Show(
                    "Load a spawn folder or spawn file first so the new DFN file has a valid location.",
                    "New Spawn File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            string suggestedFileName = "world" + currentWorld + "_custom.dfn";

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Spawn Files|*.dfn";
                saveDialog.Title = "Create New Spawn File";
                saveDialog.InitialDirectory = baseFolder;
                saveDialog.FileName = suggestedFileName;
                saveDialog.OverwritePrompt = false;

                if (saveDialog.ShowDialog() != DialogResult.OK)
                    return;

                string newFilePath = saveDialog.FileName;

                if (!File.Exists(newFilePath))
                    File.WriteAllText(newFilePath, string.Empty);

                activeSpawnFileByWorld[currentWorld] = newFilePath;

                if (loadedFromFolder)
                {
                    lastSpawnFolderPath = baseFolder;
                    PopulateWorldFilterFromFolder(baseFolder);
                }
                else
                {
                    lastSpawnPath = newFilePath;
                    PopulateWorldFilter(newFilePath);
                }

                SaveSettings();
                UpdateWindowTitle();

                MessageBox.Show(
                    "Created new spawn file for World " + currentWorld + ":\n" + Path.GetFileName(newFilePath) +
                    "\n\nNew spawn regions on this world will now go into that file.",
                    "New Spawn File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        private void DeleteSpawnFileForCurrentWorld()
        {
            List<string> candidateFiles = spawnRegions
                .Where(region => region.World == currentWorld && !string.IsNullOrWhiteSpace(region.SourceFilePath))
                .Select(region => region.SourceFilePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => Path.GetFileName(path))
                .ToList();

            string activeFilePath = GetActiveSpawnFilePathForCurrentWorld();

            if (!string.IsNullOrWhiteSpace(activeFilePath) &&
                !candidateFiles.Contains(activeFilePath, StringComparer.OrdinalIgnoreCase) &&
                File.Exists(activeFilePath))
            {
                candidateFiles.Insert(0, activeFilePath);
            }

            if (candidateFiles.Count == 0)
            {
                MessageBox.Show(
                    "There are no spawn files available to delete for the current world.",
                    "Delete Spawn File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            string selectedFilePath = PromptForSpawnFileToDelete(candidateFiles, activeFilePath);
            if (string.IsNullOrWhiteSpace(selectedFilePath))
                return;

            DeleteSpawnFileByPath(selectedFilePath);
        }

        private void DeleteSpawnFileByPath(string targetFilePath)
        {
            if (string.IsNullOrWhiteSpace(targetFilePath))
                return;

            string fileName = Path.GetFileName(targetFilePath);

            int regionsInFile = spawnRegions.Count(region =>
                string.Equals(region.SourceFilePath, targetFilePath, StringComparison.OrdinalIgnoreCase));

            DialogResult result = MessageBox.Show(
                "Delete spawn file '" + fileName + "'?\n\n" +
                "This will remove " + regionsInFile + " loaded spawn region(s) that belong to that file and delete the DFN from disk.",
                "Delete Spawn File",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result != DialogResult.Yes)
                return;

            PushUndo();

            spawnRegions.RemoveAll(region =>
                string.Equals(region.SourceFilePath, targetFilePath, StringComparison.OrdinalIgnoreCase));

            if (selectedSpawnRegion != null &&
                string.Equals(selectedSpawnRegion.SourceFilePath, targetFilePath, StringComparison.OrdinalIgnoreCase))
            {
                selectedSpawnRegion = null;
                selectedRect = Rectangle.Empty;
                ClearSelectedRegionDetails();
            }

            try
            {
                if (File.Exists(targetFilePath))
                    File.Delete(targetFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to delete spawn file:\n" + ex.Message,
                    "Delete Spawn File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            List<int> worldsToClear = activeSpawnFileByWorld
                .Where(entry => string.Equals(entry.Value, targetFilePath, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Key)
                .ToList();

            foreach (int worldNumber in worldsToClear)
                activeSpawnFileByWorld.Remove(worldNumber);

            if (loadedFromFolder)
            {
                if (!string.IsNullOrWhiteSpace(lastSpawnFolderPath) && Directory.Exists(lastSpawnFolderPath))
                    PopulateWorldFilterFromFolder(lastSpawnFolderPath);
            }
            else if (string.Equals(lastSpawnPath, targetFilePath, StringComparison.OrdinalIgnoreCase))
            {
                lastSpawnPath = null;
            }

            PopulateSpawnGroups();
            UpdateSpawnRegionListUI();
            pictureBox1.Invalidate();
            SaveSettings();
            UpdateWindowTitle();

            MessageBox.Show(
                "Deleted spawn file '" + fileName + "'.",
                "Delete Spawn File",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private string PromptForSpawnFileToDelete(List<string> filePaths, string activeFilePath)
        {
            Form picker = new Form();
            ApplyThemeToDialog(picker);
            picker.Text = "Select Spawn File to Delete";
            picker.Width = 520;
            picker.Height = 420;
            picker.StartPosition = FormStartPosition.CenterParent;
            picker.MinimizeBox = false;
            picker.MaximizeBox = false;
            picker.FormBorderStyle = FormBorderStyle.SizableToolWindow;

            Label descriptionLabel = new Label();
            descriptionLabel.Text = "Choose the spawn file to delete for World " + currentWorld + ".";
            descriptionLabel.Left = 12;
            descriptionLabel.Top = 12;
            descriptionLabel.Width = 480;
            descriptionLabel.Height = 20;

            ListBox listBox = new ListBox();
            listBox.Left = 12;
            listBox.Top = 40;
            listBox.Width = 480;
            listBox.Height = 280;
            listBox.HorizontalScrollbar = true;

            foreach (string filePath in filePaths)
            {
                string displayName = Path.GetFileName(filePath);

                if (!string.IsNullOrWhiteSpace(activeFilePath) &&
                    string.Equals(filePath, activeFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    displayName += "  [Active]";
                }

                listBox.Items.Add(new SpawnFileListItem
                {
                    FilePath = filePath,
                    DisplayName = displayName
                });
            }

            Button deleteButton = new Button();
            deleteButton.Text = "Delete";
            deleteButton.Width = 100;
            deleteButton.Height = 28;
            deleteButton.Left = 392;
            deleteButton.Top = 332;
            deleteButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Width = 100;
            cancelButton.Height = 28;
            cancelButton.Left = 284;
            cancelButton.Top = 332;
            cancelButton.DialogResult = DialogResult.Cancel;

            picker.Controls.Add(descriptionLabel);
            picker.Controls.Add(listBox);
            picker.Controls.Add(deleteButton);
            picker.Controls.Add(cancelButton);

            picker.AcceptButton = deleteButton;
            picker.CancelButton = cancelButton;

            if (listBox.Items.Count > 0)
                listBox.SelectedIndex = 0;

            listBox.DoubleClick += delegate
            {
                if (listBox.SelectedItem != null)
                    picker.DialogResult = DialogResult.OK;
            };

            if (picker.ShowDialog(this) != DialogResult.OK)
                return string.Empty;

            SpawnFileListItem selectedItem = listBox.SelectedItem as SpawnFileListItem;
            if (selectedItem == null)
                return string.Empty;

            return selectedItem.FilePath;
        }


        private class NpcPickerItem
        {
            public string SectionID { get; set; }
            public string SourceFilePath { get; set; }

            public string DisplayName
            {
                get
                {
                    string sourceFileName = string.IsNullOrWhiteSpace(SourceFilePath) ? string.Empty : Path.GetFileName(SourceFilePath);
                    if (string.IsNullOrWhiteSpace(sourceFileName))
                        return SectionID;

                    return SectionID + "  {" + sourceFileName + "}";
                }
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private class NpcListPickerItem
        {
            public string SectionID { get; set; }
            public string SourceFilePath { get; set; }

            public string DisplayName
            {
                get
                {
                    string sourceFileName = string.IsNullOrWhiteSpace(SourceFilePath) ? string.Empty : Path.GetFileName(SourceFilePath);
                    if (string.IsNullOrWhiteSpace(sourceFileName))
                        return SectionID;

                    return SectionID + "  {" + sourceFileName + "}";
                }
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private class ItemListPickerItem
        {
            public string SectionID { get; set; }
            public string SourceFilePath { get; set; }

            public string DisplayName
            {
                get
                {
                    string sourceFileName = string.IsNullOrWhiteSpace(SourceFilePath) ? string.Empty : Path.GetFileName(SourceFilePath);
                    if (string.IsNullOrWhiteSpace(sourceFileName))
                        return SectionID;

                    return SectionID + "  {" + sourceFileName + "}";
                }
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private class ItemPickerItem
        {
            public string SectionID { get; set; }
            public string SourceFilePath { get; set; }
            public string DisplayNameText { get; set; }

            public string DisplayName
            {
                get
                {
                    string sourceFileName = string.IsNullOrWhiteSpace(SourceFilePath) ? string.Empty : Path.GetFileName(SourceFilePath);

                    if (!string.IsNullOrWhiteSpace(DisplayNameText))
                    {
                        if (string.IsNullOrWhiteSpace(sourceFileName))
                            return SectionID + " - " + DisplayNameText;

                        return SectionID + " - " + DisplayNameText + " {" + sourceFileName + "}";
                    }

                    if (string.IsNullOrWhiteSpace(sourceFileName))
                        return SectionID;

                    return SectionID + " {" + sourceFileName + "}";
                }
            }

            public bool MatchesSearch(string searchText)
            {
                if (string.IsNullOrWhiteSpace(searchText))
                    return true;

                string[] searchParts = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                for (int searchIndex = 0; searchIndex < searchParts.Length; searchIndex++)
                {
                    string searchPart = searchParts[searchIndex];
                    bool matchedThisPart = false;

                    if (!string.IsNullOrWhiteSpace(SectionID) && SectionID.IndexOf(searchPart, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedThisPart = true;
                    }

                    if (!matchedThisPart && !string.IsNullOrWhiteSpace(DisplayNameText) && DisplayNameText.IndexOf(searchPart, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedThisPart = true;
                    }

                    if (!matchedThisPart && !string.IsNullOrWhiteSpace(SourceFilePath))
                    {
                        string sourceFileName = Path.GetFileName(SourceFilePath);

                        if (!string.IsNullOrWhiteSpace(sourceFileName) && sourceFileName.IndexOf(searchPart, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedThisPart = true;
                        }
                    }

                    if (!matchedThisPart)
                        return false;
                }

                return true;
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private string GetNpcFolderPath()
        {
            List<string> candidateFolders = new List<string>();

            if (!string.IsNullOrWhiteSpace(lastSpawnFolderPath))
            {
                string spawnFolderParent = Directory.GetParent(lastSpawnFolderPath) != null ? Directory.GetParent(lastSpawnFolderPath).FullName : string.Empty;

                if (!string.IsNullOrWhiteSpace(spawnFolderParent))
                    candidateFolders.Add(Path.Combine(spawnFolderParent, "npc"));
            }

            if (!string.IsNullOrWhiteSpace(lastSpawnPath))
            {
                string spawnFileFolder = Path.GetDirectoryName(lastSpawnPath);
                if (!string.IsNullOrWhiteSpace(spawnFileFolder))
                {
                    string spawnFileFolderParent = Directory.GetParent(spawnFileFolder) != null ? Directory.GetParent(spawnFileFolder).FullName : string.Empty;

                    if (!string.IsNullOrWhiteSpace(spawnFileFolderParent))
                        candidateFolders.Add(Path.Combine(spawnFileFolderParent, "npc"));
                }
            }

            candidateFolders.Add(Path.Combine(Application.StartupPath, "data", "dfndata", "npc"));

            foreach (string candidateFolder in candidateFolders.Where(folder => !string.IsNullOrWhiteSpace(folder)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(candidateFolder))
                    return candidateFolder;
            }

            return string.Empty;
        }

        private List<NpcPickerItem> LoadNpcEntries()
        {
            List<NpcPickerItem> npcEntries = new List<NpcPickerItem>();
            string npcFolderPath = GetNpcFolderPath();

            if (string.IsNullOrWhiteSpace(npcFolderPath) || !Directory.Exists(npcFolderPath))
                return npcEntries;

            foreach (string filePath in Directory.GetFiles(npcFolderPath, "*.dfn", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(filePath);

                string relativePath = filePath.Substring(npcFolderPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (relativePath.StartsWith("npclists" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                           relativePath.StartsWith("npclists/", StringComparison.OrdinalIgnoreCase) ||
                           relativePath.StartsWith("npclists\\", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (fileName.Equals("namelists.dfn", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (string rawLine in File.ReadAllLines(filePath))
                {
                    string line = rawLine.Trim();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("//") || line.StartsWith(";"))
                        continue;

                    if (!line.StartsWith("[") || !line.EndsWith("]"))
                        continue;

                    string sectionID = line.Substring(1, line.Length - 2).Trim();
                    if (string.IsNullOrWhiteSpace(sectionID))
                        continue;

                    if (sectionID.StartsWith("base_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    int lastUnderscoreIndex = sectionID.LastIndexOf('_');
                    if (lastUnderscoreIndex > 0 && lastUnderscoreIndex < sectionID.Length - 1)
                    {
                        string suffix = sectionID.Substring(lastUnderscoreIndex + 1).ToLowerInvariant();

                        if (suffix == "lbr" || suffix == "aos" || suffix == "t2a" || suffix == "uor" || suffix == "ml" || suffix == "se" || suffix == "tol" || suffix == "sa" || suffix == "hs")
                            continue;
                    }

                    npcEntries.Add(new NpcPickerItem
                    {
                        SectionID = sectionID,
                        SourceFilePath = filePath
                    });
                }
            }

            return npcEntries
                .GroupBy(entry => entry.SectionID, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderBy(entry => Path.GetFileName(entry.SourceFilePath), StringComparer.OrdinalIgnoreCase)
                    .First())
                .OrderBy(entry => entry.SectionID, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string PromptForNpcSelection(string currentValue)
        {
            List<NpcPickerItem> allNpcEntries = LoadNpcEntries();
            string npcFolderPath = GetNpcFolderPath();

            if (allNpcEntries.Count == 0)
            {
                string notFoundMessage = "Could not find any NPC entries.";

                if (!string.IsNullOrWhiteSpace(npcFolderPath))
                    notFoundMessage += Environment.NewLine + Environment.NewLine +
                        "Checked folder:" + Environment.NewLine + npcFolderPath;
                else
                    notFoundMessage += Environment.NewLine +
                        "Load a UOX3 spawn folder first so the tool can locate data\\dfndata\\npc.";

                MessageBox.Show(notFoundMessage, "NPC Picker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return string.Empty;
            }

            Form picker = new Form();
            ApplyThemeToDialog(picker);
            picker.Text = "Select NPC";
            picker.Width = 620;
            picker.Height = 560;
            picker.StartPosition = FormStartPosition.CenterParent;
            picker.MinimizeBox = false;
            picker.MaximizeBox = false;
            picker.FormBorderStyle = FormBorderStyle.SizableToolWindow;

            Label descriptionLabel = new Label();
            descriptionLabel.Text = "Choose an NPC section ID. You can still type a value manually if needed.";
            descriptionLabel.Left = 12;
            descriptionLabel.Top = 12;
            descriptionLabel.Width = 580;
            descriptionLabel.Height = 20;

            TextBox searchBox = new TextBox();
            searchBox.Left = 12;
            searchBox.Top = 40;
            searchBox.Width = 580;

            ListBox listBox = new ListBox();
            listBox.Left = 12;
            listBox.Top = 72;
            listBox.Width = 580;
            listBox.Height = 400;
            listBox.HorizontalScrollbar = true;

            Button selectButton = new Button();
            selectButton.Text = "Select";
            selectButton.Width = 100;
            selectButton.Height = 28;
            selectButton.Left = 492;
            selectButton.Top = 484;
            selectButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Width = 100;
            cancelButton.Height = 28;
            cancelButton.Left = 384;
            cancelButton.Top = 484;
            cancelButton.DialogResult = DialogResult.Cancel;

            picker.Controls.Add(descriptionLabel);
            picker.Controls.Add(searchBox);
            picker.Controls.Add(listBox);
            picker.Controls.Add(selectButton);
            picker.Controls.Add(cancelButton);

            picker.AcceptButton = selectButton;
            picker.CancelButton = cancelButton;

            Action refreshNpcItems = delegate
            {
                string searchText = searchBox.Text != null ? searchBox.Text.Trim() : string.Empty;
                List<NpcPickerItem> filteredEntries = allNpcEntries;

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    filteredEntries = allNpcEntries
                        .Where(entry =>
                            entry.SectionID.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            Path.GetFileName(entry.SourceFilePath).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                listBox.BeginUpdate();
                listBox.Items.Clear();

                foreach (NpcPickerItem entry in filteredEntries)
                    listBox.Items.Add(entry);

                listBox.EndUpdate();

                if (!string.IsNullOrWhiteSpace(currentValue))
                {
                    for (int index = 0; index < listBox.Items.Count; index++)
                    {
                        NpcPickerItem item = listBox.Items[index] as NpcPickerItem;
                        if (item != null && string.Equals(item.SectionID, currentValue, StringComparison.OrdinalIgnoreCase))
                        {
                            listBox.SelectedIndex = index;
                            break;
                        }
                    }
                }

                if (listBox.SelectedIndex < 0 && listBox.Items.Count > 0)
                    listBox.SelectedIndex = 0;
            };

            searchBox.TextChanged += delegate
            {
                refreshNpcItems();
            };

            listBox.DoubleClick += delegate
            {
                if (listBox.SelectedItem != null)
                    picker.DialogResult = DialogResult.OK;
            };

            refreshNpcItems();

            if (picker.ShowDialog(this) != DialogResult.OK)
                return string.Empty;

            NpcPickerItem selectedItem = listBox.SelectedItem as NpcPickerItem;
            if (selectedItem == null)
                return string.Empty;

            return selectedItem.SectionID;
        }

        private string GetNpcListsFolderPath()
        {
            List<string> candidateFolders = new List<string>();

            if (!string.IsNullOrWhiteSpace(lastSpawnFolderPath))
            {
                string spawnFolderParent = Directory.GetParent(lastSpawnFolderPath) != null
                    ? Directory.GetParent(lastSpawnFolderPath).FullName
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(spawnFolderParent))
                    candidateFolders.Add(Path.Combine(spawnFolderParent, "npc", "npclists"));
            }

            if (!string.IsNullOrWhiteSpace(lastSpawnPath))
            {
                string spawnFileFolder = Path.GetDirectoryName(lastSpawnPath);
                if (!string.IsNullOrWhiteSpace(spawnFileFolder))
                {
                    string spawnFileFolderParent = Directory.GetParent(spawnFileFolder) != null
                        ? Directory.GetParent(spawnFileFolder).FullName
                        : string.Empty;

                    if (!string.IsNullOrWhiteSpace(spawnFileFolderParent))
                        candidateFolders.Add(Path.Combine(spawnFileFolderParent, "npc", "npclists"));
                }
            }

            candidateFolders.Add(Path.Combine(Application.StartupPath, "data", "dfndata", "npc", "npclists"));

            foreach (string candidateFolder in candidateFolders
                .Where(folder => !string.IsNullOrWhiteSpace(folder))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(candidateFolder))
                    return candidateFolder;
            }

            return string.Empty;
        }

        private List<NpcListPickerItem> LoadNpcListEntries()
        {
            List<NpcListPickerItem> npcListEntries = new List<NpcListPickerItem>();
            string npcListsFolderPath = GetNpcListsFolderPath();

            if (string.IsNullOrWhiteSpace(npcListsFolderPath) || !Directory.Exists(npcListsFolderPath))
                return npcListEntries;

            foreach (string filePath in Directory.GetFiles(npcListsFolderPath, "*.dfn", SearchOption.AllDirectories))
            {
                foreach (string rawLine in File.ReadAllLines(filePath))
                {
                    string line = rawLine.Trim();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("//") || line.StartsWith(";"))
                        continue;

                    if (!line.StartsWith("[") || !line.EndsWith("]"))
                        continue;

                    string sectionID = line.Substring(1, line.Length - 2).Trim();
                    if (string.IsNullOrWhiteSpace(sectionID))
                        continue;

                    npcListEntries.Add(new NpcListPickerItem
                    {
                        SectionID = sectionID,
                        SourceFilePath = filePath
                    });
                }
            }

            return npcListEntries
                .GroupBy(entry => entry.SectionID, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderBy(entry => Path.GetFileName(entry.SourceFilePath), StringComparer.OrdinalIgnoreCase)
                    .First())
                .OrderBy(entry => entry.SectionID, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string PromptForNpcListSelection(string currentValue)
        {
            List<NpcListPickerItem> allNpcListEntries = LoadNpcListEntries();
            string npcListsFolderPath = GetNpcListsFolderPath();

            if (allNpcListEntries.Count == 0)
            {
                string notFoundMessage = "Could not find any NPC list entries.";

                if (!string.IsNullOrWhiteSpace(npcListsFolderPath))
                    notFoundMessage += "\n\nChecked folder:\n" + npcListsFolderPath;
                else
                    notFoundMessage += "\n\nLoad a UOX3 spawn folder first so the tool can locate data\\dfndata\\npc\\npclists.";

                MessageBox.Show(notFoundMessage, "NPC List Picker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return string.Empty;
            }

            Form picker = new Form();
            ApplyThemeToDialog(picker);
            picker.Text = "Select NPC List";
            picker.Width = 620;
            picker.Height = 560;
            picker.StartPosition = FormStartPosition.CenterParent;
            picker.MinimizeBox = false;
            picker.MaximizeBox = false;
            picker.FormBorderStyle = FormBorderStyle.SizableToolWindow;

            Label descriptionLabel = new Label();
            descriptionLabel.Text = "Choose an NPC list section ID. You can still type a value manually if needed.";
            descriptionLabel.Left = 12;
            descriptionLabel.Top = 12;
            descriptionLabel.Width = 580;
            descriptionLabel.Height = 20;

            TextBox searchBox = new TextBox();
            searchBox.Left = 12;
            searchBox.Top = 40;
            searchBox.Width = 580;

            ListBox listBox = new ListBox();
            listBox.Left = 12;
            listBox.Top = 72;
            listBox.Width = 580;
            listBox.Height = 400;
            listBox.HorizontalScrollbar = true;

            Button selectButton = new Button();
            selectButton.Text = "Select";
            selectButton.Width = 100;
            selectButton.Height = 28;
            selectButton.Left = 492;
            selectButton.Top = 484;
            selectButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Width = 100;
            cancelButton.Height = 28;
            cancelButton.Left = 384;
            cancelButton.Top = 484;
            cancelButton.DialogResult = DialogResult.Cancel;

            picker.Controls.Add(descriptionLabel);
            picker.Controls.Add(searchBox);
            picker.Controls.Add(listBox);
            picker.Controls.Add(selectButton);
            picker.Controls.Add(cancelButton);

            picker.AcceptButton = selectButton;
            picker.CancelButton = cancelButton;

            Action refreshNpcListItems = delegate
            {
                string searchText = searchBox.Text != null ? searchBox.Text.Trim() : string.Empty;
                List<NpcListPickerItem> filteredEntries = allNpcListEntries;

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    filteredEntries = allNpcListEntries
                        .Where(entry =>
                            entry.SectionID.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            Path.GetFileName(entry.SourceFilePath).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                listBox.BeginUpdate();
                listBox.Items.Clear();

                foreach (NpcListPickerItem entry in filteredEntries)
                    listBox.Items.Add(entry);

                listBox.EndUpdate();

                if (!string.IsNullOrWhiteSpace(currentValue))
                {
                    for (int index = 0; index < listBox.Items.Count; index++)
                    {
                        NpcListPickerItem item = listBox.Items[index] as NpcListPickerItem;
                        if (item != null && string.Equals(item.SectionID, currentValue, StringComparison.OrdinalIgnoreCase))
                        {
                            listBox.SelectedIndex = index;
                            break;
                        }
                    }
                }

                if (listBox.SelectedIndex < 0 && listBox.Items.Count > 0)
                    listBox.SelectedIndex = 0;
            };

            searchBox.TextChanged += delegate
            {
                refreshNpcListItems();
            };

            listBox.DoubleClick += delegate
            {
                if (listBox.SelectedItem != null)
                    picker.DialogResult = DialogResult.OK;
            };

            refreshNpcListItems();

            if (picker.ShowDialog(this) != DialogResult.OK)
                return string.Empty;

            NpcListPickerItem selectedItem = listBox.SelectedItem as NpcListPickerItem;
            if (selectedItem == null)
                return string.Empty;

            return selectedItem.SectionID;
        }



        private string GetItemsFolderPath()
        {
            List<string> candidateFolders = new List<string>();

            if (!string.IsNullOrWhiteSpace(lastSpawnFolderPath))
            {
                string spawnFolderParent = Directory.GetParent(lastSpawnFolderPath) != null
                    ? Directory.GetParent(lastSpawnFolderPath).FullName
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(spawnFolderParent))
                    candidateFolders.Add(Path.Combine(spawnFolderParent, "items"));
            }

            if (!string.IsNullOrWhiteSpace(lastSpawnPath))
            {
                string spawnFileFolder = Path.GetDirectoryName(lastSpawnPath);
                if (!string.IsNullOrWhiteSpace(spawnFileFolder))
                {
                    string spawnFileFolderParent = Directory.GetParent(spawnFileFolder) != null
                        ? Directory.GetParent(spawnFileFolder).FullName
                        : string.Empty;

                    if (!string.IsNullOrWhiteSpace(spawnFileFolderParent))
                        candidateFolders.Add(Path.Combine(spawnFileFolderParent, "items"));
                }
            }

            candidateFolders.Add(Path.Combine(Application.StartupPath, "data", "dfndata", "items"));

            foreach (string candidateFolder in candidateFolders
                .Where(folder => !string.IsNullOrWhiteSpace(folder))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(candidateFolder))
                    return candidateFolder;
            }

            return string.Empty;
        }

        private List<ItemPickerItem> LoadItemEntries()
        {
            List<ItemPickerItem> itemEntries = new List<ItemPickerItem>();
            string itemFolderPath = GetItemsFolderPath();

            if (string.IsNullOrWhiteSpace(itemFolderPath) || !Directory.Exists(itemFolderPath))
                return itemEntries;

            HashSet<string> excludedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "baseitem.dfn",
        "carve_items.dfn",
        "housekeys.dfn",
        "ItemMenu.bulk.dfn",
        "ItemMenu.dfn",
        "itemtypes.dfn",
        "lootlists.dfn",
        "namelists.dfn",
        "npcmenu.bulk.dfn",
        "npcmenu.dfn",
        "polymorphmenu.dfn",
        "shoplist.dfn"
    };

            foreach (string filePath in Directory.GetFiles(itemFolderPath, "*.dfn", SearchOption.AllDirectories))
            {
                string relativePath = filePath.Substring(itemFolderPath.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                string fileName = Path.GetFileName(filePath);

                if (relativePath.StartsWith("itemlists" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    relativePath.StartsWith("itemlists/", StringComparison.OrdinalIgnoreCase) ||
                    relativePath.StartsWith("itemlists\\", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (excludedFiles.Contains(fileName))
                    continue;

                string[] lines = File.ReadAllLines(filePath);
                string currentSectionID = string.Empty;
                string currentDisplayNameText = string.Empty;

                Action commitCurrentSection = delegate ()
                {
                    if (string.IsNullOrWhiteSpace(currentSectionID))
                        return;

                    if (currentSectionID.StartsWith("base_", StringComparison.OrdinalIgnoreCase))
                        return;

                    int lastUnderscoreIndex = currentSectionID.LastIndexOf('_');
                    if (lastUnderscoreIndex > 0 && lastUnderscoreIndex < currentSectionID.Length - 1)
                    {
                        string suffix = currentSectionID.Substring(lastUnderscoreIndex + 1).ToLowerInvariant();

                        if (suffix == "lbr" || suffix == "aos" || suffix == "t2a" || suffix == "uor" ||
                            suffix == "ml" || suffix == "se" || suffix == "tol" || suffix == "sa" || suffix == "hs")
                        {
                            return;
                        }
                    }

                    itemEntries.Add(new ItemPickerItem
                    {
                        SectionID = currentSectionID,
                        SourceFilePath = filePath,
                        DisplayNameText = currentDisplayNameText
                    });
                };

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex].Trim();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("//") || line.StartsWith(";"))
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        commitCurrentSection();

                        currentSectionID = line.Substring(1, line.Length - 2).Trim();
                        currentDisplayNameText = string.Empty;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(currentSectionID))
                        continue;

                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex <= 0)
                        continue;

                    string tagName = line.Substring(0, equalsIndex).Trim();
                    string tagValue = line.Substring(equalsIndex + 1).Trim();

                    if (tagName.Equals("NAME", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(tagValue) &&
                        string.IsNullOrWhiteSpace(currentDisplayNameText))
                    {
                        currentDisplayNameText = tagValue;
                    }
                }

                commitCurrentSection();
            }

            return itemEntries
                .GroupBy(entry => entry.SectionID, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(entry => !string.IsNullOrWhiteSpace(entry.DisplayNameText))
                    .ThenBy(entry => Path.GetFileName(entry.SourceFilePath), StringComparer.OrdinalIgnoreCase)
                    .First())
                .OrderBy(entry => entry.SectionID, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string PromptForItemSelection(string currentValue)
        {
            List<ItemPickerItem> allItemEntries = LoadItemEntries();
            string itemsFolderPath = GetItemsFolderPath();

            if (allItemEntries.Count == 0)
            {
                string notFoundMessage = "Could not find any item entries.";

                if (!string.IsNullOrWhiteSpace(itemsFolderPath))
                    notFoundMessage += Environment.NewLine + Environment.NewLine +
                        "Checked folder:" + Environment.NewLine + itemsFolderPath;
                else
                    notFoundMessage += Environment.NewLine +
                        "Load a UOX3 spawn folder first so the tool can locate data\\dfndata\\items.";

                MessageBox.Show(notFoundMessage, "Item Picker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return string.Empty;
            }

            Form picker = new Form();
            ApplyThemeToDialog(picker);
            picker.Text = "Select Item";
            picker.Width = 620;
            picker.Height = 560;
            picker.StartPosition = FormStartPosition.CenterParent;
            picker.MinimizeBox = false;
            picker.MaximizeBox = false;
            picker.FormBorderStyle = FormBorderStyle.SizableToolWindow;

            Label descriptionLabel = new Label();
            descriptionLabel.Text = "Choose an item section ID. You can still type a value manually if needed.";
            descriptionLabel.Left = 12;
            descriptionLabel.Top = 12;
            descriptionLabel.Width = 580;
            descriptionLabel.Height = 20;

            TextBox searchBox = new TextBox();
            searchBox.Left = 12;
            searchBox.Top = 40;
            searchBox.Width = 580;

            ListBox listBox = new ListBox();
            listBox.Left = 12;
            listBox.Top = 72;
            listBox.Width = 580;
            listBox.Height = 400;
            listBox.HorizontalScrollbar = true;

            Button selectButton = new Button();
            selectButton.Text = "Select";
            selectButton.Width = 100;
            selectButton.Height = 28;
            selectButton.Left = 492;
            selectButton.Top = 484;
            selectButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Width = 100;
            cancelButton.Height = 28;
            cancelButton.Left = 384;
            cancelButton.Top = 484;
            cancelButton.DialogResult = DialogResult.Cancel;

            picker.Controls.Add(descriptionLabel);
            picker.Controls.Add(searchBox);
            picker.Controls.Add(listBox);
            picker.Controls.Add(selectButton);
            picker.Controls.Add(cancelButton);

            picker.AcceptButton = selectButton;
            picker.CancelButton = cancelButton;

            Action refreshItemItems = delegate
            {
                string searchText = searchBox.Text != null ? searchBox.Text.Trim() : string.Empty;
                List<ItemPickerItem> filteredEntries = allItemEntries;

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    filteredEntries = allItemEntries
                        .Where(entry =>
                            entry.SectionID.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            Path.GetFileName(entry.SourceFilePath).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                listBox.BeginUpdate();
                listBox.Items.Clear();

                foreach (ItemPickerItem entry in filteredEntries)
                    listBox.Items.Add(entry);

                listBox.EndUpdate();

                if (!string.IsNullOrWhiteSpace(currentValue))
                {
                    for (int index = 0; index < listBox.Items.Count; index++)
                    {
                        ItemPickerItem item = listBox.Items[index] as ItemPickerItem;
                        if (item != null && string.Equals(item.SectionID, currentValue, StringComparison.OrdinalIgnoreCase))
                        {
                            listBox.SelectedIndex = index;
                            break;
                        }
                    }
                }

                if (listBox.SelectedIndex < 0 && listBox.Items.Count > 0)
                    listBox.SelectedIndex = 0;
            };

            searchBox.TextChanged += delegate
            {
                refreshItemItems();
            };

            listBox.DoubleClick += delegate
            {
                if (listBox.SelectedItem != null)
                    picker.DialogResult = DialogResult.OK;
            };

            refreshItemItems();

            if (picker.ShowDialog(this) != DialogResult.OK)
                return string.Empty;

            ItemPickerItem selectedItem = listBox.SelectedItem as ItemPickerItem;
            if (selectedItem == null)
                return string.Empty;

            return selectedItem.SectionID;
        }

        private string GetItemListsFolderPath()
        {
            List<string> candidateFolders = new List<string>();

            if (!string.IsNullOrWhiteSpace(lastSpawnFolderPath))
            {
                string spawnFolderParent = Directory.GetParent(lastSpawnFolderPath) != null
                    ? Directory.GetParent(lastSpawnFolderPath).FullName
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(spawnFolderParent))
                    candidateFolders.Add(Path.Combine(spawnFolderParent, "items", "itemlists"));
            }

            if (!string.IsNullOrWhiteSpace(lastSpawnPath))
            {
                string spawnFileFolder = Path.GetDirectoryName(lastSpawnPath);
                if (!string.IsNullOrWhiteSpace(spawnFileFolder))
                {
                    string spawnFileFolderParent = Directory.GetParent(spawnFileFolder) != null
                        ? Directory.GetParent(spawnFileFolder).FullName
                        : string.Empty;

                    if (!string.IsNullOrWhiteSpace(spawnFileFolderParent))
                        candidateFolders.Add(Path.Combine(spawnFileFolderParent, "items", "itemlists"));
                }
            }

            candidateFolders.Add(Path.Combine(Application.StartupPath, "data", "dfndata", "items", "itemlists"));

            foreach (string candidateFolder in candidateFolders
                .Where(folder => !string.IsNullOrWhiteSpace(folder))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(candidateFolder))
                    return candidateFolder;
            }

            return string.Empty;
        }

        private List<ItemListPickerItem> LoadItemListEntries()
        {
            List<ItemListPickerItem> itemListEntries = new List<ItemListPickerItem>();
            string itemListsFolderPath = GetItemListsFolderPath();

            if (string.IsNullOrWhiteSpace(itemListsFolderPath) || !Directory.Exists(itemListsFolderPath))
                return itemListEntries;

            foreach (string filePath in Directory.GetFiles(itemListsFolderPath, "*.dfn", SearchOption.AllDirectories))
            {
                foreach (string rawLine in File.ReadAllLines(filePath))
                {
                    string line = rawLine.Trim();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("//") || line.StartsWith(";"))
                        continue;

                    if (!line.StartsWith("[") || !line.EndsWith("]"))
                        continue;

                    string sectionID = line.Substring(1, line.Length - 2).Trim();
                    if (string.IsNullOrWhiteSpace(sectionID))
                        continue;

                    itemListEntries.Add(new ItemListPickerItem
                    {
                        SectionID = sectionID,
                        SourceFilePath = filePath
                    });
                }
            }

            return itemListEntries
                .GroupBy(entry => entry.SectionID, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderBy(entry => Path.GetFileName(entry.SourceFilePath), StringComparer.OrdinalIgnoreCase)
                    .First())
                .OrderBy(entry => entry.SectionID, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string PromptForItemListSelection(string currentValue)
        {
            List<ItemListPickerItem> allItemListEntries = LoadItemListEntries();
            string itemListsFolderPath = GetItemListsFolderPath();

            if (allItemListEntries.Count == 0)
            {
                string notFoundMessage = "Could not find any item list entries.";

                if (!string.IsNullOrWhiteSpace(itemListsFolderPath))
                    notFoundMessage += Environment.NewLine + Environment.NewLine +
                        "Checked folder:" + Environment.NewLine + itemListsFolderPath;
                else
                    notFoundMessage += Environment.NewLine +
                        "Load a UOX3 spawn folder first so the tool can locate data\\dfndata\\items\\itemlists.";

                MessageBox.Show(notFoundMessage, "Item List Picker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return string.Empty;
            }

            Form picker = new Form();
            ApplyThemeToDialog(picker);
            picker.Text = "Select Item List";
            picker.Width = 620;
            picker.Height = 560;
            picker.StartPosition = FormStartPosition.CenterParent;
            picker.MinimizeBox = false;
            picker.MaximizeBox = false;
            picker.FormBorderStyle = FormBorderStyle.SizableToolWindow;

            Label descriptionLabel = new Label();
            descriptionLabel.Text = "Choose an item list section ID. You can still type a value manually if needed.";
            descriptionLabel.Left = 12;
            descriptionLabel.Top = 12;
            descriptionLabel.Width = 580;
            descriptionLabel.Height = 20;

            TextBox searchBox = new TextBox();
            searchBox.Left = 12;
            searchBox.Top = 40;
            searchBox.Width = 580;

            ListBox listBox = new ListBox();
            listBox.Left = 12;
            listBox.Top = 72;
            listBox.Width = 580;
            listBox.Height = 400;
            listBox.HorizontalScrollbar = true;

            Button selectButton = new Button();
            selectButton.Text = "Select";
            selectButton.Width = 100;
            selectButton.Height = 28;
            selectButton.Left = 492;
            selectButton.Top = 484;
            selectButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Width = 100;
            cancelButton.Height = 28;
            cancelButton.Left = 384;
            cancelButton.Top = 484;
            cancelButton.DialogResult = DialogResult.Cancel;

            picker.Controls.Add(descriptionLabel);
            picker.Controls.Add(searchBox);
            picker.Controls.Add(listBox);
            picker.Controls.Add(selectButton);
            picker.Controls.Add(cancelButton);

            picker.AcceptButton = selectButton;
            picker.CancelButton = cancelButton;

            Action refreshItemListItems = delegate
            {
                string searchText = searchBox.Text != null ? searchBox.Text.Trim() : string.Empty;
                List<ItemListPickerItem> filteredEntries = allItemListEntries;

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    filteredEntries = allItemListEntries
                        .Where(entry =>
                            entry.SectionID.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            Path.GetFileName(entry.SourceFilePath).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                listBox.BeginUpdate();
                listBox.Items.Clear();

                foreach (ItemListPickerItem entry in filteredEntries)
                    listBox.Items.Add(entry);

                listBox.EndUpdate();

                if (!string.IsNullOrWhiteSpace(currentValue))
                {
                    for (int index = 0; index < listBox.Items.Count; index++)
                    {
                        ItemListPickerItem item = listBox.Items[index] as ItemListPickerItem;
                        if (item != null && string.Equals(item.SectionID, currentValue, StringComparison.OrdinalIgnoreCase))
                        {
                            listBox.SelectedIndex = index;
                            break;
                        }
                    }
                }

                if (listBox.SelectedIndex < 0 && listBox.Items.Count > 0)
                    listBox.SelectedIndex = 0;
            };

            searchBox.TextChanged += delegate
            {
                refreshItemListItems();
            };

            listBox.DoubleClick += delegate
            {
                if (listBox.SelectedItem != null)
                    picker.DialogResult = DialogResult.OK;
            };

            refreshItemListItems();

            if (picker.ShowDialog(this) != DialogResult.OK)
                return string.Empty;

            ItemListPickerItem selectedItem = listBox.SelectedItem as ItemListPickerItem;
            if (selectedItem == null)
                return string.Empty;

            return selectedItem.SectionID;
        }

        private class SpawnFileListItem
        {
            public string FilePath { get; set; }
            public string DisplayName { get; set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private void newSpawnFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateNewSpawnFileForCurrentWorld();
        }

        private void deleteSpawnFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSpawnFileForCurrentWorld();
        }

        private async void loadRegionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Load a whole spawn folder?\n\nYes = Load all .dfn files in a folder\nNo = Load one spawn file",
                "Load Spawn Data",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Cancel)
                return;

            if (result == DialogResult.Yes)
            {
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select Spawn Folder";

                    if (!string.IsNullOrWhiteSpace(lastSpawnFolderPath) && Directory.Exists(lastSpawnFolderPath))
                        folderDialog.SelectedPath = lastSpawnFolderPath;
                    else if (!string.IsNullOrWhiteSpace(lastSpawnPath) && File.Exists(lastSpawnPath))
                        folderDialog.SelectedPath = Path.GetDirectoryName(lastSpawnPath);

                    if (folderDialog.ShowDialog() != DialogResult.OK)
                        return;

                    lastSpawnFolderPath = folderDialog.SelectedPath;
                    PopulateWorldFilterFromFolder(lastSpawnFolderPath);
                    await LoadSpawnFolderAsync(lastSpawnFolderPath);
                    SaveSettings();
                }
            }
            else
            {
                using (OpenFileDialog openDialog = new OpenFileDialog())
                {
                    openDialog.Filter = "Spawn Files|*.dfn";
                    openDialog.Title = "Load Spawn File";

                    if (openDialog.ShowDialog() != DialogResult.OK)
                        return;

                    lastSpawnPath = openDialog.FileName;
                    PopulateWorldFilter(lastSpawnPath);
                    await LoadSpawnFileAsync(lastSpawnPath);
                    SaveSettings();
                }
            }
        }

        private void saveRegionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (spawnRegions == null || spawnRegions.Count == 0)
            {
                MessageBox.Show("No spawn regions are loaded.", "Save Spawn File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (loadedFromFolder)
                {
                    SpawnRegionParser.SaveSpawnRegionsToSourceFiles(spawnRegions);
                    MessageBox.Show("All loaded spawn files were saved back to their source files.", "Save Spawn Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    string savePath = lastSpawnPath;

                    if (string.IsNullOrWhiteSpace(savePath))
                    {
                        using (SaveFileDialog saveDialog = new SaveFileDialog())
                        {
                            saveDialog.Filter = "Spawn Files|*.dfn";
                            saveDialog.Title = "Save Spawn File";
                            saveDialog.FileName = "spawn.dfn";

                            if (saveDialog.ShowDialog() != DialogResult.OK)
                                return;

                            savePath = saveDialog.FileName;
                        }
                    }

                    SpawnRegionParser.SaveSpawnRegions(savePath, spawnRegions);
                    lastSpawnPath = savePath;
                    MessageBox.Show("Spawn file saved.", "Save Spawn File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                SaveSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save spawn data:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateWorldFilter(string filePath)
        {
            HashSet<int> worlds = new HashSet<int>();

            if (!File.Exists(filePath))
                return;

            foreach (string rawLine in File.ReadAllLines(filePath))
            {
                string line = rawLine.Trim();

                if (!line.StartsWith("WORLD=", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] parts = line.Split('=');
                if (parts.Length != 2)
                    continue;

                int parsedWorld;
                if (int.TryParse(parts[1].Trim(), out parsedWorld))
                    worlds.Add(parsedWorld);
            }

            SetWorldFilterItems(worlds);
        }

        private void PopulateWorldFilterFromFolder(string folderPath)
        {
            HashSet<int> worlds = new HashSet<int>();

            if (!Directory.Exists(folderPath))
                return;

            foreach (string filePath in Directory.GetFiles(folderPath, "*.dfn", SearchOption.AllDirectories))
            {
                foreach (string rawLine in File.ReadAllLines(filePath))
                {
                    string line = rawLine.Trim();

                    if (!line.StartsWith("WORLD=", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string[] parts = line.Split('=');
                    if (parts.Length != 2)
                        continue;

                    int parsedWorld;
                    if (int.TryParse(parts[1].Trim(), out parsedWorld))
                        worlds.Add(parsedWorld);
                }
            }

            SetWorldFilterItems(worlds);
        }

        private void SetWorldFilterItems(IEnumerable<int> worlds)
        {
            List<int> orderedWorlds = worlds
                .Distinct()
                .OrderBy(value => value)
                .ToList();

            if (orderedWorlds.Count == 0)
                orderedWorlds.Add(0);

            suppressWorldFilterReload = true;
            comboWorldFilter.Items.Clear();

            foreach (int world in orderedWorlds)
                comboWorldFilter.Items.Add("World " + world);

            if (!orderedWorlds.Contains(currentWorld))
                currentWorld = orderedWorlds[0];

            SelectWorldFilterItem(currentWorld);
            suppressWorldFilterReload = false;
        }

        private void SelectWorldFilterItem(int worldNumber)
        {
            string itemText = "World " + worldNumber;

            for (int index = 0; index < comboWorldFilter.Items.Count; index++)
            {
                if (string.Equals(comboWorldFilter.Items[index].ToString(), itemText, StringComparison.OrdinalIgnoreCase))
                {
                    comboWorldFilter.SelectedIndex = index;
                    return;
                }
            }

            if (comboWorldFilter.Items.Count > 0)
                comboWorldFilter.SelectedIndex = 0;
        }

        private void toggleRegionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PushUndo();
            bool anyUnchecked = false;

            for (int index = 0; index < checkedListBoxRegions.Items.Count; index++)
            {
                if (!checkedListBoxRegions.GetItemChecked(index))
                {
                    anyUnchecked = true;
                    break;
                }
            }

            for (int index = 0; index < visibleSpawnRegions.Count; index++)
                visibleSpawnRegions[index].Visible = anyUnchecked;

            UpdateSpawnRegionListUI();
            SaveSettings();
            pictureBox1.Invalidate();
        }

        private void txtRegionSearch_TextChanged(object sender, EventArgs e)
        {
            UpdateSpawnRegionListUI();
            pictureBox1.Invalidate();
        }

        private async void comboWorldFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressWorldFilterReload || isLoadingSpawnData)
                return;

            int selectedWorld = GetSelectedWorldFilterValue();
            if (selectedWorld < 0)
                return;

            currentWorld = selectedWorld;

            WorldItem matchingWorld = comboBoxWorlds.Items
                .OfType<WorldItem>()
                .FirstOrDefault(item => item.ID == currentWorld);

            if (matchingWorld != null && comboBoxWorlds.SelectedItem != matchingWorld)
                comboBoxWorlds.SelectedItem = matchingWorld;
            else
            {
                if (worldMaps.ContainsKey(currentWorld))
                {
                    pictureBox1.Image = worldMaps[currentWorld];
                    originalImage = worldMaps[currentWorld] as Bitmap;
                }

                UpdateSpawnRegionListUI();
                pictureBox1.Invalidate();
            }

            await Task.CompletedTask;
        }

        private void comboBoxRegionGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSpawnRegionListUI();
            pictureBox1.Invalidate();
        }

        private void PopulateSpawnGroups()
        {
            string previousSelection = comboBoxRegionGroups.SelectedItem != null
                ? comboBoxRegionGroups.SelectedItem.ToString()
                : "All Spawn Regions";

            comboBoxRegionGroups.Items.Clear();
            comboBoxRegionGroups.Items.Add("All Spawn Regions");
            comboBoxRegionGroups.Items.Add("NPC Spawns");
            comboBoxRegionGroups.Items.Add("NPC List Spawns");
            comboBoxRegionGroups.Items.Add("Item Spawns");
            comboBoxRegionGroups.Items.Add("Item List Spawns");
            comboBoxRegionGroups.Items.Add("Missing Source");
            comboBoxRegionGroups.Items.Add("Towns");
            comboBoxRegionGroups.Items.Add("Dungeons");
            comboBoxRegionGroups.Items.Add("Wilderness");

            foreach (int world in spawnRegions.Select(region => region.World).Distinct().OrderBy(world => world))
                comboBoxRegionGroups.Items.Add("World " + world);

            if (comboBoxRegionGroups.Items.Contains(previousSelection))
                comboBoxRegionGroups.SelectedItem = previousSelection;
            else
                comboBoxRegionGroups.SelectedIndex = 0;
        }

        private void checkedListBoxRegions_MouseDown(object sender, MouseEventArgs e)
        {
            int index = checkedListBoxRegions.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
                checkedListBoxRegions.SelectedIndex = index;
        }

        private void checkedListBoxRegions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (checkedListBoxRegions.SelectedIndex < 0 || checkedListBoxRegions.SelectedIndex >= visibleSpawnRegions.Count)
            {
                selectedSpawnRegion = null;
                selectedRect = Rectangle.Empty;
                ClearSelectedRegionDetails();
                pictureBox1.Invalidate();
                return;
            }

            selectedSpawnRegion = visibleSpawnRegions[checkedListBoxRegions.SelectedIndex];
            selectedRect = selectedSpawnRegion.Bounds.Count > 0 ? selectedSpawnRegion.Bounds[0] : Rectangle.Empty;
            UpdateSelectedRegionDetails();
            pictureBox1.Invalidate();
        }

        private void checkedListBoxRegions_DoubleClick(object sender, EventArgs e)
        {
            if (checkedListBoxRegions.SelectedIndex < 0 || checkedListBoxRegions.SelectedIndex >= visibleSpawnRegions.Count)
                return;

            ShowSpawnRegionEditor(visibleSpawnRegions[checkedListBoxRegions.SelectedIndex]);
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (originalImage == null)
                return;

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && spacePanMode))
            {
                isPanning = true;
                mouseDownPos = e.Location;
                pictureBox1.Cursor = Cursors.Hand;
                return;
            }

            if (e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Shift))
            {
                isCreatingRegion = true;
                PointF start = ScreenToMapCoords(e.Location);
                newRegionRect = new Rectangle((int)start.X, (int)start.Y, 0, 0);
                regionDragStart = e.Location;
                pictureBox1.Cursor = Cursors.Cross;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                PointF mapPoint = ScreenToMapCoords(e.Location);

                if (selectedSpawnRegion != null && selectedRect != Rectangle.Empty)
                {
                    Rectangle screenRect = MapToScreenRect(selectedRect);
                    activeHandle = GetHandleUnderMouse(screenRect, e.Location);

                    if (activeHandle != ResizeHandle.None)
                    {
                        PushUndo();
                        isResizing = true;
                        regionDragStart = e.Location;
                        pictureBox1.Cursor = GetCursorForHandle(activeHandle);
                        return;
                    }
                }

                selectedSpawnRegion = null;
                selectedRect = Rectangle.Empty;
                ClearSelectedRegionDetails();

                foreach (SpawnRegion spawnRegion in spawnRegions.Where(region => region.World == currentWorld && region.Visible))
                {
                    foreach (Rectangle rect in spawnRegion.Bounds)
                    {
                        if (rect.Contains((int)mapPoint.X, (int)mapPoint.Y))
                        {
                            selectedSpawnRegion = spawnRegion;
                            selectedRect = rect;
                            regionDragStart = e.Location;
                            UpdateSelectedRegionDetails();

                            if (checkedListBoxRegions.SelectedIndex >= 0 && checkedListBoxRegions.SelectedIndex < visibleSpawnRegions.Count && visibleSpawnRegions[checkedListBoxRegions.SelectedIndex] == spawnRegion)
                            {
                                PushUndo();
                                isMovingRegion = true;
                                pictureBox1.Cursor = Cursors.SizeAll;
                            }
                            else
                            {
                                checkedListBoxRegions.SelectedIndex = visibleSpawnRegions.IndexOf(spawnRegion);
                                pictureBox1.Cursor = Cursors.Default;
                            }

                            pictureBox1.Invalidate();
                            return;
                        }
                    }
                }

                pictureBox1.Invalidate();
            }
            else if (e.Button == MouseButtons.Right)
            {
                PointF mapPoint = ScreenToMapCoords(e.Location);

                foreach (SpawnRegion spawnRegion in spawnRegions.Where(region => region.World == currentWorld && region.Visible))
                {
                    foreach (Rectangle rect in spawnRegion.Bounds)
                    {
                        if (rect.Contains((int)mapPoint.X, (int)mapPoint.Y))
                        {
                            selectedSpawnRegion = spawnRegion;
                            selectedRect = rect;
                            checkedListBoxRegions.SelectedIndex = visibleSpawnRegions.IndexOf(spawnRegion);
                            UpdateSelectedRegionDetails();
                            pictureBox1.Invalidate();
                            ShowPictureRegionContextMenu(e.Location);
                            return;
                        }
                    }
                }
            }
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (originalImage == null)
                return;

            if (isCreatingRegion)
            {
                PointF start = ScreenToMapCoords(regionDragStart);
                PointF end = ScreenToMapCoords(e.Location);

                newRegionRect = new Rectangle(
                    (int)Math.Min(start.X, end.X),
                    (int)Math.Min(start.Y, end.Y),
                    (int)Math.Abs(end.X - start.X),
                    (int)Math.Abs(end.Y - start.Y)
                );

                pictureBox1.Invalidate();
                return;
            }

            if (isResizing && selectedSpawnRegion != null)
            {
                PointF currentMap = ScreenToMapCoords(e.Location);

                int x1 = selectedRect.Left;
                int y1 = selectedRect.Top;
                int x2 = selectedRect.Right;
                int y2 = selectedRect.Bottom;

                if (activeHandle == ResizeHandle.TopLeft)
                {
                    x1 = (int)currentMap.X;
                    y1 = (int)currentMap.Y;
                }
                else if (activeHandle == ResizeHandle.Top)
                {
                    y1 = (int)currentMap.Y;
                }
                else if (activeHandle == ResizeHandle.TopRight)
                {
                    x2 = (int)currentMap.X;
                    y1 = (int)currentMap.Y;
                }
                else if (activeHandle == ResizeHandle.Right)
                {
                    x2 = (int)currentMap.X;
                }
                else if (activeHandle == ResizeHandle.BottomRight)
                {
                    x2 = (int)currentMap.X;
                    y2 = (int)currentMap.Y;
                }
                else if (activeHandle == ResizeHandle.Bottom)
                {
                    y2 = (int)currentMap.Y;
                }
                else if (activeHandle == ResizeHandle.BottomLeft)
                {
                    x1 = (int)currentMap.X;
                    y2 = (int)currentMap.Y;
                }
                else if (activeHandle == ResizeHandle.Left)
                {
                    x1 = (int)currentMap.X;
                }

                Rectangle updatedRect = Rectangle.FromLTRB(
                    Math.Min(x1, x2),
                    Math.Min(y1, y2),
                    Math.Max(x1, x2),
                    Math.Max(y1, y2)
                );

                int index = selectedSpawnRegion.Bounds.IndexOf(selectedRect);
                if (index >= 0)
                {
                    selectedSpawnRegion.Bounds[index] = updatedRect;
                    selectedRect = updatedRect;
                    UpdateSelectedRegionDetails();
                }

                pictureBox1.Invalidate();
                return;
            }

            if (isMovingRegion && selectedSpawnRegion != null)
            {
                PointF previousMapPoint = ScreenToMapCoords(regionDragStart);
                PointF currentMapPoint = ScreenToMapCoords(e.Location);

                int deltaX = (int)(currentMapPoint.X - previousMapPoint.X);
                int deltaY = (int)(currentMapPoint.Y - previousMapPoint.Y);

                Rectangle movedRect = new Rectangle(
                    selectedRect.X + deltaX,
                    selectedRect.Y + deltaY,
                    selectedRect.Width,
                    selectedRect.Height
                );

                int index = selectedSpawnRegion.Bounds.IndexOf(selectedRect);
                if (index >= 0)
                {
                    selectedSpawnRegion.Bounds[index] = movedRect;
                    selectedRect = movedRect;
                    UpdateSelectedRegionDetails();
                }

                regionDragStart = e.Location;
                pictureBox1.Invalidate();
                return;
            }

            if (isPanning)
            {
                panOffset.X += e.X - mouseDownPos.X;
                panOffset.Y += e.Y - mouseDownPos.Y;
                mouseDownPos = e.Location;
                pictureBox1.Invalidate();
                return;
            }

            if (selectedSpawnRegion != null && selectedRect != Rectangle.Empty)
            {
                Rectangle screenRect = MapToScreenRect(selectedRect);
                ResizeHandle handle = GetHandleUnderMouse(screenRect, e.Location);
                pictureBox1.Cursor = GetCursorForHandle(handle);
            }
            else
            {
                pictureBox1.Cursor = Cursors.Default;
            }
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (isCreatingRegion)
            {
                isCreatingRegion = false;
                pictureBox1.Cursor = Cursors.Default;

                if (newRegionRect.Width > 0 && newRegionRect.Height > 0)
                {
                    PushUndo();

                    SpawnRegion spawnRegion = new SpawnRegion();
                    spawnRegion.RegionNum = GetNextRegionNumber();
                    spawnRegion.Name = "New Spawn Region";
                    spawnRegion.World = currentWorld;
                    spawnRegion.Bounds.Add(newRegionRect);

                    string targetFilePath = GetActiveSpawnFilePathForCurrentWorld();

                    if (string.IsNullOrWhiteSpace(targetFilePath))
                    {
                        MessageBox.Show(
                            "No target spawn file is set for this world.\nCreate a new spawn file first.",
                            "New Spawn Region",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                        pictureBox1.Invalidate();
                        return;
                    }

                    spawnRegion.SourceFilePath = targetFilePath;

                    if (!File.Exists(spawnRegion.SourceFilePath))
                        File.WriteAllText(spawnRegion.SourceFilePath, string.Empty);

                    activeSpawnFileByWorld[currentWorld] = spawnRegion.SourceFilePath;

                    spawnRegion.SyncTypedFieldsToTags();

                    spawnRegions.Add(spawnRegion);
                    selectedSpawnRegion = spawnRegion;
                    selectedRect = newRegionRect;

                    PopulateSpawnGroups();
                    UpdateSpawnRegionListUI();
                    SaveSettings();
                }

                pictureBox1.Invalidate();
                return;
            }

            if (isMovingRegion || isResizing)
                SaveSettings();

            isMovingRegion = false;
            isResizing = false;
            isPanning = false;
            activeHandle = ResizeHandle.None;

            if (spacePanMode)
                pictureBox1.Cursor = Cursors.Hand;
            else
                pictureBox1.Cursor = Cursors.Default;
        }

        private void pictureBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || originalImage == null)
                return;

            PointF mapPoint = ScreenToMapCoords(e.Location);

            foreach (SpawnRegion spawnRegion in spawnRegions.Where(region => region.World == currentWorld && region.Visible))
            {
                foreach (Rectangle rect in spawnRegion.Bounds)
                {
                    if (rect.Contains((int)mapPoint.X, (int)mapPoint.Y))
                    {
                        selectedSpawnRegion = spawnRegion;
                        selectedRect = rect;
                        checkedListBoxRegions.SelectedIndex = visibleSpawnRegions.IndexOf(spawnRegion);
                        UpdateSelectedRegionDetails();
                        EditSelectedSpawnRegion();
                        return;
                    }
                }
            }
        }

        private void pictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
                zoomFactor += 0.1f;
            else
                zoomFactor -= 0.1f;

            ClampZoom();
            UpdateZoomUI();
            pictureBox1.Invalidate();
        }

        private int GetNextRegionNumber()
        {
            if (spawnRegions.Count == 0)
                return 1;

            return spawnRegions.Max(region => region.RegionNum) + 1;
        }

        private void ShowPictureRegionContextMenu(Point location)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ApplyThemeToContextMenu(menu);
            menu.Items.Add("Edit Tags", null, (menuSender, menuArgs) => ShowSpawnRegionEditor(selectedSpawnRegion));
            menu.Items.Add("Duplicate", null, (menuSender, menuArgs) => DuplicateSelectedSpawnRegion());
            menu.Items.Add("Rename", null, (menuSender, menuArgs) => RenameSelectedSpawnRegion());
            menu.Items.Add("Delete", null, (menuSender, menuArgs) => DeleteSelectedSpawnRegion());
            menu.Show(pictureBox1, location);
        }

        private void EditSelectedSpawnRegion()
        {
            if (selectedSpawnRegion == null)
                return;

            ShowSpawnRegionEditor(selectedSpawnRegion);
        }

        private void DuplicateSelectedSpawnRegion()
        {
            if (selectedSpawnRegion == null)
                return;

            PushUndo();

            SpawnRegion duplicateRegion = selectedSpawnRegion.Clone();
            duplicateRegion.RegionNum = GetNextRegionNumber();
            duplicateRegion.Name = selectedSpawnRegion.Name + " Copy";
            duplicateRegion.Bounds = selectedSpawnRegion.Bounds
                .Select(rect => new Rectangle(rect.X + 5, rect.Y + 5, rect.Width, rect.Height))
                .ToList();
            duplicateRegion.SyncTypedFieldsToTags();

            spawnRegions.Add(duplicateRegion);
            selectedSpawnRegion = duplicateRegion;
            selectedRect = duplicateRegion.Bounds.Count > 0 ? duplicateRegion.Bounds[0] : Rectangle.Empty;
            UpdateSelectedRegionDetails();

            PopulateSpawnGroups();
            UpdateSpawnRegionListUI();
            checkedListBoxRegions.SelectedIndex = visibleSpawnRegions.IndexOf(duplicateRegion);
            pictureBox1.Invalidate();
            SaveSettings();
        }

        private void editSelectedToolStripButton_Click(object sender, EventArgs e)
        {
            EditSelectedSpawnRegion();
        }

        private void duplicateSelectedToolStripButton_Click(object sender, EventArgs e)
        {
            DuplicateSelectedSpawnRegion();
        }

        private void RenameSelectedSpawnRegion()
        {
            if (selectedSpawnRegion == null)
                return;

            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a new name for the spawn region:",
                "Rename Spawn Region",
                selectedSpawnRegion.Name
            );

            if (string.IsNullOrWhiteSpace(newName))
                return;

            PushUndo();
            selectedSpawnRegion.Name = newName.Trim();
            selectedSpawnRegion.SyncTypedFieldsToTags();
            UpdateSelectedRegionDetails();
            UpdateSpawnRegionListUI();
            pictureBox1.Invalidate();
            SaveSettings();
        }

        private void DeleteSelectedSpawnRegion()
        {
            if (selectedSpawnRegion == null)
                return;

            DialogResult result = MessageBox.Show(
                "Delete spawn region '" + selectedSpawnRegion.Name + "'?",
                "Delete Spawn Region",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result != DialogResult.Yes)
                return;

            PushUndo();
            spawnRegions.Remove(selectedSpawnRegion);
            selectedSpawnRegion = null;
            selectedRect = Rectangle.Empty;
            ClearSelectedRegionDetails();
            PopulateSpawnGroups();
            UpdateSpawnRegionListUI();
            pictureBox1.Invalidate();
            SaveSettings();
        }

        private void ShowSpawnRegionEditor(SpawnRegion spawnRegion)
        {
            if (spawnRegion == null)
                return;

            Form tagEditor = new Form();
            ApplyThemeToDialog(tagEditor);
            tagEditor.Text = "Edit Spawn Region: " + spawnRegion.Name;
            tagEditor.Width = 720;
            tagEditor.Height = 760;
            tagEditor.StartPosition = FormStartPosition.CenterParent;
            tagEditor.MinimumSize = new Size(640, 620);
            //tagEditor.BackColor = Color.WhiteSmoke;

            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;
            panel.Padding = new Padding(14, 14, 14, 14);
            //panel.BackColor = Color.WhiteSmoke;

            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Bottom;
            buttonPanel.Height = 48;
            buttonPanel.Padding = new Padding(10, 8, 10, 8);
            buttonPanel.BackColor = Color.Gainsboro;

            Button saveButton = new Button();
            saveButton.Text = "Save";
            saveButton.Width = 100;
            saveButton.Height = 28;
            saveButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            saveButton.Location = new Point(buttonPanel.Width - 110, 10);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Width = 100;
            cancelButton.Height = 28;
            cancelButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            cancelButton.Location = new Point(buttonPanel.Width - 220, 10);
            cancelButton.Click += (cancelSender, cancelArgs) => tagEditor.Close();

            buttonPanel.Resize += (resizeSender, resizeArgs) =>
            {
                saveButton.Location = new Point(buttonPanel.ClientSize.Width - saveButton.Width - 10, 10);
                cancelButton.Location = new Point(saveButton.Left - cancelButton.Width - 8, 10);
            };

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(saveButton);

            tagEditor.Controls.Add(panel);
            tagEditor.Controls.Add(buttonPanel);

            Dictionary<string, TextBox> tagInputs = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
            int y = 0;

            string[] coreTagOrder = { "NAME", "WORLD", "INSTANCEID" };
            string[] spawnSourceTagOrder = { "NPC", "NPCLIST", "ITEM", "ITEMLIST" };
            string[] spawnSettingsTagOrder = { "MAXNPCS", "MAXITEMS", "MINTIME", "MAXTIME", "CALL", "DEFZ", "PREFZ" };
            string[] advancedTagOrder = { "ONLYOUTSIDE", "FORCESPAWN", "ISSPAWNER", "ADDSCRIPT", "VALIDLANDPOS", "VALIDWATERPOS" };

            y = AddEditorSection(panel, "Core Fields", "Main identifying values for the spawn region.", coreTagOrder, spawnRegion, tagInputs, y);
            y = AddEditorSection(panel, "Spawn Source", "Pick the creature or item source used by this region.", spawnSourceTagOrder, spawnRegion, tagInputs, y);
            y = AddEditorSection(panel, "Spawn Settings", "Counts, timing, and preferred Z values.", spawnSettingsTagOrder, spawnRegion, tagInputs, y);
            y = AddEditorSection(panel, "Advanced Tags", "Optional behavior flags and extra script hooks.", advancedTagOrder, spawnRegion, tagInputs, y);

            List<KeyValuePair<string, string>> customTags = spawnRegion.Tags
                .Where(tag => !tagDescriptions.ContainsKey(tag.Key.ToUpperInvariant()))
                .OrderBy(tag => tag.Key)
                .ToList();

            if (customTags.Count > 0)
            {
                y = AddEditorSectionHeader(panel, "Custom Tags", "Extra tags already present on this spawn region.", y);

                foreach (KeyValuePair<string, string> entry in customTags)
                    y = AddEditorField(panel, entry.Key.ToUpperInvariant(), "(Custom tag)", entry.Value, tagInputs, y, false);
            }

            saveButton.Click += (saveSender, saveArgs) =>
            {
                PushUndo();

                foreach (KeyValuePair<string, TextBox> entry in tagInputs)
                {
                    string key = entry.Key.ToUpperInvariant();
                    string value = entry.Value.Text.Trim();

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        if (spawnRegion.Tags.ContainsKey(key))
                            spawnRegion.Tags.Remove(key);
                    }
                    else
                    {
                        spawnRegion.Tags[key] = value;
                    }
                }

                spawnRegion.SyncTagsToTypedFields();
                if (spawnRegion == selectedSpawnRegion)
                    UpdateSelectedRegionDetails();
                UpdateSpawnRegionListUI();
                PopulateSpawnGroups();
                pictureBox1.Invalidate();
                SaveSettings();
                tagEditor.Close();
            };
            ApplyThemeToDialog(tagEditor);

            tagEditor.ShowDialog(this);
        }

        private int AddEditorSection(Panel panel, string sectionTitle, string sectionDescription, IEnumerable<string> keys, SpawnRegion spawnRegion, Dictionary<string, TextBox> tagInputs, int y)
        {
            y = AddEditorSectionHeader(panel, sectionTitle, sectionDescription, y);

            foreach (string key in keys)
            {
                string existingValue = string.Empty;
                if (spawnRegion.Tags.ContainsKey(key))
                    existingValue = spawnRegion.Tags[key];

                string description = tagDescriptions.ContainsKey(key) ? tagDescriptions[key] : string.Empty;
                y = AddEditorField(panel, key, description, existingValue, tagInputs, y, true);
            }

            return y;
        }

        private int AddEditorSectionHeader(Panel panel, string title, string description, int y)
        {
            Panel headerPanel = new Panel();
            headerPanel.Location = new Point(10, y);
            headerPanel.Width = Math.Max(560, panel.ClientSize.Width - 40);
            headerPanel.Height = 48;
            headerPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            headerPanel.BackColor = Color.FromArgb(232, 238, 247);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Location = new Point(10, 6);
            titleLabel.Width = headerPanel.Width - 20;
            titleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            titleLabel.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);

            Label descriptionLabel = new Label();
            descriptionLabel.Text = description;
            descriptionLabel.Location = new Point(10, 25);
            descriptionLabel.Width = headerPanel.Width - 20;
            descriptionLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            descriptionLabel.ForeColor = Color.DimGray;
            descriptionLabel.Font = new Font("Segoe UI", 8.25F, FontStyle.Regular);

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(descriptionLabel);
            panel.Controls.Add(headerPanel);

            return y + headerPanel.Height + 8;
        }

        private int AddEditorField(Panel panel, string key, string description, string value, Dictionary<string, TextBox> tagInputs, int y, bool highlightKnownTag)
        {
            bool isNpcField = string.Equals(key, "NPC", StringComparison.OrdinalIgnoreCase);
            bool isNpcListField = string.Equals(key, "NPCLIST", StringComparison.OrdinalIgnoreCase);
            bool isItemField = string.Equals(key, "ITEM", StringComparison.OrdinalIgnoreCase);
            bool isItemListField = string.Equals(key, "ITEMLIST", StringComparison.OrdinalIgnoreCase);
            bool hasBrowseButton = isNpcField || isNpcListField || isItemField || isItemListField;

            Panel fieldPanel = new Panel();
            fieldPanel.Location = new Point(10, y);
            fieldPanel.Width = Math.Max(560, panel.ClientSize.Width - 40);
            fieldPanel.Height = 70;
            fieldPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            fieldPanel.Padding = new Padding(8, 6, 8, 6);
            fieldPanel.BackColor = highlightKnownTag ? Color.White : Color.FromArgb(248, 248, 248);
            fieldPanel.BorderStyle = BorderStyle.FixedSingle;

            Label keyLabel = new Label();
            keyLabel.Text = key;
            keyLabel.Location = new Point(8, 6);
            keyLabel.Width = fieldPanel.Width - 16;
            keyLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            keyLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            Label descriptionLabel = new Label();
            descriptionLabel.Text = description;
            descriptionLabel.Location = new Point(8, 25);
            descriptionLabel.Width = fieldPanel.Width - 16;
            descriptionLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            descriptionLabel.ForeColor = Color.DimGray;
            descriptionLabel.Font = new Font("Segoe UI", 8.25F, FontStyle.Regular);

            TextBox input = new TextBox();
            input.Name = key;
            input.Location = new Point(8, 44);
            input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            input.Text = value;
            input.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            if (hasBrowseButton)
            {
                Button browseButton = new Button();
                browseButton.Text = "Browse...";
                browseButton.Width = 90;
                browseButton.Height = 24;
                browseButton.Top = 42;
                browseButton.Left = fieldPanel.Width - browseButton.Width - 8;
                browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

                input.Width = fieldPanel.Width - browseButton.Width - 24;

                browseButton.Click += delegate
                {
                    string selectedValue = string.Empty;

                    if (isNpcField)
                        selectedValue = PromptForNpcSelection(input.Text.Trim());
                    else if (isNpcListField)
                        selectedValue = PromptForNpcListSelection(input.Text.Trim());
                    else if (isItemField)
                        selectedValue = PromptForItemSelection(input.Text.Trim());
                    else if (isItemListField)
                        selectedValue = PromptForItemListSelection(input.Text.Trim());

                    if (!string.IsNullOrWhiteSpace(selectedValue))
                        input.Text = selectedValue;
                };

                fieldPanel.Controls.Add(browseButton);
            }
            else
            {
                input.Width = fieldPanel.Width - 16;
            }

            fieldPanel.Controls.Add(keyLabel);
            fieldPanel.Controls.Add(descriptionLabel);
            fieldPanel.Controls.Add(input);
            panel.Controls.Add(fieldPanel);

            tagInputs[key] = input;
            return y + fieldPanel.Height + 8;
        }

        private void editTagsMenuItem_Click(object sender, EventArgs e)
        {
            if (checkedListBoxRegions.SelectedIndex < 0 || checkedListBoxRegions.SelectedIndex >= visibleSpawnRegions.Count)
                return;

            ShowSpawnRegionEditor(visibleSpawnRegions[checkedListBoxRegions.SelectedIndex]);
        }

        private void compareTagsMenuItem_Click(object sender, EventArgs e)
        {
            if (checkedListBoxRegions.SelectedIndex < 0 || checkedListBoxRegions.SelectedIndex >= visibleSpawnRegions.Count)
                return;

            SpawnRegion selectedRegionForCompare = visibleSpawnRegions[checkedListBoxRegions.SelectedIndex];

            Form picker = new Form();
            ApplyThemeToDialog(picker);
            picker.Text = "Select Spawn Region to Compare";
            picker.Width = 420;
            picker.Height = 500;

            ListBox listBox = new ListBox();
            listBox.Dock = DockStyle.Fill;

            foreach (SpawnRegion spawnRegion in spawnRegions.Where(region => region != selectedRegionForCompare))
                listBox.Items.Add(spawnRegion);

            picker.Controls.Add(listBox);

            listBox.DoubleClick += (listSender, listArgs) =>
            {
                SpawnRegion otherRegion = listBox.SelectedItem as SpawnRegion;
                if (otherRegion == null)
                    return;

                ShowTagComparison(selectedRegionForCompare, otherRegion);
                picker.Close();
            };

            picker.ShowDialog();
        }

        private void ShowTagComparison(SpawnRegion firstRegion, SpawnRegion secondRegion)
        {
            Form compareForm = new Form();
            ApplyThemeToDialog(compareForm);
            compareForm.Text = "Compare Spawn Region Tags";
            compareForm.Width = 900;
            compareForm.Height = 650;

            TextBox compareBox = new TextBox();
            compareBox.Multiline = true;
            compareBox.ScrollBars = ScrollBars.Both;
            compareBox.ReadOnly = true;
            compareBox.WordWrap = false;
            compareBox.Dock = DockStyle.Fill;
            compareBox.Font = new Font("Consolas", 10f);

            List<string> lines = new List<string>();
            lines.Add("Left : " + firstRegion.Name + " [" + firstRegion.RegionNum + "]");
            lines.Add("Right: " + secondRegion.Name + " [" + secondRegion.RegionNum + "]");
            lines.Add(string.Empty);

            HashSet<string> allKeys = new HashSet<string>(firstRegion.Tags.Keys, StringComparer.OrdinalIgnoreCase);
            allKeys.UnionWith(secondRegion.Tags.Keys);

            foreach (string key in allKeys.OrderBy(value => value))
            {
                string leftValue = firstRegion.Tags.ContainsKey(key) ? firstRegion.Tags[key] : string.Empty;
                string rightValue = secondRegion.Tags.ContainsKey(key) ? secondRegion.Tags[key] : string.Empty;

                if (leftValue == rightValue)
                    lines.Add(key + " = " + leftValue);
                else
                {
                    lines.Add(key);
                    lines.Add("  Left : " + leftValue);
                    lines.Add("  Right: " + rightValue);
                    lines.Add(string.Empty);
                }
            }

            compareBox.Text = string.Join(Environment.NewLine, lines);
            compareForm.Controls.Add(compareBox);
            compareForm.ShowDialog();
        }

        private void toggleLabelsToolStripButton_Click(object sender, EventArgs e)
        {
            if (labelDisplayMode == LabelDisplayMode.SelectedOnly)
                labelDisplayMode = LabelDisplayMode.AllVisible;
            else if (labelDisplayMode == LabelDisplayMode.AllVisible)
                labelDisplayMode = LabelDisplayMode.Hidden;
            else
                labelDisplayMode = LabelDisplayMode.SelectedOnly;

            UpdateSpawnLabelButtonText();
            pictureBox1.Invalidate();
            SaveSettings();
        }

        private void UpdateSpawnLabelButtonText()
        {
            if (toggleLabelsToolStripButton == null)
                return;

            if (labelDisplayMode == LabelDisplayMode.Hidden)
                toggleLabelsToolStripButton.Text = "Labels: Off";
            else if (labelDisplayMode == LabelDisplayMode.SelectedOnly)
                toggleLabelsToolStripButton.Text = "Labels: Selected";
            else
                toggleLabelsToolStripButton.Text = "Labels: All";
        }

        private bool ShouldDrawLabelForRegion(SpawnRegion spawnRegion)
        {
            if (labelDisplayMode == LabelDisplayMode.Hidden)
                return false;

            if (labelDisplayMode == LabelDisplayMode.SelectedOnly)
                return spawnRegion == selectedSpawnRegion;

            return zoomFactor >= 1.15f || spawnRegion == selectedSpawnRegion;
        }

        private void ClearSelectedRegionDetails()
        {
            SetSelectedDetailsText(txtSelectedName, string.Empty);
            SetSelectedDetailsText(txtSelectedRegionNumber, string.Empty);
            SetSelectedDetailsText(txtSelectedWorld, string.Empty);
            SetSelectedDetailsText(txtSelectedSourceFile, string.Empty);
            SetSelectedDetailsText(txtSelectedSpawnSource, string.Empty);
            SetSelectedDetailsText(txtSelectedCounts, string.Empty);
            SetSelectedDetailsText(txtSelectedRespawn, string.Empty);
            SetSelectedDetailsText(txtSelectedZ, string.Empty);
            SetSelectedDetailsText(txtSelectedBounds, string.Empty);
            SetSelectedDetailsText(txtSelectedRaw, string.Empty);
        }

        private void UpdateSelectedRegionDetails()
        {
            if (selectedSpawnRegion == null)
            {
                ClearSelectedRegionDetails();
                return;
            }

            string worldText = worldNameMap.ContainsKey(selectedSpawnRegion.World)
                ? selectedSpawnRegion.World + " - " + worldNameMap[selectedSpawnRegion.World]
                : selectedSpawnRegion.World.ToString();

            string countsText = "Max NPCs: " + selectedSpawnRegion.MaxNpcs + " | Max Items: " + selectedSpawnRegion.MaxItems;
            string respawnText = "Min: " + selectedSpawnRegion.MinTime + " | Max: " + selectedSpawnRegion.MaxTime + " | Call: " + selectedSpawnRegion.Call;
            string zText = "DefZ: " + selectedSpawnRegion.DefZ + " | PrefZ: " + selectedSpawnRegion.PrefZ;

            SetSelectedDetailsText(txtSelectedName, selectedSpawnRegion.Name);
            SetSelectedDetailsText(txtSelectedRegionNumber, selectedSpawnRegion.RegionNum.ToString());
            SetSelectedDetailsText(txtSelectedWorld, worldText);
            SetSelectedDetailsText(txtSelectedSourceFile, selectedSpawnRegion.GetShortSourceFileName());
            SetSelectedDetailsText(txtSelectedSpawnSource, selectedSpawnRegion.GetSpawnSource());
            SetSelectedDetailsText(txtSelectedCounts, countsText);
            SetSelectedDetailsText(txtSelectedRespawn, respawnText);
            SetSelectedDetailsText(txtSelectedZ, zText);
            SetSelectedDetailsText(txtSelectedBounds, BuildBoundsSummary(selectedSpawnRegion));
            SetSelectedDetailsText(txtSelectedRaw, BuildRawSpawnRegionBlock(selectedSpawnRegion));
        }

        private void SetSelectedDetailsText(TextBox textBox, string value)
        {
            if (textBox == null)
                return;

            textBox.Text = value ?? string.Empty;
        }

        private string BuildBoundsSummary(SpawnRegion spawnRegion)
        {
            if (spawnRegion == null || spawnRegion.Bounds == null || spawnRegion.Bounds.Count == 0)
                return string.Empty;

            List<string> lines = new List<string>();

            for (int index = 0; index < spawnRegion.Bounds.Count; index++)
            {
                Rectangle rect = spawnRegion.Bounds[index];
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                lines.Add(
                    "#" + (index + 1) +
                    ": X1=" + rect.Left +
                    ", Y1=" + rect.Top +
                    ", X2=" + rect.Right +
                    ", Y2=" + rect.Bottom +
                    " (" + width + "x" + height + ")"
                );
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string BuildRawSpawnRegionBlock(SpawnRegion spawnRegion)
        {
            if (spawnRegion == null)
                return string.Empty;

            spawnRegion.SyncTypedFieldsToTags();

            List<string> lines = new List<string>();
            lines.Add("[REGIONSPAWN " + spawnRegion.RegionNum + "]");
            lines.Add("{");

            AddRawTagIfPresent(lines, spawnRegion.Tags, "NAME");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "WORLD");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "INSTANCEID");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "NPC");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "NPCLIST");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "ITEM");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "ITEMLIST");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "MAXNPCS");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "MAXITEMS");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "MINTIME");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "MAXTIME");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "CALL");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "DEFZ");
            AddRawTagIfPresent(lines, spawnRegion.Tags, "PREFZ");

            foreach (KeyValuePair<string, string> tag in spawnRegion.Tags.OrderBy(entry => entry.Key))
            {
                string key = tag.Key.ToUpperInvariant();

                if (key == "NAME" ||
                    key == "WORLD" ||
                    key == "INSTANCEID" ||
                    key == "NPC" ||
                    key == "NPCLIST" ||
                    key == "ITEM" ||
                    key == "ITEMLIST" ||
                    key == "MAXNPCS" ||
                    key == "MAXITEMS" ||
                    key == "MINTIME" ||
                    key == "MAXTIME" ||
                    key == "CALL" ||
                    key == "DEFZ" ||
                    key == "PREFZ" ||
                    key == "X1" ||
                    key == "Y1" ||
                    key == "X2" ||
                    key == "Y2")
                {
                    continue;
                }

                lines.Add(key + "=" + tag.Value);
            }

            foreach (Rectangle rect in spawnRegion.Bounds)
            {
                lines.Add("X1=" + rect.Left);
                lines.Add("Y1=" + rect.Top);
                lines.Add("X2=" + rect.Right);
                lines.Add("Y2=" + rect.Bottom);
            }

            lines.Add("}");
            return string.Join(Environment.NewLine, lines);
        }

        private void AddRawTagIfPresent(List<string> lines, Dictionary<string, string> tags, string key)
        {
            string value;
            if (tags.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                lines.Add(key + "=" + value);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                spacePanMode = true;

                if (!isPanning)
                    pictureBox1.Cursor = Cursors.Hand;
            }
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                spacePanMode = false;

                if (!isPanning)
                    pictureBox1.Cursor = Cursors.Default;
            }
        }
        private PointF ScreenToMapCoords(Point screenPoint)
        {
            if (originalImage == null)
                return PointF.Empty;

            Size currentWorldSize = GetCurrentWorldDimensions();
            float scaleX = (float)originalImage.Width / currentWorldSize.Width;
            float scaleY = (float)originalImage.Height / currentWorldSize.Height;

            float x = (screenPoint.X - panOffset.X) / zoomFactor / scaleX;
            float y = (screenPoint.Y - panOffset.Y) / zoomFactor / scaleY;

            return new PointF(x, y);
        }

        private void DrawResizeHandle(Graphics graphics, float centerX, float centerY, float handleSize)
        {
            graphics.FillRectangle(Brushes.White, centerX - handleSize / 2f, centerY - handleSize / 2f, handleSize, handleSize);
            graphics.DrawRectangle(Pens.Black, centerX - handleSize / 2f, centerY - handleSize / 2f, handleSize, handleSize);
        }

        private Cursor GetCursorForHandle(ResizeHandle handle)
        {
            switch (handle)
            {
                case ResizeHandle.TopLeft:
                case ResizeHandle.BottomRight:
                    return Cursors.SizeNWSE;
                case ResizeHandle.TopRight:
                case ResizeHandle.BottomLeft:
                    return Cursors.SizeNESW;
                case ResizeHandle.Top:
                case ResizeHandle.Bottom:
                    return Cursors.SizeNS;
                case ResizeHandle.Left:
                case ResizeHandle.Right:
                    return Cursors.SizeWE;
                default:
                    return Cursors.Default;
            }
        }

        private Rectangle MapToScreenRect(Rectangle rect)
        {
            if (originalImage == null)
                return Rectangle.Empty;

            Size currentWorldSize = GetCurrentWorldDimensions();
            float scaleX = ((float)originalImage.Width / currentWorldSize.Width) * zoomFactor;
            float scaleY = ((float)originalImage.Height / currentWorldSize.Height) * zoomFactor;

            int x = (int)(rect.X * scaleX + panOffset.X);
            int y = (int)(rect.Y * scaleY + panOffset.Y);
            int width = (int)(rect.Width * scaleX);
            int height = (int)(rect.Height * scaleY);

            return new Rectangle(x, y, width, height);
        }

        private ResizeHandle GetHandleUnderMouse(Rectangle rect, Point mouse)
        {
            int handleSize = 8;
            int halfHandle = handleSize / 2;
            int midX = rect.Left + rect.Width / 2;
            int midY = rect.Top + rect.Height / 2;

            if (new Rectangle(rect.Left - halfHandle, rect.Top - halfHandle, handleSize, handleSize).Contains(mouse))
                return ResizeHandle.TopLeft;

            if (new Rectangle(midX - halfHandle, rect.Top - halfHandle, handleSize, handleSize).Contains(mouse))
                return ResizeHandle.Top;

            if (new Rectangle(rect.Right - halfHandle, rect.Top - halfHandle, handleSize, handleSize).Contains(mouse))
                return ResizeHandle.TopRight;

            if (new Rectangle(rect.Right - halfHandle, midY - halfHandle, handleSize, handleSize).Contains(mouse))
                return ResizeHandle.Right;

            if (new Rectangle(rect.Right - halfHandle, rect.Bottom - halfHandle, handleSize, handleSize).Contains(mouse))
                return ResizeHandle.BottomRight;

            if (new Rectangle(midX - halfHandle, rect.Bottom - halfHandle, handleSize, handleSize).Contains(mouse))
                return ResizeHandle.Bottom;

            if (new Rectangle(rect.Left - halfHandle, rect.Bottom - halfHandle, handleSize, handleSize).Contains(mouse))
                return ResizeHandle.BottomLeft;

            if (new Rectangle(rect.Left - halfHandle, midY - halfHandle, handleSize, handleSize).Contains(mouse))
                return ResizeHandle.Left;

            return ResizeHandle.None;
        }

        private bool IsDungeonSpawn(SpawnRegion spawnRegion)
        {
            string sourceFile = spawnRegion.GetShortSourceFileName().ToLowerInvariant();

            return sourceFile.Contains("dungeon") || sourceFile.Contains("cave");
        }

        private bool IsTownSpawn(SpawnRegion spawnRegion)
        {
            string sourceFile = spawnRegion.GetShortSourceFileName().ToLowerInvariant();

            return sourceFile.Contains("town");
        }

        private bool IsWildernessSpawn(SpawnRegion spawnRegion)
        {
            return !IsTownSpawn(spawnRegion) && !IsDungeonSpawn(spawnRegion);
        }

        private void buttonHideAll_Click(object sender, EventArgs e)
        {
            PushUndo();

            foreach (SpawnRegion spawnRegion in spawnRegions)
                spawnRegion.Visible = false;

            UpdateSpawnRegionListUI();
            pictureBox1.Invalidate();
            SaveSettings();
        }

        private void buttonShowAll_Click(object sender, EventArgs e)
        {
            PushUndo();

            foreach (SpawnRegion spawnRegion in spawnRegions)
                spawnRegion.Visible = true;

            UpdateSpawnRegionListUI();
            pictureBox1.Invalidate();
            SaveSettings();
        }

        private void buttonShowOnlySelected_Click(object sender, EventArgs e)
        {
            if (checkedListBoxRegions.SelectedIndex < 0 || checkedListBoxRegions.SelectedIndex >= visibleSpawnRegions.Count)
                return;

            PushUndo();

            SpawnRegion selectedVisibleRegion = visibleSpawnRegions[checkedListBoxRegions.SelectedIndex];

            foreach (SpawnRegion spawnRegion in spawnRegions)
                spawnRegion.Visible = (spawnRegion == selectedVisibleRegion);

            UpdateSpawnRegionListUI();
            pictureBox1.Invalidate();
            SaveSettings();
        }

        private void undoToolStripButton_Click(object sender, EventArgs e)
        {
            UndoLastAction();
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UndoLastAction();
        }

        private void UpdateUndoUI()
        {
            bool canUndo = undoStack.Count > 0;

            if (undoToolStripButton != null)
                undoToolStripButton.Enabled = canUndo;

            if (undoToolStripMenuItem != null)
                undoToolStripMenuItem.Enabled = canUndo;
        }

        private void UpdateWindowTitle()
        {
            string titleText = "UOX3 Spawn Editor v" + AppVersionHelper.GetCurrentVersion() + " - Profile: " + currentProfileName;

            string activeFilePath = GetActiveSpawnFilePathForCurrentWorld();
            if (!string.IsNullOrWhiteSpace(activeFilePath))
                titleText += " - World " + currentWorld + " -> " + Path.GetFileName(activeFilePath);

            this.Text = titleText;
        }


        private void SetTheme(AppTheme theme)
        {
            currentTheme = theme;
            ApplyTheme();
            SaveSettings();
        }

        private void ApplyTheme()
        {
            Color backColor;
            Color surfaceColor;
            Color altSurfaceColor;
            Color controlBackColor;
            Color controlForeColor;
            Color menuBackColor;
            Color accentBackColor;
            Color pictureBackColor;

            if (currentTheme == AppTheme.Dark)
            {
                backColor = Color.FromArgb(32, 32, 36);
                surfaceColor = Color.FromArgb(45, 45, 50);
                altSurfaceColor = Color.FromArgb(55, 55, 60);
                controlBackColor = Color.FromArgb(62, 62, 68);
                controlForeColor = Color.WhiteSmoke;
                menuBackColor = Color.FromArgb(52, 73, 94);
                accentBackColor = Color.FromArgb(70, 70, 76);
                pictureBackColor = Color.FromArgb(40, 40, 44);
            }
            else
            {
                backColor = SystemColors.Control;
                surfaceColor = Color.WhiteSmoke;
                altSurfaceColor = Color.Gainsboro;
                controlBackColor = Color.White;
                controlForeColor = SystemColors.ControlText;
                menuBackColor = Color.LightBlue;
                accentBackColor = Color.WhiteSmoke;
                pictureBackColor = Color.DimGray;
            }

            this.BackColor = backColor;
            this.ForeColor = controlForeColor;

            if (mainLayout != null)
            {
                mainLayout.BackColor = backColor;
                mainLayout.ForeColor = controlForeColor;
            }

            if (menuStrip1 != null)
            {
                menuStrip1.BackColor = menuBackColor;
                menuStrip1.ForeColor = controlForeColor;
                ApplyThemeToToolStrip(menuStrip1, menuBackColor, controlForeColor);
            }

            if (toolStrip1 != null)
            {
                toolStrip1.BackColor = accentBackColor;
                toolStrip1.ForeColor = controlForeColor;
                ApplyThemeToToolStrip(toolStrip1, accentBackColor, controlForeColor);
            }

            if (loadingStatusStrip != null)
            {
                loadingStatusStrip.BackColor = accentBackColor;
                loadingStatusStrip.ForeColor = controlForeColor;
                ApplyThemeToToolStrip(loadingStatusStrip, accentBackColor, controlForeColor);
            }

            if (panelRegionSidebar != null)
                panelRegionSidebar.BackColor = surfaceColor;

            if (panelSelectionDetails != null)
                panelSelectionDetails.BackColor = surfaceColor;

            if (panelSidebarButtons != null)
                panelSidebarButtons.BackColor = surfaceColor;

            if (tableLayoutSelectedRegionDetails != null)
                tableLayoutSelectedRegionDetails.BackColor = surfaceColor;

            if (pictureBox1 != null)
                pictureBox1.BackColor = pictureBackColor;

            ApplyThemeToControl(this, backColor, surfaceColor, altSurfaceColor, controlBackColor, controlForeColor);
            UpdateThemeMenuChecks();

            if (regionContextMenu != null)
                ApplyThemeToContextMenu(regionContextMenu);

            if (pictureBox1 != null)
                pictureBox1.Invalidate();
            ApplyWindowTitleBarTheme();
        }

        private void ApplyThemeToToolStrip(ToolStrip toolStrip, Color backColor, Color foreColor)
        {
            toolStrip.BackColor = backColor;
            toolStrip.ForeColor = foreColor;

            foreach (ToolStripItem item in toolStrip.Items)
            {
                item.BackColor = backColor;
                item.ForeColor = foreColor;

                ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                if (menuItem != null)
                    ApplyThemeToMenuItemTree(menuItem, backColor, foreColor);
            }
        }

        private void ApplyThemeToMenuItemTree(ToolStripMenuItem menuItem, Color backColor, Color foreColor)
        {
            menuItem.BackColor = backColor;
            menuItem.ForeColor = foreColor;

            foreach (ToolStripItem dropDownItem in menuItem.DropDownItems)
            {
                dropDownItem.BackColor = backColor;
                dropDownItem.ForeColor = foreColor;

                ToolStripMenuItem childMenuItem = dropDownItem as ToolStripMenuItem;
                if (childMenuItem != null)
                    ApplyThemeToMenuItemTree(childMenuItem, backColor, foreColor);
            }
        }

        private void ApplyThemeToControl(Control parentControl, Color formBackColor, Color surfaceColor, Color altSurfaceColor, Color controlBackColor, Color controlForeColor)
        {
            foreach (Control control in parentControl.Controls)
            {
                if (control is TextBox)
                {
                    TextBox textBox = (TextBox)control;
                    textBox.BackColor = controlBackColor;
                    textBox.ForeColor = controlForeColor;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (control is CheckedListBox)
                {
                    CheckedListBox checkedListBox = (CheckedListBox)control;
                    checkedListBox.BackColor = controlBackColor;
                    checkedListBox.ForeColor = controlForeColor;
                    checkedListBox.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (control is ListBox)
                {
                    ListBox listBox = (ListBox)control;
                    listBox.BackColor = controlBackColor;
                    listBox.ForeColor = controlForeColor;
                    listBox.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (control is ComboBox)
                {
                    ComboBox comboBox = (ComboBox)control;
                    comboBox.BackColor = controlBackColor;
                    comboBox.ForeColor = controlForeColor;
                    comboBox.FlatStyle = FlatStyle.Flat;
                }
                else if (control is Button)
                {
                    Button button = (Button)control;
                    button.BackColor = altSurfaceColor;
                    button.ForeColor = controlForeColor;
                    button.FlatStyle = FlatStyle.Flat;
                }
                else if (control is Panel || control is FlowLayoutPanel || control is TableLayoutPanel)
                {
                    control.BackColor = surfaceColor;
                    control.ForeColor = controlForeColor;
                }
                else if (control is Label)
                {
                    control.BackColor = Color.Transparent;
                    control.ForeColor = controlForeColor;
                }
                else if (control is MenuStrip || control is ToolStrip || control is StatusStrip || control is PictureBox)
                {
                }
                else
                {
                    control.BackColor = formBackColor;
                    control.ForeColor = controlForeColor;
                }

                ApplyThemeToControl(control, formBackColor, surfaceColor, altSurfaceColor, controlBackColor, controlForeColor);
            }

            if (parentControl.ContextMenuStrip != null)
                ApplyThemeToContextMenu(parentControl.ContextMenuStrip);
        }

        private void ApplyThemeToContextMenu(ContextMenuStrip contextMenu)
        {
            if (contextMenu == null)
                return;

            Color surfaceColor = currentTheme == AppTheme.Dark ? Color.FromArgb(45, 45, 50) : Color.WhiteSmoke;
            Color controlForeColor = currentTheme == AppTheme.Dark ? Color.WhiteSmoke : SystemColors.ControlText;

            contextMenu.BackColor = surfaceColor;
            contextMenu.ForeColor = controlForeColor;

            foreach (ToolStripItem item in contextMenu.Items)
            {
                item.BackColor = surfaceColor;
                item.ForeColor = controlForeColor;
            }
        }

        private void ApplyThemeToDialog(Form dialog)
        {
            if (dialog == null)
                return;

            Color backColor = currentTheme == AppTheme.Dark ? Color.FromArgb(32, 32, 36) : SystemColors.Control;
            Color surfaceColor = currentTheme == AppTheme.Dark ? Color.FromArgb(45, 45, 50) : Color.WhiteSmoke;
            Color altSurfaceColor = currentTheme == AppTheme.Dark ? Color.FromArgb(55, 55, 60) : Color.Gainsboro;
            Color controlBackColor = currentTheme == AppTheme.Dark ? Color.FromArgb(62, 62, 68) : Color.White;
            Color controlForeColor = currentTheme == AppTheme.Dark ? Color.WhiteSmoke : SystemColors.ControlText;

            dialog.BackColor = backColor;
            dialog.ForeColor = controlForeColor;
            ApplyThemeToControl(dialog, backColor, surfaceColor, altSurfaceColor, controlBackColor, controlForeColor);
        }

        private void UpdateThemeMenuChecks()
        {
            if (lightModeToolStripMenuItem != null)
                lightModeToolStripMenuItem.Checked = currentTheme == AppTheme.Light;

            if (darkModeToolStripMenuItem != null)
                darkModeToolStripMenuItem.Checked = currentTheme == AppTheme.Dark;
        }

        private void lightModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetTheme(AppTheme.Light);
        }

        private void darkModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetTheme(AppTheme.Dark);
        }

        private void CheckForUpdates()
        {
            try
            {
                string manifestUrl = "https://distantlanduo.com/spawnupdater/version.json";
                string currentVersion = AppVersionHelper.GetCurrentVersion();

                UpdateManifest manifest = UpdateService.GetManifest(manifestUrl);
                if (manifest == null)
                    return;

                if (!UpdateService.IsUpdateAvailable(currentVersion, manifest.LatestVersion))
                    return;

                DialogResult result = MessageBox.Show(
                    "A new version is available.\n\n" +
                    "Current Version: " + currentVersion + "\n" +
                    "Latest Version: " + manifest.LatestVersion + "\n\n" +
                    "Changes:\n" + (manifest.Changelog ?? "No changelog provided.") + "\n\n" +
                    "Do you want to update now?",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );

                if (result != DialogResult.Yes)
                    return;

                LaunchUpdater(manifestUrl);
            }
            catch
            {
                // fail quietly for now
            }
        }

        private void LaunchUpdater(string downloadUrl)
        {
            string appPath = Application.ExecutablePath;
            string updaterPath = Path.Combine(Application.StartupPath, "UOX3SpawnEditorUpdater.exe");

            if (!File.Exists(updaterPath))
            {
                MessageBox.Show(
                    "Updater executable was not found:\n" + updaterPath,
                    "Update Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            SaveSettings();

            System.Diagnostics.Process.Start(
                updaterPath,
                "\"" + appPath + "\" \"" + downloadUrl + "\""
            );

            Application.Exit();
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string helpText =
            @"== UOX3 Spawn Editor Help ==

SHIFT + Left Click + Drag
  Create a new spawn region

Left Click + Drag inside a region
  Move selected spawn region rectangle

Left Click on a resize handle
  Resize selected spawn region rectangle

Mouse Wheel
  Zoom in or out

Middle Mouse Drag
  Pan map

Space + Left Drag
  Pan map

Right Click on a spawn region
  Rename, duplicate, delete, or edit tags

File Menu
  Open Map Image
  Load Spawn File
  Save Spawn File
  New Spawn File
  Toggle Spawn Regions

Theme Menu
  Switch between Light Mode and Dark Mode

DELETE
  Delete selected spawn region

CTRL + SHIFT + N
  Create a new spawn file for the current world

CTRL + SHIFT + DELETE
  Choose and delete a spawn file for the current world

Sidebar
  Shows visible spawn regions with checkboxes

Toolbar Labels button
  Cycle between selected labels, all labels, or no labels

CTRL + Z
  Undo last edit";

            MessageBox.Show(helpText, "How to Use", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}