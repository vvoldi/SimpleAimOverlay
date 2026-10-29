using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SimpleAimOverlay;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        AppFonts.Initialize();
        if (args.Contains("--watch-game")) { GameLauncher.RunWatcher(); return; }
        using var mutex = new Mutex(true, "SimpleAimOverlay.SingleInstance", out bool first);
        if (!first) return;
        Application.Run(new ControlWindow());
    }
}

internal sealed class Profile
{
    public string Name { get; set; } = "Default";
    public AimSettings Aim { get; set; } = new();
}

internal sealed class AimSettings
{
    public string Shape { get; set; } = "Cross";
    public string Color { get; set; } = "#FF39D98A";
    public string OutlineColor { get; set; } = "#FF101820";
    public bool Outline { get; set; }
    public float OutlineWidth { get; set; } = 1;
    public int OutlineOpacity { get; set; } = 100;
    public int Size { get; set; } = 32;
    public int ImageScale { get; set; } = 200;
    public float Thickness { get; set; } = 3;
    public int Gap { get; set; } = 7;
    public int Opacity { get; set; } = 100;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public string Monitor { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public bool TintImage { get; set; }
    public bool Visible { get; set; } = true;
    public string Hotkey { get; set; } = "F8";
    public bool HideWhileHeld { get; set; } = true;
    public string HoldKey { get; set; } = "Right Mouse";
    public bool StartWithWindows { get; set; }
}

internal sealed class StoreData
{
    public AimSettings Current { get; set; } = new();
    public List<Profile> Profiles { get; set; } = [new Profile()];
    public List<string> Images { get; set; } = [];
    public List<string> OriginalColorImages { get; set; } = [];
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool MinimizeToTray { get; set; }
    public bool LaunchWithGame { get; set; }
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "dark";
    public string GameExecutable { get; set; } = GameLauncher.FindPubg();
}

internal static class Store
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleAimOverlay");
    public static readonly string FilePath = Path.Combine(Root, "settings.json");
    public static StoreData Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<StoreData>(File.ReadAllText(FilePath)) ?? new StoreData(); } catch { }
        return new StoreData();
    }
    public static void Save(StoreData data)
    {
        try { Directory.CreateDirectory(Root); string pending=FilePath+".new"; File.WriteAllText(pending, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })); File.Move(pending,FilePath,true); } catch { }
    }
}

internal sealed class ControlWindow : Form
{
    private const int WM_HOTKEY = 0x0312, HotkeyId = 0x51A1;
    private StoreData data;
    private readonly bool previewMode;
    private readonly OverlayWindow overlay = new();
    private readonly DarkTabHost tabs = new();
    private readonly DarkComboBox shape = new(), monitor = new(), language = new(), theme = new();
    private readonly KeyBindingButton hotkey = new(), holdKey = new();
    private bool toggleWasDown;
    private readonly DarkSlider outlineWidth = Slider(5, 100), outlineOpacity = Slider(0, 100);
    private readonly DarkListBox images = new();
    private readonly DarkCheckBox outline = new(), visible = new(), startup = new(), hideWhileHeld = new(), closeToTray = new();
    private readonly DarkSlider size = Slider(8, 180), imageScale = Slider(50, 800), thickness = Slider(10, 240), gap = Slider(0, 100), opacity = Slider(10, 100);
    private readonly DarkNumberBox offsetX = new(-2000, 2000), offsetY = new(-2000, 2000);
    private readonly Button colorButton = new RoundedButton(), outlineButton = new RoundedButton();
    private readonly Button toggleButton = new RoundedButton();
    private readonly Panel preview = new();
    private readonly Label status = new();
    private readonly NotifyIcon tray = new();
    private readonly AppUpdater updater = new();
    private readonly Label updateStatus = new() { ForeColor = DarkTheme.Muted, AutoSize = false, Text = "Checks once at launch; manual checks are always available." };
    private readonly System.Windows.Forms.Timer holdTimer = new() { Interval = 12 };
    private bool initializing = true;
    private bool holdDown, desiredVisible = true, allowExit, trayInteracting;
    private bool runtimeStarted, gameForeground;
    private long nextGameFocusCheck;
    private readonly DarkCheckBox minimizeToTray = new(), launchWithGame = new();
    private readonly TextBox gameExecutable = new() { BackColor = DarkTheme.Input, ForeColor = DarkTheme.Text, BorderStyle = BorderStyle.FixedSingle };
    private Color aimColor = Color.LimeGreen, borderColor = Color.FromArgb(16, 24, 32);
    private static readonly string[] Shapes = ["Cross", "Dot", "Circle", "Cross + Dot", "T-shape", "Diagonal", "Chevron", "3-ray", "Square", "Predator", "Image"];
    private static readonly string[] BundledCrosshairs = [
        "2-sectors-dot.png", "arrow-dot.png", "chevron-big.png", "chevron-medium.png", "chevron-small.png",
        "circle-cross.png", "circle-dot.png", "cross-diag-dot.png", "cross-dot-small.png", "cross-dot.png",
        "cross-x-dot.png", "dot-2x2.png", "dot-4x4.png", "square-cross-dot.png", "t-shape-chevron.png",
        "t-shape-dot.png", "tiny-cross-1.png", "tiny-cross.png", "tiny-thick-circle.png", "tiny-thin-circle.png"
    ];

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

    public ControlWindow() : this(null) { }
    internal ControlWindow(StoreData? previewData)
    {
        previewMode=previewData!=null; data=previewData??Store.Load();
        UiText.Language = data.Language == "uk" ? "uk" : "en";
        Text = "Simple Aim Overlay"; Icon = BrandIcon.Load(); MinimumSize = new Size(900, 660); Size = new Size(1120, 780);
        StartPosition = FormStartPosition.CenterScreen; Font = AppFonts.Regular(9.5f); BackColor = DarkTheme.Window;
        data.Images.RemoveAll(x => BundledCrosshairs.Contains(x, StringComparer.OrdinalIgnoreCase));
        foreach (var profile in data.Profiles) if (profile.Aim.Shape == "Image" && BundledCrosshairs.Contains(Path.GetFileName(profile.Aim.ImagePath), StringComparer.OrdinalIgnoreCase)) profile.Aim.Shape = "Cross";
        if (data.Current.Shape == "Image" && BundledCrosshairs.Contains(Path.GetFileName(data.Current.ImagePath), StringComparer.OrdinalIgnoreCase)) data.Current.Shape = "Cross";
        BuildLayout(); LoadCurrent(); initializing = false; Apply(); PopulateImages();
        tray.Icon = BrandIcon.Load(); tray.Text = "Simple Aim Overlay"; tray.Visible = !previewMode;
        var menu = new ContextMenuStrip { BackColor = DarkTheme.Surface, ForeColor = DarkTheme.Text, Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()) };
        menu.Items.Add("Open", GlyphPainter.Create("aim", 16, DarkTheme.Accent), (_, _) => RestoreWindow()); menu.Items.Add("Show / hide", GlyphPainter.Create("visibility", 16), (_, _) => ToggleVisible()); menu.Items.Add("Exit", GlyphPainter.Create("delete", 16), (_, _) => { allowExit = true; Close(); }); tray.ContextMenuStrip = menu;
        tray.MouseDown += (_, e) => { if (e.Button == MouseButtons.Right) trayInteracting = true; };
        menu.Opening += (_, _) => { trayInteracting = true; overlay.SuspendZOrder = true; };
        menu.Closed += (_, _) => { trayInteracting = false; overlay.SuspendZOrder = false; };
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized && data.MinimizeToTray) Hide(); };
        tray.DoubleClick += (_, _) => RestoreWindow();
        holdTimer.Tick += (_, _) => PollHoldKey();
        Shown += async (_, _) => { ApplyLanguage(); RoundedUi.Apply(this); if(previewMode)return; runtimeStarted = true; RefreshRuntimeVisibility(); BindHotkey(); holdTimer.Start(); if (data.LaunchWithGame) GameLauncher.StartWatcher(); if (Environment.GetCommandLineArgs().Contains("--game")) Hide(); await CheckUpdates(false); };
        FormClosing += (_, e) =>
        {
            if (!allowExit && e.CloseReason == CloseReason.UserClosing && data.MinimizeToTrayOnClose)
            {
                e.Cancel = true; Hide(); return;
            }
            updater.Dispose(); holdTimer.Stop(); holdTimer.Dispose(); UnregisterHotKey(Handle, HotkeyId); tray.Visible = false; tray.Dispose(); overlay.Close();
        };
        ApplyTheme();
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1, caption = 0x00191510, border = 0x00191510, text = 0x00F0EAE8;
        _ = DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
        _ = DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(Handle, 36, ref text, sizeof(int));
        DarkTheme.Caption(this);
    }

    private static DarkSlider Slider(int min, int max) => new(min, max) { Dock = DockStyle.Fill };
    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(22), BackColor = DarkTheme.Window };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 65)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(root);
        var heading = new Panel { Dock = DockStyle.Fill, BackColor = DarkTheme.Window };
        heading.Controls.Add(new Label { Text = "AIM OVERLAY", Font = AppFonts.Semibold(20), ForeColor = DarkTheme.Text, AutoSize = true, Location = new Point(0, 0), BackColor = DarkTheme.Window });
        heading.Controls.Add(new Label { Text = "Build a clean, readable crosshair", ForeColor = DarkTheme.Muted, AutoSize = true, Location = new Point(2, 38), BackColor = DarkTheme.Window });
        root.Controls.Add(heading, 0, 0); root.SetColumnSpan(heading, 2);
        tabs.Dock = DockStyle.Fill;
        tabs.AddPage("Crosshair", "aim", MakeCrosshairTab()); tabs.AddPage("Editor", "edit", MakeLibraryTab()); tabs.AddPage("Settings", "settings", MakeSettingsTab());
        tabs.AddPage("Feedback","feedback",new FeedbackPanel(()=>data));
        root.Controls.Add(tabs, 0, 1);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18, 10, 0, 0), BackColor = DarkTheme.Window };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); right.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        right.Controls.Add(new Label { Text = "LIVE PREVIEW", Font = AppFonts.Semibold(9), ForeColor = DarkTheme.Muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = DarkTheme.Window }, 0, 0);
        preview.Dock = DockStyle.Fill; preview.BackColor = DarkTheme.Preview; preview.Paint += (_, e) => AimRenderer.Draw(e.Graphics, preview.ClientRectangle, data.Current, aimColor, borderColor, true); right.Controls.Add(preview, 0, 1);
        var bottom = new Panel { Dock = DockStyle.Fill, BackColor = DarkTheme.Window };
        toggleButton.Text = "Crosshair: ON"; toggleButton.Image = GlyphPainter.Create("visibility", 18, DarkTheme.Window); toggleButton.ImageAlign = ContentAlignment.MiddleLeft; toggleButton.TextImageRelation = TextImageRelation.ImageBeforeText; toggleButton.BackColor = DarkTheme.Accent; toggleButton.ForeColor = DarkTheme.Window; toggleButton.Font = AppFonts.Semibold(10); toggleButton.FlatStyle = FlatStyle.Flat; toggleButton.FlatAppearance.BorderSize = 0; toggleButton.Dock = DockStyle.Top; toggleButton.Height = 44; toggleButton.Click += (_, _) => ToggleVisible(); bottom.Controls.Add(toggleButton);
        status.Text = "F8 toggles · Right Mouse temporarily hides"; status.Dock = DockStyle.Bottom; status.Height = 42; status.ForeColor = DarkTheme.Muted; status.TextAlign = ContentAlignment.MiddleCenter; status.BackColor = DarkTheme.Window; bottom.Controls.Add(status); right.Controls.Add(bottom, 0, 2); root.Controls.Add(right, 1, 1);
    }
    private Panel Page(string name) { var p = new Panel { BackColor = DarkTheme.Surface, Padding = new Padding(18), AutoScroll = true }; return p; }
    private Panel MakeCrosshairTab()
    {
        var p = Page("Crosshair"); var grid = Grid(); p.Controls.Add(grid);
        shape.DropDownStyle = ComboBoxStyle.DropDownList; shape.Items.AddRange(Shapes); AddRow(grid, "Style", shape);
        colorButton.Text = "Aim color"; colorButton.Image = GlyphPainter.Create("color", 17); colorButton.ImageAlign = ContentAlignment.MiddleLeft; colorButton.TextImageRelation = TextImageRelation.ImageBeforeText; colorButton.TextAlign = ContentAlignment.MiddleLeft; colorButton.FlatStyle = FlatStyle.Flat; colorButton.FlatAppearance.BorderColor = DarkTheme.Border; colorButton.Click += (_, _) => PickColor(false); AddRow(grid, "Color", colorButton);
        AddSliderRow(grid, "Opacity", opacity, s => s.Opacity, "opacity");
        AddSliderRow(grid, "Size", size, s => s.Size, "size");
        AddSliderRow(grid, "Image zoom", imageScale, s => s.ImageScale, "scale");
        AddSliderRow(grid, "Thickness", thickness, s => (int)Math.Round(s.Thickness * 10), "thickness");
        AddSliderRow(grid, "Gap", gap, s => s.Gap, "gap");
        outline.Text = "Show outline"; AddRow(grid, "Outline", outline);
        outlineButton.Text = "Outline color"; outlineButton.ForeColor = DarkTheme.Text; outlineButton.FlatStyle = FlatStyle.Flat; outlineButton.FlatAppearance.BorderColor = DarkTheme.Border; outlineButton.Click += (_, _) => PickColor(true); AddRow(grid, "", outlineButton);
        AddSliderRow(grid, "Outline width", outlineWidth, s => (int)(s.OutlineWidth * 10), "outlineWidth");
        AddSliderRow(grid, "Outline opacity", outlineOpacity, s => s.OutlineOpacity, "opacity");
        var offsets = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, BackColor = DarkTheme.Surface };
        offsets.Controls.Add(new Label { Text = "X", AutoSize = true, Padding = new Padding(0, 7, 5, 0), ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface }); offsets.Controls.Add(offsetX); offsets.Controls.Add(new Label { Text = "Y", AutoSize = true, Padding = new Padding(10, 7, 5, 0), ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface }); offsets.Controls.Add(offsetY);
        AddRow(grid, "Center offset", offsets);
        monitor.DropDownStyle = ComboBoxStyle.DropDownList; AddRow(grid, "Display", monitor);
        var refresh = Button("Refresh displays", DarkTheme.SurfaceHover, DarkTheme.Text); refresh.Dock = DockStyle.Fill; refresh.Click += (_, _) => PopulateMonitors(); AddRow(grid, "", refresh);
        shape.SelectedIndexChanged += (_, _) => { imageScale.Enabled = shape.SelectedItem?.ToString() == "Image"; thickness.Enabled = gap.Enabled = !imageScale.Enabled; Apply(); }; outline.CheckedChanged += (_, _) => Apply(); offsetX.ValueChanged += (_, _) => Apply(); offsetY.ValueChanged += (_, _) => Apply(); monitor.SelectedIndexChanged += (_, _) => Apply();
        return p;
    }
    private Panel MakeLibraryTab()
    {
        var p = Page("Editor"); var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = DarkTheme.Surface };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); p.Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "Your drawings. Create a crosshair or edit one you saved. Standard styles are in Crosshair.", ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        images.Dock = DockStyle.Fill; images.IntegralHeight = false; images.BackColor = DarkTheme.Card; images.ForeColor = DarkTheme.Text; images.BorderStyle = BorderStyle.None; images.DrawMode = DrawMode.OwnerDrawFixed; images.ItemHeight = 72; images.DrawItem += DrawCrosshairItem; images.ItemDoubleClick += (_, _) => EditImage(); layout.Controls.Add(images, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight };
        buttons.BackColor = DarkTheme.Surface;
        var create = Button("New", DarkTheme.Accent, DarkTheme.Window); create.Click += (_, _) => CreateImage(); buttons.Controls.Add(create);
        var edit = Button("Edit", DarkTheme.SurfaceHover, DarkTheme.Text); edit.Click += (_, _) => EditImage(); buttons.Controls.Add(edit);
        var import = Button("Import", DarkTheme.SurfaceHover, DarkTheme.Text); import.Click += (_, _) => ImportDrawing(); buttons.Controls.Add(import);
        var export = Button("Export", DarkTheme.SurfaceHover, DarkTheme.Text); export.Click += (_, _) => ExportDrawing(); buttons.Controls.Add(export);
        var remove = Button("Delete", DarkTheme.SurfaceHover, DarkTheme.Text); remove.Click += (_, _) => RemoveImage(); buttons.Controls.Add(remove);
        layout.Controls.Add(buttons, 0, 2);
        images.SelectedIndexChanged += (_, _) =>
        {
            if (initializing || images.SelectedItem is not string filename) return;
            data.Current.ImagePath = Path.Combine(Store.Root, "crosshairs", filename);
            data.Current.TintImage = !data.OriginalColorImages.Contains(filename, StringComparer.OrdinalIgnoreCase);
            shape.SelectedItem = "Image"; imageScale.Enabled = true; Apply();
        };
        return p;
    }

    private Panel MakeSettingsTab()
    {
        var p = Page("Settings"); var grid = Grid(); p.Controls.Add(grid);
        language.Items.AddRange(["English", "Українська"]); AddRow(grid, "Language", language);
        language.SelectedIndexChanged += (_, _) => { if(initializing)return; data.Language=language.SelectedIndex==1?"uk":"en"; ApplyLanguage(); SaveData(); };
        theme.Items.AddRange(["Dark", "Light"]);AddRow(grid,"Theme",theme);
        theme.SelectedIndexChanged+=(_,_)=>{if(initializing)return;data.Theme=theme.SelectedIndex==1?"light":"dark";ApplyTheme();SaveData();};
        visible.Text = "Show crosshair"; visible.AutoSize = true; AddRow(grid, "Visibility", visible);
        AddRow(grid, "Toggle hotkey", hotkey);
        hideWhileHeld.Text = "Hide while key is held"; hideWhileHeld.AutoSize = true; AddRow(grid, "Hold to hide", hideWhileHeld);
        AddRow(grid, "Hold key", holdKey);
        startup.Text = "Launch when Windows starts"; startup.AutoSize = true; AddRow(grid, "Startup", startup);
        closeToTray.Text = "Minimize to tray when closed"; closeToTray.AutoSize = true; AddRow(grid, "Close button", closeToTray);
        minimizeToTray.Text = "Minimize to tray with the − button"; AddRow(grid, "Minimize button", minimizeToTray);
        minimizeToTray.CheckedChanged += (_, _) => { if (!initializing) { data.MinimizeToTray = minimizeToTray.Checked; SaveData(); } };
        launchWithGame.Text = "Show only in the selected game"; AddRow(grid, "Selected game", launchWithGame);
        var browseGame = Button("Browse…", DarkTheme.SurfaceHover, DarkTheme.Text);
        var gameRow = GamePathRow.Create(gameExecutable, browseGame);
        browseGame.Click += (_, _) => { using var dialog = new OpenFileDialog { Title = UiText.T("Select game executable"), Filter = UiText.T("Executable|*.exe"), CheckFileExists = true }; if (dialog.ShowDialog(this) == DialogResult.OK) { gameExecutable.Text = dialog.FileName; SaveGameStartup(); } };
        AddRow(grid, "Game EXE", gameRow);
        gameExecutable.Leave += (_, _) => { if (!launchWithGame.Checked || GameLauncher.IsValidTarget(gameExecutable.Text.Trim().Trim('"'))) SaveGameStartup(); };
        gameExecutable.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { SaveGameStartup(); e.SuppressKeyPress = true; } };
        launchWithGame.CheckedChanged += (_, _) => SaveGameStartup();
        var checkUpdates = Button("Check for updates", DarkTheme.SurfaceHover, DarkTheme.Text); checkUpdates.Click += async (_, _) => await CheckUpdates(true); AddRow(grid, "Updates", checkUpdates); AddRow(grid, "Version " + AppUpdater.CurrentVersion.ToString(3), updateStatus);
        var transfer = new FlowLayoutPanel { Dock=DockStyle.Fill };
        var exportConfig=Button("Export",DarkTheme.SurfaceHover,DarkTheme.Text);exportConfig.Click+=(_,_)=>TransferConfig(false);transfer.Controls.Add(exportConfig);
        var importConfig=Button("Import",DarkTheme.SurfaceHover,DarkTheme.Text);importConfig.Click+=(_,_)=>TransferConfig(true);transfer.Controls.Add(importConfig);
        AddRow(grid,"Configuration",transfer);
        var logs=Button("Open update logs",DarkTheme.SurfaceHover,DarkTheme.Text);logs.Click+=(_,_)=>{string folder=Path.Combine(Store.Root,"logs");Directory.CreateDirectory(folder);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder){UseShellExecute=true});};AddRow(grid,"Diagnostics",logs);
        var text = new Label { Text = "UIAccess overlay. Fullscreen support depends on the game and its graphics mode.", Dock = DockStyle.Fill, ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface, AutoSize = false };
        grid.Controls.Add(text, 0, grid.RowCount); grid.SetColumnSpan(text, 2); grid.RowCount++;
        visible.CheckedChanged += (_, _) => { if (!initializing) ToggleVisible(visible.Checked); };
        hotkey.BindingChanged += (_, _) => Apply();
        hideWhileHeld.CheckedChanged += (_, _) => { if (!initializing) Apply(); };
        holdKey.BindingChanged += (_, _) => { if (!initializing) Apply(); };
        startup.CheckedChanged += (_, _) => SetStartup();
        closeToTray.CheckedChanged += (_, _) => { if (initializing) return; data.MinimizeToTrayOnClose = closeToTray.Checked; SaveData(); };
        return p;
    }

    private void SaveData() { if(!previewMode)Store.Save(data); }
    private void ApplyTheme()
    {
        DarkTheme.Select(data.Theme=="light",this);
        RefreshColorSwatches();
        if(tray.ContextMenuStrip!=null){
            DarkTheme.RefreshMenu(tray.ContextMenuStrip);
            string[] glyphs=["aim","visibility","delete"];
            for(int i=0;i<tray.ContextMenuStrip.Items.Count;i++){var item=tray.ContextMenuStrip.Items[i];item.Image?.Dispose();item.Image=GlyphPainter.Create(glyphs[i],16,i==0?DarkTheme.Accent:DarkTheme.Text);}
        }
    }
    private void ApplyLanguage()
    {
        UiText.Language=data.Language=="uk"?"uk":"en";
        UiText.Apply(this);
        foreach(Form form in OwnedForms)UiText.Apply(form);
        if(tray.ContextMenuStrip is { } menu) {
            string[] names=["Open","Show / hide","Exit"];
            for(int i=0;i<Math.Min(names.Length,menu.Items.Count);i++)menu.Items[i].Text=UiText.T(names[i]);
        }
        monitor.DropDownWidth=Math.Min(Screen.FromControl(this).WorkingArea.Width,Math.Max(monitor.Width,monitor.Items.Cast<MonitorItem>().Select(item=>TextRenderer.MeasureText(UiText.T(item.ToString()),monitor.Font).Width+48).DefaultIfEmpty(monitor.Width).Max()));
    }
    private void SaveGameStartup()
    {
        if (initializing) return;
        string selected = gameExecutable.Text.Trim().Trim('"');
        if (launchWithGame.Checked && !GameLauncher.IsValidTarget(selected)) { DarkMessage.Show(this, "Choose an existing game EXE, or enter its process filename (for example TslGame.exe).", "Game startup"); return; }
        try { GameLauncher.Configure(launchWithGame.Checked); data.GameExecutable = selected; data.LaunchWithGame = launchWithGame.Checked; SaveData(); RefreshRuntimeVisibility(); if (data.LaunchWithGame) GameLauncher.StartWatcher(); }
        catch (Exception ex) { DarkMessage.Show(this, ex.Message, "Game startup"); }
    }
    private Task CheckUpdates(bool manual) => updater.CheckAsync(this, manual, text => { if (!IsDisposed) updateStatus.Text = text; }, () => { allowExit = true; Close(); });
    private TableLayoutPanel Grid() => new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 0, Padding = new Padding(0, 2, 4, 2), BackColor = DarkTheme.Surface }.Also(g => { g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); });
    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        // Nested button margins count towards the row height, including wrapped rows.
        bool flowing = control is FlowLayoutPanel;
        int row = grid.RowCount++; grid.RowStyles.Add(flowing ? new RowStyle(SizeType.AutoSize) : new RowStyle(SizeType.Absolute, 45));
        var l = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface };
        // Layout containers share the page background; only inputs need a dark fill.
        control.BackColor = control is Button ? control.BackColor : control is FlowLayoutPanel or TableLayoutPanel ? DarkTheme.Surface : DarkTheme.Input;
        control.ForeColor = control is Button ? control.ForeColor : DarkTheme.Text;
        control.Dock = DockStyle.Fill; grid.Controls.Add(l, 0, row); grid.Controls.Add(control, 1, row);
        if (control is FlowLayoutPanel flow) {
            flow.AutoSize = true; flow.AutoSizeMode = AutoSizeMode.GrowAndShrink; flow.Dock = DockStyle.Top;
            flow.MinimumSize = new Size(0, 39);
        }
    }
    private void AddSliderRow(TableLayoutPanel g, string label, DarkSlider bar, Func<AimSettings, int> get, string key)
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 }; host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        var value = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface }; host.BackColor = DarkTheme.Surface; bar.BackColor = DarkTheme.Surface; host.Controls.Add(bar, 0, 0); host.Controls.Add(value, 1, 0);
        string ValueText() => key is "opacity" or "scale" ? $"{bar.Value}%" : key is "thickness" or "outlineWidth" ? (bar.Value / 10f).ToString("0.0") : bar.Value.ToString();
        value.Text=ValueText();
        value.Font = AppFonts.Semibold(9); bar.ValueChanged += (_, _) => { value.Text=ValueText(); if (!initializing) Apply(); };
        AddRow(g, label, host); host.BackColor = DarkTheme.Surface; host.Margin = Padding.Empty; bar.Margin = Padding.Empty; value.Margin = Padding.Empty;
    }
    private static Button Button(string label, Color back, Color fore)
    {
        bool primary = back == DarkTheme.Accent;
        var button = new RoundedButton { Text = label, BackColor = primary ? DarkTheme.Accent : DarkTheme.SurfaceHover, ForeColor = primary ? DarkTheme.Window : fore, FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 38, Padding = new Padding(12, 4, 12, 4), Margin = new Padding(0, 4, 8, 4), Cursor = Cursors.Hand, Font = AppFonts.Semibold(9) };
        button.FlatAppearance.BorderSize = primary ? 0 : 1; button.FlatAppearance.BorderColor = DarkTheme.Border; button.FlatAppearance.MouseOverBackColor = primary ? DarkTheme.AccentHover : DarkTheme.Border;
        string? glyph = label.StartsWith("Refresh", StringComparison.OrdinalIgnoreCase) ? "refresh" : label.StartsWith("New", StringComparison.OrdinalIgnoreCase) ? "image" : label.StartsWith("Edit", StringComparison.OrdinalIgnoreCase) ? "edit" : label.StartsWith("Import", StringComparison.OrdinalIgnoreCase) ? "import" : label.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ? "delete" : label.StartsWith("Save", StringComparison.OrdinalIgnoreCase) ? "save" : label.StartsWith("Load", StringComparison.OrdinalIgnoreCase) ? "load" : null;
        if (glyph != null) { button.Image = GlyphPainter.Create(glyph, 17, primary ? DarkTheme.Window : DarkTheme.Text); button.ImageAlign = ContentAlignment.MiddleLeft; button.TextImageRelation = TextImageRelation.ImageBeforeText; }
        return button;
    }

    private void LoadCurrent()
    {
        var s = data.Current;
        language.SelectedIndex=data.Language=="uk"?1:0;
        theme.SelectedIndex=data.Theme=="light"?1:0;
        shape.SelectedItem = Shapes.Contains(s.Shape) ? s.Shape : "Cross";
        if (s.Shape == "Image") s.TintImage = !data.OriginalColorImages.Contains(Path.GetFileName(s.ImagePath), StringComparer.OrdinalIgnoreCase);
        SetColor(s.Color, false); SetColor(s.OutlineColor, true);
        size.Value = Math.Clamp(s.Size, size.Minimum, size.Maximum); imageScale.Value = Math.Clamp(s.ImageScale, imageScale.Minimum, imageScale.Maximum); imageScale.Enabled = s.Shape == "Image"; thickness.Value = Math.Clamp((int)Math.Round(s.Thickness * 10), thickness.Minimum, thickness.Maximum); gap.Value = Math.Clamp(s.Gap, gap.Minimum, gap.Maximum); opacity.Value = Math.Clamp(s.Opacity, opacity.Minimum, opacity.Maximum);
        outline.Checked = s.Outline; visible.Checked = s.Visible; offsetX.Value = Math.Clamp(s.OffsetX, (int)offsetX.Minimum, (int)offsetX.Maximum); offsetY.Value = Math.Clamp(s.OffsetY, (int)offsetY.Minimum, (int)offsetY.Maximum);
        desiredVisible = s.Visible;
        hotkey.Binding = s.Hotkey;
        hideWhileHeld.Checked = s.HideWhileHeld; holdKey.Binding = NormalizeHoldKey(s.HoldKey);
        outlineWidth.Value = Math.Clamp((int)(s.OutlineWidth * 10),5,100); outlineOpacity.Value = Math.Clamp(s.OutlineOpacity,0,100);
        startup.Checked = s.StartWithWindows;
        closeToTray.Checked = data.MinimizeToTrayOnClose; minimizeToTray.Checked = data.MinimizeToTray; launchWithGame.Checked = data.LaunchWithGame; gameExecutable.Text = data.GameExecutable;
        PopulateMonitors();
    }
    private void PopulateMonitors()
    {
        var wanted = data.Current.Monitor; monitor.Items.Clear();
        foreach (var scr in Screen.AllScreens) monitor.Items.Add(new MonitorItem(scr, monitor.Items.Count + 1));
        int textWidth = monitor.Items.Cast<MonitorItem>().Select(item => TextRenderer.MeasureText(UiText.T(item.ToString()), monitor.Font).Width + 48).DefaultIfEmpty(monitor.Width).Max();
        monitor.DropDownWidth = Math.Min(Screen.FromControl(this).WorkingArea.Width, Math.Max(monitor.Width, textWidth));
        int i = Enumerable.Range(0, monitor.Items.Count).FirstOrDefault(k => ((MonitorItem)monitor.Items[k]!).Screen.DeviceName == wanted, -1);
        if (i < 0) i = Array.FindIndex(Screen.AllScreens, x => x.Primary);
        if (i >= 0 && i < monitor.Items.Count) monitor.SelectedIndex = i;
    }

    private void PopulateImages()
    {
        string folder = Path.Combine(Store.Root, "crosshairs"); Directory.CreateDirectory(folder);
        images.Items.Clear(); foreach (var x in data.Images) if (File.Exists(Path.Combine(folder, x))) images.Items.Add(x);
        SaveData();
    }
    private void DrawCrosshairItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= images.Items.Count) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var background = new SolidBrush(selected ? DarkTheme.SurfaceHover : DarkTheme.Card)) e.Graphics.FillRectangle(background, e.Bounds);
        if (selected) using (var accent = new SolidBrush(DarkTheme.Accent)) e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top + 8, 3, e.Bounds.Height - 16);
        string name = images.Items[e.Index]?.ToString() ?? "";
        var imageBox = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + 7, 56, 56);
        string path = Path.Combine(Store.Root, "crosshairs", name);
        try
        {
            using var image = Image.FromFile(path);
            var source = AimRenderer.ImageContentBounds(image);
            float scale = Math.Min((float)imageBox.Width / source.Width, (float)imageBox.Height / source.Height);
            int width = Math.Max(1, (int)(source.Width * scale)), height = Math.Max(1, (int)(source.Height * scale));
            var target = new Rectangle(imageBox.Left + (imageBox.Width - width) / 2, imageBox.Top + (imageBox.Height - height) / 2, width, height);
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            bool tint = !data.OriginalColorImages.Contains(name, StringComparer.OrdinalIgnoreCase);
            AimRenderer.DrawCrosshairImage(e.Graphics, image, source, target, aimColor, 255, tint);
        }
        catch { }
        TextRenderer.DrawText(e.Graphics, Path.GetFileNameWithoutExtension(name), e.Font ?? SystemFonts.MessageBoxFont, new Point(imageBox.Right + 10, e.Bounds.Top + 25), e.ForeColor, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        e.DrawFocusRectangle();
    }

    private void Apply()
    {
        if (initializing) return;
        var s = data.Current;
        s.Shape = shape.SelectedItem?.ToString() ?? "Cross"; s.Color = ColorTranslator.ToHtml(aimColor); s.OutlineColor = ColorTranslator.ToHtml(borderColor);
        s.Outline = outline.Checked; s.Size = size.Value; s.ImageScale = imageScale.Value; s.Thickness = thickness.Value / 10f; s.Gap = gap.Value; s.Opacity = opacity.Value;
        s.OffsetX = (int)offsetX.Value; s.OffsetY = (int)offsetY.Value; desiredVisible = visible.Checked; s.Visible = desiredVisible;
        s.HideWhileHeld = hideWhileHeld.Checked; s.HoldKey = holdKey.Binding;
        s.OutlineWidth = outlineWidth.Value / 10f; s.OutlineOpacity = outlineOpacity.Value;
        toggleButton.Text = s.Visible ? "Crosshair: ON" : "Crosshair: OFF";
        toggleButton.BackColor = s.Visible ? DarkTheme.Accent : DarkTheme.SurfaceHover;
        images.Invalidate(); RefreshColorSwatches();
        if (monitor.SelectedItem is MonitorItem mi) s.Monitor = mi.Screen.DeviceName;
        s.Hotkey = hotkey.Binding;
        RefreshRuntimeVisibility(); preview.Invalidate(); SaveData(); BindHotkey();
    }
    private Screen SelectedScreen() => monitor.SelectedItem is MonitorItem mi ? mi.Screen : Screen.PrimaryScreen ?? Screen.AllScreens[0];
    private void PickColor(bool isOutline)
    {
        using var d = new DarkColorPicker { Color = isOutline ? borderColor : aimColor, FullOpen = true };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        if (isOutline) { borderColor = d.Color;  } else { aimColor = d.Color;  images.Invalidate(); preview.Invalidate(); }
        Apply();
    }
    private void SetColor(string value, bool isOutline)
    {
        Color c; try { c = ColorTranslator.FromHtml(value); } catch { c = isOutline ? Color.FromArgb(16, 24, 32) : Color.LimeGreen; }
        if (isOutline) { borderColor = c;  } else { aimColor = c;  }
    }
    private void ToggleVisible() => ToggleVisible(!data.Current.Visible);
    private void ToggleVisible(bool value)
    {
        desiredVisible = value; data.Current.Visible = value; visible.Checked = value;
        toggleButton.Text = value ? "Crosshair: ON" : "Crosshair: OFF";
        toggleButton.BackColor = value ? DarkTheme.Accent : DarkTheme.SurfaceHover;
        Apply();
    }
    private void RefreshRuntimeVisibility()
    {
        var runtimeSettings = Copy(data.Current);
        bool hiddenByHold = runtimeSettings.HideWhileHeld && holdDown;
        gameForeground = !data.LaunchWithGame || GameLauncher.IsForegroundTarget(data.GameExecutable);
        runtimeSettings.Visible = GameLauncher.ShouldShow(runtimeSettings.Visible, hiddenByHold, data.LaunchWithGame, gameForeground);
        if (runtimeStarted) overlay.UpdateAim(runtimeSettings, aimColor, borderColor, SelectedScreen());
        status.Text = StatusText(hiddenByHold, runtimeSettings);
    }
    private string StatusText(bool? hidden = null, AimSettings? current = null)
    {
        current ??= data.Current;
        if (data.Current.Visible && data.LaunchWithGame && !gameForeground)
            return "Crosshair hidden · switch to " + Path.GetFileName(data.GameExecutable);
        hidden ??= current.HideWhileHeld && holdDown;
        string holdName = HoldKeyLabel(current.HoldKey);
        string aim = hidden.Value ? $"{holdName} held · crosshair hidden" : current.Visible ? $"{current.Hotkey} toggles · {holdName} temporarily hides" : $"Crosshair off · {current.Hotkey} toggles visibility";
        return aim;
    }
    private void BindHotkey()
    {
        if (!IsHandleCreated) return;
        UnregisterHotKey(Handle, HotkeyId);
        toggleWasDown = KeyBindingButton.IsDown(data.Current.Hotkey);
    }
    private void PollHoldKey()
    {
        // Check focus even while a dialog or tray menu is open. Never persist this
        // temporary visibility change to the user's on/off preference.
        if (Environment.TickCount64 >= nextGameFocusCheck) {
            nextGameFocusCheck = Environment.TickCount64 + 100;
            bool active = !data.LaunchWithGame || GameLauncher.IsForegroundTarget(data.GameExecutable);
            if (active != gameForeground) RefreshRuntimeVisibility();
        }
        bool toggleDown = KeyBindingButton.IsDown(data.Current.Hotkey);
        if (trayInteracting || tray.ContextMenuStrip?.Visible == true || OwnedForms.Any(f=>f.Visible) || (ContainsFocus && GetFocusedControl(this) is TextBoxBase)) { toggleWasDown=toggleDown; return; }
        if(toggleDown&&!toggleWasDown)ToggleVisible();
        toggleWasDown=toggleDown;
        bool pressed = hideWhileHeld.Checked && desiredVisible && KeyBindingButton.IsDown(holdKey.Binding);
        if (pressed == holdDown) return;
        holdDown = pressed;
        RefreshRuntimeVisibility();
    }
    private static Control GetFocusedControl(Control root) => root.Controls.Cast<Control>().FirstOrDefault(c=>c.ContainsFocus) is Control child ? GetFocusedControl(child) : root;
    private static string NormalizeHoldKey(string key) => key switch
    {
        "ПКМ" => "Right Mouse", "ЛКМ" => "Left Mouse", "Середня кнопка" => "Middle Mouse", "Бічна кнопка X1" => "Mouse X1", "Бічна кнопка X2" => "Mouse X2", _ => key
    };
    private static string HoldKeyLabel(string key) => NormalizeHoldKey(key);
    private static int HoldKeyCode(string key) => key switch
    {
        "Left Mouse" or "ЛКМ" => 0x01, "Right Mouse" or "ПКМ" => 0x02, "Middle Mouse" or "Середня кнопка" => 0x04, "Mouse X1" or "Бічна кнопка X1" => 0x05, "Mouse X2" or "Бічна кнопка X2" => 0x06,
        "Space" => 0x20, "Shift" => 0x10, "Ctrl" => 0x11, "Alt" => 0x12,
        _ when Enum.TryParse<Keys>(key, out var parsed) => (int)parsed,
        _ => 0x02
    };
    protected override void WndProc(ref Message m) { if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId) ToggleVisible(); base.WndProc(ref m); }
    private void SetStartup()
    {
        if (initializing) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (startup.Checked) key?.SetValue("SimpleAimOverlay", $"\"{Application.ExecutablePath}\""); else key?.DeleteValue("SimpleAimOverlay", false);
            data.Current.StartWithWindows = startup.Checked; SaveData();
        }
        catch { DarkMessage.Show(this, "Could not update the startup setting.", "Simple Aim Overlay", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void RemoveImage()
    {
        if (images.SelectedItem is not string name) return;
        if (BundledCrosshairs.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            DarkMessage.Show(this, "Bundled crosshairs cannot be deleted. You can remove your own images.", "Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string p = Path.Combine(Store.Root, "crosshairs", name); try { if (File.Exists(p)) File.Delete(p); } catch { }
        data.Images.Remove(name); data.OriginalColorImages.Remove(name); if (data.Current.ImagePath == p) { data.Current.ImagePath = ""; shape.SelectedItem = "Cross"; } PopulateImages(); Apply();
    }
    private void RefreshColorSwatches()
    {
        foreach(var (button,color) in new[]{(colorButton,aimColor),(outlineButton,borderColor)}) {
            var old=button.Image;var swatch=new Bitmap(20,20);using(var g=Graphics.FromImage(swatch)){using var b=new SolidBrush(color);g.FillRectangle(b,2,2,16,16);}
            button.Image=swatch;old?.Dispose();button.BackColor=DarkTheme.Input;button.ForeColor=DarkTheme.Text;button.TextImageRelation=TextImageRelation.ImageBeforeText;button.ImageAlign=ContentAlignment.MiddleLeft;button.TextAlign=ContentAlignment.MiddleLeft;
        }
    }
    private void ImportDrawing() {
        using var dialog=new OpenFileDialog{Filter=UiText.T("Crosshair drawing|*.aim;*.png"),Title=UiText.T("Import drawing")};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try {
            if(new FileInfo(dialog.FileName).Length>12_000_000)throw new InvalidDataException("Drawing is too large.");
            var drawing=Path.GetExtension(dialog.FileName).Equals(".png",StringComparison.OrdinalIgnoreCase)?ConfigurationTransfer.ReadDrawing(dialog.FileName,true):JsonSerializer.Deserialize<ConfigurationTransfer.Drawing>(File.ReadAllText(dialog.FileName))??throw new InvalidDataException("Invalid drawing.");
            string name=ConfigurationTransfer.InstallDrawing(drawing);data.Images.Add(name);if(!drawing.Tint)data.OriginalColorImages.Add(name);
            PopulateImages();images.SelectedItem=name;Apply();
        }catch(Exception ex){DarkMessage.Show(this,ex.Message,"Import failed");}
    }
    private void ExportDrawing() {
        if(images.SelectedItem is not string name)return;
        using var dialog=new SaveFileDialog{Filter=UiText.T("Shareable crosshair|*.aim|PNG image|*.png"),FileName=Path.GetFileNameWithoutExtension(name),DefaultExt="aim"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try {
            var drawing=ConfigurationTransfer.ReadDrawing(Path.Combine(Store.Root,"crosshairs",name),!data.OriginalColorImages.Contains(name));
            if(Path.GetExtension(dialog.FileName).Equals(".png",StringComparison.OrdinalIgnoreCase)){using var image=ConfigurationTransfer.Decode(drawing);image.Save(dialog.FileName,ImageFormat.Png);}
            else File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(drawing));
        }catch(Exception ex){DarkMessage.Show(this,ex.Message,"Export failed");}
    }
    private void TransferConfig(bool import) {
        using FileDialog dialog=import?new OpenFileDialog():new SaveFileDialog{FileName="SimpleAimOverlay.aimconfig"};
        dialog.Filter=UiText.T("Aim Overlay configuration|*.aimconfig");dialog.DefaultExt="aimconfig"; dialog.Title=UiText.T(import?"Import configuration":"Export configuration");
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{
            if(!import){ConfigurationTransfer.Export(dialog.FileName,data);return;}
            var incoming=ConfigurationTransfer.Import(dialog.FileName);
            ConfigurationTransfer.Backup();
            incoming.Current.StartWithWindows=data.Current.StartWithWindows;incoming.LaunchWithGame=data.LaunchWithGame;incoming.GameExecutable=data.GameExecutable;
            data=incoming;initializing=true;LoadCurrent();PopulateImages();initializing=false;ApplyLanguage();ApplyTheme();Apply();
        }catch(Exception ex){initializing=false;DarkMessage.Show(this,ex.Message,"Configuration");}
    }
    private void CreateImage() => OpenPixelEditor(null, "New crosshair");
    private void EditImage()
    {
        if (images.SelectedItem is not string name) { DarkMessage.Show(this, "Select a crosshair first.", "Editor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        OpenPixelEditor(Path.Combine(Store.Root, "crosshairs", name), Path.GetFileNameWithoutExtension(name));
    }
    private void OpenPixelEditor(string? path, string title)
    {
        using var editor = new PixelCrosshairEditor(path, aimColor) { TintForOverlay = path == null || !data.OriginalColorImages.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase) };
        if (editor.ShowDialog(this) != DialogResult.OK || editor.ResultImage == null) return;
        string folder = Path.Combine(Store.Root, "crosshairs"); Directory.CreateDirectory(folder);
        string chosenName = editor.DrawingName;
        string safe = string.Concat(chosenName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch));
        string name = $"{safe}-edit-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        string destination = Path.Combine(folder, name);
        using (editor.ResultImage) editor.ResultImage.Save(destination, ImageFormat.Png);
        if (!data.Images.Contains(name, StringComparer.OrdinalIgnoreCase)) data.Images.Add(name);
        data.Current.ImagePath = destination; data.Current.TintImage = editor.TintForOverlay;
        if (editor.TintForOverlay) data.OriginalColorImages.Remove(name);
        else if (!data.OriginalColorImages.Contains(name, StringComparer.OrdinalIgnoreCase)) data.OriginalColorImages.Add(name);
        PopulateImages(); images.SelectedItem = name; shape.SelectedItem = "Image"; imageScale.Enabled = true; Apply();
    }



    private static AimSettings Copy(AimSettings x) => JsonSerializer.Deserialize<AimSettings>(JsonSerializer.Serialize(x)) ?? new AimSettings();
    private void RestoreWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }

    private sealed class MonitorItem(Screen screen, int number) {
        public Screen Screen { get; } = screen;
        private readonly string label = DisplayDetails.Describe(screen,number);
        public override string ToString() => label;
    }
}

internal sealed class OverlayWindow : Form
{
    private const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
    private const int ULW_ALPHA = 2, AC_SRC_ALPHA = 1, BI_RGB = 0, DIB_RGB_COLORS = 0;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_NOOWNERZORDER = 0x0200;
    private static readonly IntPtr HwndTopmost = new(-1);
    private AimSettings settings = new(); private Color color = Color.LimeGreen, outline = Color.Black; private Screen screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
    private readonly System.Windows.Forms.Timer topmostTimer = new() { Interval = 750 };
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public bool SuspendZOrder { get; set; }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE; return p; } }
    public OverlayWindow()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; Bounds = screen.Bounds;
        topmostTimer.Tick += (_, _) => MaintainOverlay();
        FormClosed += (_, _) => { topmostTimer.Stop(); topmostTimer.Dispose(); };
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct Size2 { public int Width, Height; public Size2(int width, int height) { Width = width; Height = height; } }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct Blend { public byte Operation, Flags, SourceAlpha, AlphaFormat; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct BitmapInfoHeader
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int XPelsPerMeter, YPelsPerMeter; public uint ColorsUsed, ColorsImportant;
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct BitmapInfo { public BitmapInfoHeader Header; public uint Color; }

    [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteDC(IntPtr dc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteObject(IntPtr obj);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr destinationDc, ref Point destination, ref Size2 size, IntPtr sourceDc, ref Point source, int colorKey, ref Blend blend, int flags);

    public void UpdateAim(AimSettings s, Color c, Color o, Screen target)
    {
        settings = s; color = c; outline = o; screen = target;
        if (Bounds != screen.Bounds) Bounds = screen.Bounds;
        TopMost = true;
        if (s.Visible) { if (!Visible) Show(); if (IsHandleCreated) { MaintainOverlay(); RenderLayered(); } }
        else if (Visible) Hide();
    }
    protected override void OnShown(EventArgs e) { base.OnShown(e); MaintainOverlay(); RenderLayered(); topmostTimer.Start(); }
    private void MaintainOverlay()
    {
        if (!IsHandleCreated || !Visible || SuspendZOrder) return;
        var current = Screen.AllScreens.FirstOrDefault(x => x.DeviceName == screen.DeviceName) ?? Screen.PrimaryScreen ?? screen;
        bool resized = screen.Bounds != current.Bounds;
        screen = current;
        if (Bounds != screen.Bounds) Bounds = screen.Bounds;
        SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        if (resized) RenderLayered();
    }
    protected override void OnPaint(PaintEventArgs e) { }

    private void RenderLayered()
    {
        if (!IsHandleCreated || !settings.Visible || ClientSize.Width < 1 || ClientSize.Height < 1) return;
        using var canvas = new Bitmap(ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(canvas))
        {
            graphics.Clear(Color.Transparent);
            AimRenderer.Draw(graphics, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), settings, color, outline, false);
        }
        var rect = new Rectangle(0, 0, canvas.Width, canvas.Height);
        var data = canvas.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        IntPtr dc = IntPtr.Zero, bitmap = IntPtr.Zero, oldBitmap = IntPtr.Zero;
        try
        {
            var info = new BitmapInfo { Header = new BitmapInfoHeader { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<BitmapInfoHeader>(), Width = canvas.Width, Height = -canvas.Height, Planes = 1, BitCount = 32, Compression = BI_RGB } };
            bitmap = CreateDIBSection(IntPtr.Zero, ref info, DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) return;
            dc = CreateCompatibleDC(IntPtr.Zero); if (dc == IntPtr.Zero) return;
            oldBitmap = SelectObject(dc, bitmap);
            int stride = canvas.Width * 4; var row = new byte[stride];
            for (int y = 0; y < canvas.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, stride);
                System.Runtime.InteropServices.Marshal.Copy(row, 0, IntPtr.Add(bits, y * stride), stride);
            }
            var dest = new Point(screen.Bounds.Left, screen.Bounds.Top); var src = new Point(0, 0); var size = new Size2(canvas.Width, canvas.Height);
            var blend = new Blend { Operation = 0, Flags = 0, SourceAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
            UpdateLayeredWindow(Handle, IntPtr.Zero, ref dest, ref size, dc, ref src, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            canvas.UnlockBits(data);
            if (dc != IntPtr.Zero && oldBitmap != IntPtr.Zero) SelectObject(dc, oldBitmap);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (dc != IntPtr.Zero) DeleteDC(dc);
        }
    }
}

internal static class AimRenderer
{
    private static readonly Dictionary<string, Bitmap> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    public static Rectangle ImageContentBounds(Image image)
    {
        if (image is not Bitmap bitmap) return new Rectangle(0, 0, image.Width, image.Height);
        bool hasTransparency = false;
        for (int y = 0; y < bitmap.Height && !hasTransparency; y++)
            for (int x = 0; x < bitmap.Width; x++) if (bitmap.GetPixel(x, y).A < 250) { hasTransparency = true; break; }
        if (!hasTransparency) return new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++) if (bitmap.GetPixel(x, y).A > 8) { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
        return right < left || bottom < top ? new Rectangle(0, 0, bitmap.Width, bitmap.Height) : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    public static void DrawCrosshairImage(Graphics graphics, Image image, Rectangle source, Rectangle destination, Color color, int opacity, bool tint)
    {
        float alpha = Math.Clamp(opacity, 0, 255) / 255f;
        float[][] values =
        [
            [tint ? 0 : 1, 0, 0, 0, 0],
            [0, tint ? 0 : 1, 0, 0, 0],
            [0, 0, tint ? 0 : 1, 0, 0],
            [tint ? color.R / 255f : 0, tint ? color.G / 255f : 0, tint ? color.B / 255f : 0, alpha, 0],
            [0, 0, 0, 0, 1]
        ];
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix(values), ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        graphics.DrawImage(image, destination, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
    }

    public static void Draw(Graphics g, Rectangle bounds, AimSettings s, Color color, Color outline, bool preview)
    {
        if(s.Shape is "Cross" or "Cross + Dot" or "T-shape" or "Predator" && Math.Abs(s.Thickness-MathF.Round(s.Thickness))<.001f) {
            DrawCrispCross(g,bounds,s,color,outline,preview);return;
        }
        if(s.Shape=="Image" || Math.Abs(s.Thickness-MathF.Round(s.Thickness))<.001f){DrawCore(g,bounds,s,color,outline,preview);return;}
        float scale=preview?Math.Min(bounds.Width,bounds.Height)/230f:1;
        // Render just the crosshair at 10×, so every 0.1px step has distinct coverage.
        int side=2*(int)Math.Ceiling((s.Size/2f+s.Gap+s.Thickness+s.OutlineWidth+4)*scale);
        side=Math.Clamp(side,8,1024);
        using var high=new Bitmap(side*10,side*10,PixelFormat.Format32bppPArgb);
        using(var graphics=Graphics.FromImage(high)) {
            // Center the stroke inside a destination pixel, not between two rows.
            graphics.TranslateTransform(5,5);
            DrawCore(graphics,new Rectangle(0,0,high.Width,high.Height),s,color,outline,false,scale*10,true);
        }
        using var resolved=ResolveCoverage(high,10);
        var state=g.Save();
        try {
            // A second interpolation would blur the opaque core again.
            g.DrawImageUnscaled(resolved,(int)MathF.Round(bounds.Width/2f+s.OffsetX*scale-side/2f),(int)MathF.Round(bounds.Height/2f+s.OffsetY*scale-side/2f));
        }finally{g.Restore(state);}
    }
    private static void DrawCrispCross(Graphics g,Rectangle bounds,AimSettings s,Color color,Color outline,bool preview)
    {
        float scale=preview?Math.Min(bounds.Width,bounds.Height)/230f:1;
        int cx=bounds.Left+(int)MathF.Floor(bounds.Width/2f+s.OffsetX*scale);
        int cy=bounds.Top+(int)MathF.Floor(bounds.Height/2f+s.OffsetY*scale);
        int width=Math.Max(1,(int)MathF.Round(s.Thickness*scale));
        int arm=Math.Max(1,(int)MathF.Round(s.Size*scale/2));
        int gap=Math.Max(0,(int)MathF.Round(s.Gap*scale));
        // An even-width stroke straddles the center boundary. An odd-width
        // stroke contains the center pixel. Neither uses half-covered pixels.
        int x=cx-width/2,y=cy-width/2;
        int length=arm+width%2;
        var arms=new List<Rectangle>{new(cx-gap-arm,y,length,width),new(cx+gap,y,length,width)};
        if(s.Shape!="Predator")arms.Add(new(x,cy-gap-arm,width,length));
        if(s.Shape!="T-shape")arms.Add(new(x,cy+gap,width,length));
        using var core=new Region();core.MakeEmpty();
        using var border=new Region();border.MakeEmpty();
        int padding=Math.Max(1,(int)MathF.Round(Math.Clamp(s.OutlineWidth,.5f,10)*scale));
        foreach(var rect in arms){core.Union(rect);var outer=rect;outer.Inflate(padding,padding);border.Union(outer);}
        if(s.Shape is "Cross + Dot" or "Predator"){
            int diameter=Math.Max(2,(int)MathF.Round(width*1.4f));
            // Match parity: an even stroke is centered between pixels, while
            // an odd stroke is centered on a pixel. The dot must use that same center.
            if(diameter%2!=width%2)diameter++;
            UnionPixelDisk(core,cx-diameter/2,cy-diameter/2,diameter);
            UnionPixelDisk(border,cx-diameter/2-padding,cy-diameter/2-padding,diameter+2*padding);
        }
        int alpha=(int)Math.Clamp(color.A*s.Opacity/100f,0,255);
        var state=g.Save();
        try {
            g.SmoothingMode=SmoothingMode.None;g.PixelOffsetMode=PixelOffsetMode.None;
            if(s.Outline){
                // Draw the outline outside the core so opacity doesn't build
                // up at the center or where the crosshair arms intersect.
                border.Exclude(core);
                using var edge=new SolidBrush(Color.FromArgb(alpha*Math.Clamp(s.OutlineOpacity,0,100)/100,outline));g.FillRegion(edge,border);
            }
            using var fill=new SolidBrush(Color.FromArgb(alpha,color));g.FillRegion(fill,core);
        }finally{g.Restore(state);}
    }
    private static void UnionPixelDisk(Region region,int left,int top,int diameter)
    {
        // Rasterize mirrored pixel centers explicitly; GDI ellipse regions can
        // round opposite edges differently on these very small circles.
        for(int row=0;row<diameter;row++){
            int dy=2*row+1-diameter,start=0;
            while(start<diameter){int dx=2*start+1-diameter;if(dx*dx+dy*dy<=diameter*diameter)break;start++;}
            int count=diameter-2*start;
            if(count>0)region.Union(new Rectangle(left+start,top+row,count,1));
        }
    }
    private static Bitmap ResolveCoverage(Bitmap source,int factor)
    {
        var result=new Bitmap(source.Width/factor,source.Height/factor,PixelFormat.Format32bppPArgb);
        var input=source.LockBits(new Rectangle(0,0,source.Width,source.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        try {
            var output=result.LockBits(new Rectangle(0,0,result.Width,result.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            try {
                byte[] row=new byte[source.Width*4], resolved=new byte[result.Width*4];int[] sums=new int[resolved.Length];int samples=factor*factor;
                for(int y=0;y<result.Height;y++) {
                    Array.Clear(sums);
                    for(int dy=0;dy<factor;dy++) {
                        System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(input.Scan0,(y*factor+dy)*input.Stride),row,0,row.Length);
                        for(int x=0;x<result.Width;x++)for(int dx=0;dx<factor;dx++)for(int c=0;c<4;c++)sums[x*4+c]+=row[(x*factor+dx)*4+c];
                    }
                    for(int i=0;i<resolved.Length;i++)resolved[i]=(byte)((sums[i]+samples/2)/samples);
                    System.Runtime.InteropServices.Marshal.Copy(resolved,0,IntPtr.Add(output.Scan0,y*output.Stride),resolved.Length);
                }
            }finally{result.UnlockBits(output);}
        }catch{result.Dispose();throw;}
        finally{source.UnlockBits(input);}
        return result;
    }
    private static void DrawCore(Graphics g, Rectangle bounds, AimSettings s, Color color, Color outline, bool preview, float? explicitScale=null, bool ignoreOffset=false)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float scale = explicitScale ?? (preview ? Math.Min(bounds.Width, bounds.Height) / 230f : 1f);
        float cx = bounds.Width / 2f + (ignoreOffset?0:s.OffsetX * scale), cy = bounds.Height / 2f + (ignoreOffset?0:s.OffsetY * scale);
        float arm = s.Size * scale / 2f, gap = s.Gap * scale, width = s.Thickness * scale;
        // Gap moves complete arms away from the center without reversing endpoints.
        float reach = arm + gap;
        if (s.Thickness == 1) {
            g.SmoothingMode = SmoothingMode.None; g.PixelOffsetMode = PixelOffsetMode.None;
            cx = MathF.Round(cx); cy = MathF.Round(cy); width = Math.Max(1, MathF.Round(width));
        }
        int alpha = (int)Math.Clamp(color.A * s.Opacity / 100f, 0, 255);
        using var main = new Pen(Color.FromArgb(alpha, color), width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var edge = new Pen(Color.FromArgb((int)(alpha * Math.Clamp(s.OutlineOpacity,0,100) / 100f), outline), width + 2 * Math.Clamp(s.OutlineWidth, .5f, 10) * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var fill = new SolidBrush(Color.FromArgb(alpha, color));
        void Line(float a, float b, float c, float d) { if (s.Thickness == 1) { a = MathF.Round(a); b = MathF.Round(b); c = MathF.Round(c); d = MathF.Round(d); } if (s.Outline) g.DrawLine(edge, a, b, c, d); g.DrawLine(main, a, b, c, d); }
        if (s.Shape == "Image" && File.Exists(s.ImagePath))
        {
            try
            {
                using var im = Image.FromFile(s.ImagePath);
                var source = new Rectangle(0, 0, im.Width, im.Height);
                float d = s.Size * (s.ImageScale / 100f) * scale;
                float ratio = Math.Min(d / source.Width, d / source.Height);
                int contentWidth = Math.Max(1, (int)(source.Width * ratio)), contentHeight = Math.Max(1, (int)(source.Height * ratio));
                int padding = 2;
                int imageWidth = contentWidth + padding * 2, imageHeight = contentHeight + padding * 2;
                var target = new Rectangle((int)(cx - imageWidth / 2f), (int)(cy - imageHeight / 2f), imageWidth, imageHeight);
                int effectiveThickness = 3;
                int gapPixels = 0;
                string key = $"{Path.GetFullPath(s.ImagePath)}|{File.GetLastWriteTimeUtc(s.ImagePath).Ticks}|{imageWidth}x{imageHeight}|{effectiveThickness}|{gapPixels}";
                if (!ImageCache.TryGetValue(key, out var rendered))
                {
                    rendered = new Bitmap(imageWidth, imageHeight, PixelFormat.Format32bppArgb);
                    using (var imageGraphics = Graphics.FromImage(rendered))
                    {
                        imageGraphics.Clear(Color.Transparent);
                        imageGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                        imageGraphics.PixelOffsetMode = PixelOffsetMode.Half;
                        imageGraphics.DrawImage(im, new Rectangle(padding, padding, contentWidth, contentHeight), source, GraphicsUnit.Pixel);
                    }
                    // Preserve the drawing exactly; edit stroke width and gaps in the editor.
                    if (ImageCache.Count >= 8)
                    {
                        foreach (var cached in ImageCache.Values) cached.Dispose();
                        ImageCache.Clear();
                    }
                    ImageCache[key] = rendered;
                }
                g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
                int imageAlpha = (int)Math.Clamp(color.A * s.Opacity / 100f, 0, 255);
                var state = g.Save();
                try
                {
                    if (s.Outline)
                    {
                        int outlineWidth = Math.Max(1, (int)Math.Round(Math.Clamp(s.OutlineWidth,.5f,10)*scale));
                        using var clip = new Region(new Rectangle(target.X - outlineWidth, target.Y - outlineWidth, target.Width + outlineWidth * 2, target.Height + outlineWidth * 2));
                        if (gapPixels > 0) clip.Exclude(new RectangleF(cx - gapPixels / 2f, cy - gapPixels / 2f, gapPixels, gapPixels));
                        g.SetClip(clip, CombineMode.Intersect);
                        for (int dy = -outlineWidth; dy <= outlineWidth; dy++)
                        for (int dx = -outlineWidth; dx <= outlineWidth; dx++)
                        {
                            if (dx * dx + dy * dy > outlineWidth * outlineWidth || (dx == 0 && dy == 0)) continue;
                            var edgeRect = new Rectangle(target.X + dx, target.Y + dy, target.Width, target.Height);
                            DrawCrosshairImage(g, rendered, new Rectangle(0, 0, imageWidth, imageHeight), edgeRect, outline, imageAlpha * Math.Clamp(s.OutlineOpacity,0,100) / 100, true);
                        }
                    }
                    if (gapPixels > 0)
                    {
                        using var clip = new Region(target); clip.Exclude(new RectangleF(cx - gapPixels / 2f, cy - gapPixels / 2f, gapPixels, gapPixels)); g.SetClip(clip, CombineMode.Intersect);
                    }
                    DrawCrosshairImage(g, rendered, new Rectangle(0, 0, imageWidth, imageHeight), target, color, imageAlpha, s.TintImage);
                }
                finally { g.Restore(state); }
                return;
            }
            catch { }
        }
        if (s.Shape == "Dot") { float d = Math.Max(2, width * 1.5f); if(s.Outline){using var b=new SolidBrush(edge.Color);float extra=2*s.OutlineWidth*scale;g.FillEllipse(b,cx-(d+extra)/2,cy-(d+extra)/2,d+extra,d+extra);} g.FillEllipse(fill, cx - d / 2, cy - d / 2, d, d); return; }
        if (s.Shape is "Circle" or "Square") { float d = arm * 2; var r = new RectangleF(cx - d / 2, cy - d / 2, d, d); if (s.Shape == "Circle") { if (s.Outline) g.DrawEllipse(edge, r); g.DrawEllipse(main, r); } else { if (s.Outline) g.DrawRectangle(edge, r.X, r.Y, r.Width, r.Height); g.DrawRectangle(main, r.X, r.Y, r.Width, r.Height); } return; }
        if (s.Shape is "Cross" or "Cross + Dot" or "T-shape" or "Predator") { Line(cx - reach, cy, cx - gap, cy); Line(cx + gap, cy, cx + reach, cy); if (s.Shape != "Predator") Line(cx, cy - reach, cx, cy - gap); if (s.Shape != "T-shape") Line(cx, cy + gap, cx, cy + reach); }
        else if (s.Shape == "Diagonal") { Line(cx - reach, cy - reach, cx - gap, cy - gap); Line(cx + gap, cy + gap, cx + reach, cy + reach); Line(cx + reach, cy - reach, cx + gap, cy - gap); Line(cx - gap, cy + gap, cx - reach, cy + reach); }
        else if (s.Shape == "Chevron") { Line(cx - arm, cy + arm * .35f, cx, cy - arm * .45f); Line(cx, cy - arm * .45f, cx + arm, cy + arm * .35f); }
        else if (s.Shape == "3-ray") { for (int i = 0; i < 3; i++) { double a = (i * 120 - 90) * Math.PI / 180; Line(cx + (float)Math.Cos(a) * gap, cy + (float)Math.Sin(a) * gap, cx + (float)Math.Cos(a) * reach, cy + (float)Math.Sin(a) * reach); } }
        if (s.Shape is "Cross + Dot" or "Predator") { float d = Math.Max(2, width * 1.4f); g.FillEllipse(fill, cx - d / 2, cy - d / 2, d, d); }
    }

    private static void AdjustImageAppearance(Bitmap image, int thickness, int gap)
    {
        int radius = thickness - 3;
        int w = image.Width, h = image.Height, count = checked(w * h), r = Math.Abs(radius);
        var rect = new Rectangle(0, 0, w, h);
        var data = image.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            var pixels = new byte[checked(stride * h)];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            var alpha = new byte[count];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) alpha[y * w + x] = pixels[y * stride + x * 4 + 3];
            var horizontal = new byte[count]; var filtered = new byte[count]; var deque = new int[Math.Max(w, h)];
            bool dilate = radius > 0;
            if (radius == 0) Array.Copy(alpha, filtered, count);
            else
            {
                for (int y = 0; y < h; y++) FilterLine(alpha, horizontal, y * w, 1, w, r, dilate, deque);
                for (int x = 0; x < w; x++) FilterLine(horizontal, filtered, x, w, h, r, dilate, deque);
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x, p = y * stride + x * 4;
                if (dilate && filtered[i] > alpha[i])
                {
                    int best = -1, bestDistance = int.MaxValue;
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int ny = y + dy; if ((uint)ny >= (uint)h) continue;
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int nx = x + dx; if ((uint)nx >= (uint)w) continue;
                            int candidate = ny * w + nx; if (alpha[candidate] == 0) continue;
                            int distance = dx * dx + dy * dy;
                            if (distance < bestDistance) { best = candidate; bestDistance = distance; }
                        }
                    }
                    if (best >= 0) { int bp = (best / w) * stride + (best % w) * 4; pixels[p] = pixels[bp]; pixels[p + 1] = pixels[bp + 1]; pixels[p + 2] = pixels[bp + 2]; }
                }
                pixels[p + 3] = gap > 0 && Math.Abs(x - w / 2f) < gap / 2f && Math.Abs(y - h / 2f) < gap / 2f ? (byte)0 : filtered[i];
            }
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally { image.UnlockBits(data); }
    }

    private static void FilterLine(byte[] source, byte[] destination, int start, int step, int length, int radius, bool dilate, int[] deque)
    {
        int head = 0, tail = 0, added = -1;
        for (int i = 0; i < length; i++)
        {
            int end = Math.Min(length - 1, i + radius);
            while (added < end)
            {
                int next = ++added; byte value = source[start + next * step];
                while (tail > head && (dilate ? source[start + deque[tail - 1] * step] <= value : source[start + deque[tail - 1] * step] >= value)) tail--;
                deque[tail++] = next;
            }
            int first = Math.Max(0, i - radius);
            while (head < tail && deque[head] < first) head++;
            destination[start + i * step] = source[start + deque[head] * step];
        }
    }
}

internal static class ControlExtensions
{
    public static T Also<T>(this T value, Action<T> action) { action(value); return value; }
}

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => DarkTheme.Surface;
    public override Color MenuBorder => DarkTheme.Border;
    public override Color MenuItemSelected => DarkTheme.SurfaceHover;
    public override Color MenuItemBorder => DarkTheme.Border;
    public override Color MenuItemSelectedGradientBegin => DarkTheme.SurfaceHover;
    public override Color MenuItemSelectedGradientEnd => DarkTheme.SurfaceHover;
    public override Color MenuItemPressedGradientBegin => DarkTheme.Input;
    public override Color MenuItemPressedGradientEnd => DarkTheme.Input;
    public override Color ImageMarginGradientBegin => DarkTheme.Surface;
    public override Color ImageMarginGradientMiddle => DarkTheme.Surface;
    public override Color ImageMarginGradientEnd => DarkTheme.Surface;
    public override Color ToolStripBorder => DarkTheme.Border;
}
