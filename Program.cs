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
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        AppFonts.Initialize();
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
    public int Size { get; set; } = 32;
    public int ImageScale { get; set; } = 200;
    public int Thickness { get; set; } = 3;
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
        try { Directory.CreateDirectory(Root); File.WriteAllText(FilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })); } catch { }
    }
}

internal sealed class ControlWindow : Form
{
    private const int WM_HOTKEY = 0x0312, HotkeyId = 0x51A1;
    private readonly StoreData data = Store.Load();
    private readonly OverlayWindow overlay = new();
    private readonly DarkTabHost tabs = new();
    private readonly DarkComboBox shape = new(), monitor = new(), hotkey = new(), holdKey = new();
    private readonly DarkListBox images = new(), profiles = new();
    private readonly DarkCheckBox outline = new(), visible = new(), startup = new(), hideWhileHeld = new(), closeToTray = new();
    private readonly DarkSlider size = Slider(8, 180), imageScale = Slider(50, 800), thickness = Slider(1, 24), gap = Slider(0, 100), opacity = Slider(10, 100);
    private readonly DarkNumberBox offsetX = new(-2000, 2000), offsetY = new(-2000, 2000);
    private readonly Button colorButton = new(), outlineButton = new();
    private readonly Button toggleButton = new();
    private readonly Panel preview = new();
    private readonly Label status = new();
    private readonly NotifyIcon tray = new();
    private readonly AppUpdater updater = new();
    private readonly Label updateStatus = new() { ForeColor = DarkTheme.Muted, AutoSize = false, Text = "Checks once at launch; manual checks are always available." };
    private readonly System.Windows.Forms.Timer holdTimer = new() { Interval = 12 };
    private bool initializing = true;
    private bool holdDown, desiredVisible = true, allowExit;
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

    public ControlWindow()
    {
        Text = "Simple Aim Overlay"; Icon = BrandIcon.Load(); MinimumSize = new Size(900, 660); Size = new Size(1120, 780);
        StartPosition = FormStartPosition.CenterScreen; Font = AppFonts.Regular(9.5f); BackColor = DarkTheme.Window;
        BuildLayout(); LoadCurrent(); initializing = false; Apply(); PopulateProfiles(); PopulateImages();
        tray.Icon = BrandIcon.Load(); tray.Text = "Simple Aim Overlay"; tray.Visible = true;
        var menu = new ContextMenuStrip { BackColor = DarkTheme.Surface, ForeColor = DarkTheme.Text, Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()) };
        menu.Items.Add("Open", GlyphPainter.Create("aim", 16, DarkTheme.Accent), (_, _) => RestoreWindow()); menu.Items.Add("Show / hide", GlyphPainter.Create("visibility", 16), (_, _) => ToggleVisible()); menu.Items.Add("Exit", GlyphPainter.Create("delete", 16), (_, _) => { allowExit = true; Close(); }); tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => RestoreWindow();
        holdTimer.Tick += (_, _) => PollHoldKey();
        Shown += async (_, _) => { overlay.Show(); BindHotkey(); holdTimer.Start(); await CheckUpdates(false); };
        FormClosing += (_, e) =>
        {
            if (!allowExit && e.CloseReason == CloseReason.UserClosing && data.MinimizeToTrayOnClose)
            {
                e.Cancel = true; Hide(); return;
            }
            updater.Dispose(); holdTimer.Stop(); holdTimer.Dispose(); UnregisterHotKey(Handle, HotkeyId); tray.Visible = false; tray.Dispose(); overlay.Close();
        };
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
        tabs.AddPage("Crosshair", "aim", MakeCrosshairTab()); tabs.AddPage("Gallery", "library", MakeLibraryTab()); tabs.AddPage("Profiles", "profiles", MakeProfilesTab()); tabs.AddPage("Settings", "settings", MakeSettingsTab());
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
        AddSliderRow(grid, "Thickness", thickness, s => s.Thickness, "thickness");
        AddSliderRow(grid, "Gap", gap, s => s.Gap, "gap");
        outline.Text = "Show outline"; AddRow(grid, "Outline", outline);
        outlineButton.Text = "Outline color"; outlineButton.ForeColor = DarkTheme.Text; outlineButton.FlatStyle = FlatStyle.Flat; outlineButton.FlatAppearance.BorderColor = DarkTheme.Border; outlineButton.Click += (_, _) => PickColor(true); AddRow(grid, "", outlineButton);
        var offsets = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, BackColor = DarkTheme.Surface };
        offsets.Controls.Add(new Label { Text = "X", AutoSize = true, Padding = new Padding(0, 7, 5, 0), ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface }); offsets.Controls.Add(offsetX); offsets.Controls.Add(new Label { Text = "Y", AutoSize = true, Padding = new Padding(10, 7, 5, 0), ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface }); offsets.Controls.Add(offsetY);
        AddRow(grid, "Center offset", offsets);
        monitor.DropDownStyle = ComboBoxStyle.DropDownList; AddRow(grid, "Display", monitor);
        var refresh = Button("Refresh displays", DarkTheme.SurfaceHover, DarkTheme.Text); refresh.Dock = DockStyle.Fill; refresh.Click += (_, _) => PopulateMonitors(); AddRow(grid, "", refresh);
        shape.SelectedIndexChanged += (_, _) => { imageScale.Enabled = shape.SelectedItem?.ToString() == "Image"; Apply(); }; outline.CheckedChanged += (_, _) => Apply(); offsetX.ValueChanged += (_, _) => Apply(); offsetY.ValueChanged += (_, _) => Apply(); monitor.SelectedIndexChanged += (_, _) => Apply();
        return p;
    }
    private Panel MakeLibraryTab()
    {
        var p = Page("Gallery"); var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = DarkTheme.Surface };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); p.Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "Choose a crosshair, import an image, or edit pixels by hand.", ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        images.Dock = DockStyle.Fill; images.IntegralHeight = false; images.BackColor = DarkTheme.Card; images.ForeColor = DarkTheme.Text; images.BorderStyle = BorderStyle.None; images.DrawMode = DrawMode.OwnerDrawFixed; images.ItemHeight = 72; images.DrawItem += DrawCrosshairItem; layout.Controls.Add(images, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        buttons.BackColor = DarkTheme.Surface;
        var create = Button("New", DarkTheme.Accent, DarkTheme.Window); create.Click += (_, _) => CreateImage(); buttons.Controls.Add(create);
        var edit = Button("Edit", DarkTheme.SurfaceHover, DarkTheme.Text); edit.Click += (_, _) => EditImage(); buttons.Controls.Add(edit);
        var add = Button("Import", DarkTheme.SurfaceHover, DarkTheme.Text); add.Click += (_, _) => AddImage(); buttons.Controls.Add(add);
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
    private Panel MakeProfilesTab()
    {
        var p = Page("Profiles"); var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = DarkTheme.Surface };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); p.Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "Save crosshair presets and switch between them quickly.", ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        profiles.Dock = DockStyle.Fill; profiles.IntegralHeight = false; profiles.BackColor = DarkTheme.Card; profiles.ForeColor = DarkTheme.Text; profiles.BorderStyle = BorderStyle.None; profiles.DrawMode = DrawMode.OwnerDrawFixed; profiles.ItemHeight = 46; profiles.DrawItem += DrawProfileItem; layout.Controls.Add(profiles, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        buttons.BackColor = DarkTheme.Surface;
        var save = Button("Save as…", DarkTheme.Accent, DarkTheme.Window); save.Click += (_, _) => SaveProfile(); buttons.Controls.Add(save);
        var load = Button("Load", DarkTheme.SurfaceHover, DarkTheme.Text); load.Click += (_, _) => LoadProfile(); buttons.Controls.Add(load);
        var delete = Button("Delete", DarkTheme.SurfaceHover, DarkTheme.Text); delete.Click += (_, _) => DeleteProfile(); buttons.Controls.Add(delete); layout.Controls.Add(buttons, 0, 2);
        return p;
    }
    private Panel MakeSettingsTab()
    {
        var p = Page("Settings"); var grid = Grid(); p.Controls.Add(grid);
        visible.Text = "Show crosshair"; visible.AutoSize = true; AddRow(grid, "Visibility", visible);
        hotkey.DropDownStyle = ComboBoxStyle.DropDownList; hotkey.Items.AddRange(["F6", "F7", "F8", "F9", "F10", "F11", "F12", "Insert", "Pause"]); AddRow(grid, "Toggle hotkey", hotkey);
        hideWhileHeld.Text = "Hide while key is held"; hideWhileHeld.AutoSize = true; AddRow(grid, "Hold to hide", hideWhileHeld);
        holdKey.DropDownStyle = ComboBoxStyle.DropDownList; holdKey.Items.AddRange(["Right Mouse", "Left Mouse", "Middle Mouse", "Mouse X1", "Mouse X2", "Space", "Shift", "Ctrl", "Alt", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Insert", "Pause"]); AddRow(grid, "Hold key", holdKey);
        startup.Text = "Launch when Windows starts"; startup.AutoSize = true; AddRow(grid, "Startup", startup);
        closeToTray.Text = "Minimize to tray when closed"; closeToTray.AutoSize = true; AddRow(grid, "Close button", closeToTray);
        var checkUpdates = Button("Check for updates", DarkTheme.SurfaceHover, DarkTheme.Text); checkUpdates.Click += async (_, _) => await CheckUpdates(true); AddRow(grid, "Updates", checkUpdates); AddRow(grid, "Version " + AppUpdater.CurrentVersion.ToString(3), updateStatus);
        var text = new Label { Text = "UIAccess overlay. Fullscreen support depends on the game and its graphics mode.", Dock = DockStyle.Fill, ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface, AutoSize = false };
        grid.Controls.Add(text, 0, grid.RowCount); grid.SetColumnSpan(text, 2); grid.RowCount++;
        visible.CheckedChanged += (_, _) => { if (!initializing) ToggleVisible(visible.Checked); };
        hotkey.SelectedIndexChanged += (_, _) => Apply();
        hideWhileHeld.CheckedChanged += (_, _) => { if (!initializing) Apply(); };
        holdKey.SelectedIndexChanged += (_, _) => { if (!initializing) Apply(); };
        startup.CheckedChanged += (_, _) => SetStartup();
        closeToTray.CheckedChanged += (_, _) => { if (initializing) return; data.MinimizeToTrayOnClose = closeToTray.Checked; Store.Save(data); };
        return p;
    }

    private Task CheckUpdates(bool manual) => updater.CheckAsync(this, manual, text => { if (!IsDisposed) updateStatus.Text = text; }, () => { allowExit = true; Close(); });
    private TableLayoutPanel Grid() => new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 0, Padding = new Padding(0, 2, 4, 2), BackColor = DarkTheme.Surface }.Also(g => { g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); });
    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount++; grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        var l = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface };
        control.BackColor = control is Button ? control.BackColor : DarkTheme.Input; control.ForeColor = control is Button ? control.ForeColor : DarkTheme.Text;
        control.Dock = DockStyle.Fill; grid.Controls.Add(l, 0, row); grid.Controls.Add(control, 1, row);
    }
    private void AddSliderRow(TableLayoutPanel g, string label, DarkSlider bar, Func<AimSettings, int> get, string key)
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 }; host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        var value = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = DarkTheme.Muted, BackColor = DarkTheme.Surface }; host.BackColor = DarkTheme.Surface; bar.BackColor = DarkTheme.Surface; host.Controls.Add(bar, 0, 0); host.Controls.Add(value, 1, 0);
        value.Font = AppFonts.Semibold(9); bar.ValueChanged += (_, _) => { value.Text = key is "opacity" or "scale" ? $"{bar.Value}%" : bar.Value.ToString(); if (!initializing) Apply(); };
        AddRow(g, label, host);
    }
    private static Button Button(string label, Color back, Color fore)
    {
        bool primary = back == DarkTheme.Accent;
        var button = new Button { Text = label, BackColor = primary ? DarkTheme.Accent : DarkTheme.SurfaceHover, ForeColor = primary ? DarkTheme.Window : fore, FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 38, Padding = new Padding(12, 4, 12, 4), Margin = new Padding(0, 4, 8, 4), Cursor = Cursors.Hand, Font = AppFonts.Semibold(9) };
        button.FlatAppearance.BorderSize = primary ? 0 : 1; button.FlatAppearance.BorderColor = DarkTheme.Border; button.FlatAppearance.MouseOverBackColor = primary ? DarkTheme.AccentHover : DarkTheme.Border;
        string? glyph = label.StartsWith("Refresh", StringComparison.OrdinalIgnoreCase) ? "refresh" : label.StartsWith("New", StringComparison.OrdinalIgnoreCase) ? "image" : label.StartsWith("Edit", StringComparison.OrdinalIgnoreCase) ? "edit" : label.StartsWith("Import", StringComparison.OrdinalIgnoreCase) ? "import" : label.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ? "delete" : label.StartsWith("Save", StringComparison.OrdinalIgnoreCase) ? "save" : label.StartsWith("Load", StringComparison.OrdinalIgnoreCase) ? "load" : null;
        if (glyph != null) { button.Image = GlyphPainter.Create(glyph, 17, primary ? DarkTheme.Window : DarkTheme.Text); button.ImageAlign = ContentAlignment.MiddleLeft; button.TextImageRelation = TextImageRelation.ImageBeforeText; }
        return button;
    }

    private void LoadCurrent()
    {
        var s = data.Current;
        shape.SelectedItem = Shapes.Contains(s.Shape) ? s.Shape : "Cross";
        if (s.Shape == "Image") s.TintImage = !data.OriginalColorImages.Contains(Path.GetFileName(s.ImagePath), StringComparer.OrdinalIgnoreCase);
        SetColor(s.Color, false); SetColor(s.OutlineColor, true);
        size.Value = Math.Clamp(s.Size, size.Minimum, size.Maximum); imageScale.Value = Math.Clamp(s.ImageScale, imageScale.Minimum, imageScale.Maximum); imageScale.Enabled = s.Shape == "Image"; thickness.Value = Math.Clamp(s.Thickness, thickness.Minimum, thickness.Maximum); gap.Value = Math.Clamp(s.Gap, gap.Minimum, gap.Maximum); opacity.Value = Math.Clamp(s.Opacity, opacity.Minimum, opacity.Maximum);
        outline.Checked = s.Outline; visible.Checked = s.Visible; offsetX.Value = Math.Clamp(s.OffsetX, (int)offsetX.Minimum, (int)offsetX.Maximum); offsetY.Value = Math.Clamp(s.OffsetY, (int)offsetY.Minimum, (int)offsetY.Maximum);
        desiredVisible = s.Visible;
        hotkey.SelectedItem = hotkey.Items.Contains(s.Hotkey) ? s.Hotkey : "F8";
        hideWhileHeld.Checked = s.HideWhileHeld; holdKey.SelectedItem = holdKey.Items.Contains(NormalizeHoldKey(s.HoldKey)) ? NormalizeHoldKey(s.HoldKey) : "Right Mouse";
        startup.Checked = s.StartWithWindows;
        closeToTray.Checked = data.MinimizeToTrayOnClose;
        PopulateMonitors();
    }
    private void PopulateMonitors()
    {
        var wanted = data.Current.Monitor; monitor.Items.Clear();
        foreach (var scr in Screen.AllScreens) monitor.Items.Add(new MonitorItem(scr));
        int i = Enumerable.Range(0, monitor.Items.Count).FirstOrDefault(k => ((MonitorItem)monitor.Items[k]!).Screen.DeviceName == wanted, -1);
        if (i < 0) i = Array.FindIndex(Screen.AllScreens, x => x.Primary);
        if (i >= 0 && i < monitor.Items.Count) monitor.SelectedIndex = i;
    }
    private void PopulateProfiles()
    {
        if (data.Profiles.Count == 0) data.Profiles.Add(new Profile());
        profiles.Items.Clear(); foreach (var x in data.Profiles) profiles.Items.Add(x.Name);
    }
    private void PopulateImages()
    {
        string folder = Path.Combine(Store.Root, "crosshairs"); Directory.CreateDirectory(folder);
        foreach (string name in BundledCrosshairs)
        {
            string destination = Path.Combine(folder, name);
            if (!File.Exists(destination))
            {
                using Stream? source = Assembly.GetExecutingAssembly().GetManifestResourceStream("SimpleAimOverlay.Assets.Crosshairs." + name);
                if (source != null) using (var target = File.Create(destination)) source.CopyTo(target);
            }
            if (File.Exists(destination) && !data.Images.Contains(name, StringComparer.OrdinalIgnoreCase)) data.Images.Add(name);
        }
        images.Items.Clear(); foreach (var x in data.Images) if (File.Exists(Path.Combine(folder, x))) images.Items.Add(x);
        Store.Save(data);
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
    private void DrawProfileItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= profiles.Items.Count) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var background = new SolidBrush(selected ? DarkTheme.SurfaceHover : DarkTheme.Card)) e.Graphics.FillRectangle(background, e.Bounds);
        using var icon = GlyphPainter.Create("profiles", 18, selected ? DarkTheme.Accent : DarkTheme.Muted);
        e.Graphics.DrawImage(icon, e.Bounds.Left + 14, e.Bounds.Top + (e.Bounds.Height - icon.Height) / 2);
        TextRenderer.DrawText(e.Graphics, profiles.Items[e.Index]?.ToString() ?? string.Empty, profiles.Font, new Rectangle(e.Bounds.Left + 44, e.Bounds.Top, e.Bounds.Width - 54, e.Bounds.Height), DarkTheme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }
    private void Apply()
    {
        if (initializing) return;
        var s = data.Current;
        s.Shape = shape.SelectedItem?.ToString() ?? "Cross"; s.Color = ColorTranslator.ToHtml(aimColor); s.OutlineColor = ColorTranslator.ToHtml(borderColor);
        s.Outline = outline.Checked; s.Size = size.Value; s.ImageScale = imageScale.Value; s.Thickness = thickness.Value; s.Gap = gap.Value; s.Opacity = opacity.Value;
        s.OffsetX = (int)offsetX.Value; s.OffsetY = (int)offsetY.Value; desiredVisible = visible.Checked; s.Visible = desiredVisible;
        s.HideWhileHeld = hideWhileHeld.Checked; s.HoldKey = holdKey.SelectedItem?.ToString() ?? "Right Mouse";
        toggleButton.Text = s.Visible ? "Crosshair: ON" : "Crosshair: OFF";
        toggleButton.BackColor = s.Visible ? DarkTheme.Accent : DarkTheme.SurfaceHover;
        images.Invalidate(); colorButton.BackColor = aimColor; outlineButton.BackColor = borderColor;
        if (monitor.SelectedItem is MonitorItem mi) s.Monitor = mi.Screen.DeviceName;
        s.Hotkey = hotkey.SelectedItem?.ToString() ?? "F8";
        RefreshRuntimeVisibility(); preview.Invalidate(); Store.Save(data); BindHotkey();
    }
    private Screen SelectedScreen() => monitor.SelectedItem is MonitorItem mi ? mi.Screen : Screen.PrimaryScreen ?? Screen.AllScreens[0];
    private void PickColor(bool isOutline)
    {
        using var d = new DarkColorPicker { Color = isOutline ? borderColor : aimColor, FullOpen = true };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        if (isOutline) { borderColor = d.Color; outlineButton.BackColor = d.Color; } else { aimColor = d.Color; colorButton.BackColor = d.Color; images.Invalidate(); preview.Invalidate(); }
        Apply();
    }
    private void SetColor(string value, bool isOutline)
    {
        Color c; try { c = ColorTranslator.FromHtml(value); } catch { c = isOutline ? Color.FromArgb(16, 24, 32) : Color.LimeGreen; }
        if (isOutline) { borderColor = c; outlineButton.BackColor = c; } else { aimColor = c; colorButton.BackColor = c; }
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
        runtimeSettings.Visible = runtimeSettings.Visible && !hiddenByHold;
        overlay.UpdateAim(runtimeSettings, aimColor, borderColor, SelectedScreen());
        status.Text = StatusText(hiddenByHold, runtimeSettings);
    }
    private string StatusText(bool? hidden = null, AimSettings? current = null)
    {
        current ??= data.Current;
        hidden ??= current.HideWhileHeld && holdDown;
        string holdName = HoldKeyLabel(current.HoldKey);
        string aim = hidden.Value ? $"{holdName} held · crosshair hidden" : current.Visible ? $"{current.Hotkey} toggles · {holdName} temporarily hides" : $"Crosshair off · {current.Hotkey} toggles visibility";
        return aim;
    }
    private void BindHotkey()
    {
        if (!IsHandleCreated) return;
        UnregisterHotKey(Handle, HotkeyId);
        string keyName = data.Current.Hotkey;
        if (Enum.TryParse<Keys>(keyName, out var key) && !RegisterHotKey(Handle, HotkeyId, 0, (uint)key)) status.Text = $"Could not register {keyName}: the key is already in use.";
    }
    private void PollHoldKey()
    {
        bool pressed = hideWhileHeld.Checked && desiredVisible && (GetAsyncKeyState(HoldKeyCode(holdKey.SelectedItem?.ToString() ?? "Right Mouse")) & 0x8000) != 0;
        if (pressed == holdDown) return;
        holdDown = pressed;
        RefreshRuntimeVisibility();
    }
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
            data.Current.StartWithWindows = startup.Checked; Store.Save(data);
        }
        catch { DarkMessage.Show(this, "Could not update the startup setting.", "Simple Aim Overlay", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void AddImage()
    {
        using var d = new OpenFileDialog { Title = "Import crosshair image", Filter = "Images|*.png;*.bmp;*.jpg;*.jpeg|All files|*.*" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string folder = Path.Combine(Store.Root, "crosshairs"); Directory.CreateDirectory(folder);
            string name = Path.GetFileNameWithoutExtension(d.FileName); string ext = Path.GetExtension(d.FileName).ToLowerInvariant();
            string dest = Path.Combine(folder, name + ext); int i = 2; while (File.Exists(dest)) dest = Path.Combine(folder, $"{name} ({i++}){ext}");
            File.Copy(d.FileName, dest); data.Images.Add(Path.GetFileName(dest)); data.OriginalColorImages.Remove(Path.GetFileName(dest)); data.Current.ImagePath = dest; data.Current.TintImage = true; PopulateImages(); images.SelectedItem = Path.GetFileName(dest); shape.SelectedItem = "Image"; imageScale.Enabled = true; Apply();
        }
        catch (Exception ex) { DarkMessage.Show(this, "Could not import the file: " + ex.Message, "Gallery", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private void RemoveImage()
    {
        if (images.SelectedItem is not string name) return;
        if (BundledCrosshairs.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            DarkMessage.Show(this, "Bundled crosshairs cannot be deleted. You can remove your own images.", "Gallery", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string p = Path.Combine(Store.Root, "crosshairs", name); try { if (File.Exists(p)) File.Delete(p); } catch { }
        data.Images.Remove(name); data.OriginalColorImages.Remove(name); if (data.Current.ImagePath == p) { data.Current.ImagePath = ""; shape.SelectedItem = "Cross"; } PopulateImages(); Apply();
    }
    private void CreateImage() => OpenPixelEditor(null, "New crosshair");
    private void EditImage()
    {
        if (images.SelectedItem is not string name) { DarkMessage.Show(this, "Select a crosshair first.", "Editor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        OpenPixelEditor(Path.Combine(Store.Root, "crosshairs", name), Path.GetFileNameWithoutExtension(name));
    }
    private void OpenPixelEditor(string? path, string title)
    {
        using var editor = new PixelCrosshairEditor(path, aimColor);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.ResultImage == null) return;
        string folder = Path.Combine(Store.Root, "crosshairs"); Directory.CreateDirectory(folder);
        string safe = string.Concat(title.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch));
        string name = $"{safe}-edit-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        string destination = Path.Combine(folder, name);
        using (editor.ResultImage) editor.ResultImage.Save(destination, ImageFormat.Png);
        if (!data.Images.Contains(name, StringComparer.OrdinalIgnoreCase)) data.Images.Add(name);
        data.Current.ImagePath = destination; data.Current.TintImage = editor.TintForOverlay;
        if (editor.TintForOverlay) data.OriginalColorImages.Remove(name);
        else if (!data.OriginalColorImages.Contains(name, StringComparer.OrdinalIgnoreCase)) data.OriginalColorImages.Add(name);
        PopulateImages(); images.SelectedItem = name; shape.SelectedItem = "Image"; imageScale.Enabled = true; Apply();
    }
    private void SaveProfile()
    {
        string? response = DarkPrompt.Show(this, "Save profile", "Profile name", "My crosshair"); if (string.IsNullOrWhiteSpace(response)) return;
        string name = response.Trim();
        var existing = data.Profiles.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing == null) data.Profiles.Add(new Profile { Name = name, Aim = Copy(data.Current) }); else existing.Aim = Copy(data.Current);
        PopulateProfiles(); profiles.SelectedItem = name; Store.Save(data);
    }
    private void LoadProfile()
    {
        if (profiles.SelectedItem is not string name) return; var profile = data.Profiles.FirstOrDefault(x => x.Name == name); if (profile == null) return;
        data.Current = Copy(profile.Aim); initializing = true; LoadCurrent(); initializing = false; Apply();
    }
    private void DeleteProfile()
    {
        if (profiles.SelectedItem is not string name || data.Profiles.Count <= 1) return;
        data.Profiles.RemoveAll(x => x.Name == name); PopulateProfiles(); Store.Save(data);
    }
    private static AimSettings Copy(AimSettings x) => JsonSerializer.Deserialize<AimSettings>(JsonSerializer.Serialize(x)) ?? new AimSettings();
    private void RestoreWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }

    private sealed class MonitorItem(Screen screen) { public Screen Screen { get; } = screen; public override string ToString() => $"{(Screen.Primary ? "Primary · " : "")}{Screen.DeviceName}  ({Screen.Bounds.Width} × {Screen.Bounds.Height})"; }
}

internal sealed class OverlayWindow : Form
{
    private const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
    private const int ULW_ALPHA = 2, AC_SRC_ALPHA = 1, BI_RGB = 0, DIB_RGB_COLORS = 0;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_NOOWNERZORDER = 0x0200;
    private static readonly IntPtr HwndTopmost = new(-1);
    private AimSettings settings = new(); private Color color = Color.LimeGreen, outline = Color.Black; private Screen screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
    private readonly System.Windows.Forms.Timer topmostTimer = new() { Interval = 750 };
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
        if (s.Visible) { if (!Visible && IsHandleCreated) Show(); if (IsHandleCreated) { MaintainOverlay(); RenderLayered(); } }
        else if (Visible) Hide();
    }
    protected override void OnShown(EventArgs e) { base.OnShown(e); MaintainOverlay(); RenderLayered(); topmostTimer.Start(); }
    private void MaintainOverlay()
    {
        if (!IsHandleCreated || !Visible) return;
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
        g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float scale = preview ? Math.Min(bounds.Width, bounds.Height) / 230f : 1f;
        float cx = bounds.Width / 2f + s.OffsetX * scale, cy = bounds.Height / 2f + s.OffsetY * scale;
        float arm = s.Size * scale / 2f, gap = s.Gap * scale, width = s.Thickness * scale;
        int alpha = (int)Math.Clamp(color.A * s.Opacity / 100f, 0, 255);
        using var main = new Pen(Color.FromArgb(alpha, color), width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var edge = new Pen(Color.FromArgb(alpha, outline), width + Math.Max(2, width * .8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var fill = new SolidBrush(Color.FromArgb(alpha, color));
        void Line(float a, float b, float c, float d) { if (s.Outline) g.DrawLine(edge, a, b, c, d); g.DrawLine(main, a, b, c, d); }
        if (s.Shape == "Image" && File.Exists(s.ImagePath))
        {
            try
            {
                using var im = Image.FromFile(s.ImagePath);
                var source = ImageContentBounds(im);
                float d = s.Size * (s.ImageScale / 100f) * scale;
                float ratio = Math.Min(d / source.Width, d / source.Height);
                int contentWidth = Math.Max(1, (int)(source.Width * ratio)), contentHeight = Math.Max(1, (int)(source.Height * ratio));
                int padding = Math.Max(2, (int)Math.Ceiling(s.Thickness * scale));
                int imageWidth = contentWidth + padding * 2, imageHeight = contentHeight + padding * 2;
                var target = new Rectangle((int)(cx - imageWidth / 2f), (int)(cy - imageHeight / 2f), imageWidth, imageHeight);
                int effectiveThickness = 3 + (int)Math.Round((s.Thickness - 3) * scale / 2);
                int gapPixels = (int)Math.Round(s.Gap * scale * 2);
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
                    AdjustImageAppearance(rendered, effectiveThickness, gapPixels);
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
                        int outlineWidth = Math.Clamp((int)Math.Round(s.Thickness * .55f * scale), 1, 5);
                        using var clip = new Region(new Rectangle(target.X - outlineWidth, target.Y - outlineWidth, target.Width + outlineWidth * 2, target.Height + outlineWidth * 2));
                        if (gapPixels > 0) clip.Exclude(new RectangleF(cx - gapPixels / 2f, cy - gapPixels / 2f, gapPixels, gapPixels));
                        g.SetClip(clip, CombineMode.Intersect);
                        for (int dy = -outlineWidth; dy <= outlineWidth; dy++)
                        for (int dx = -outlineWidth; dx <= outlineWidth; dx++)
                        {
                            if (dx * dx + dy * dy > outlineWidth * outlineWidth || (dx == 0 && dy == 0)) continue;
                            var edgeRect = new Rectangle(target.X + dx, target.Y + dy, target.Width, target.Height);
                            DrawCrosshairImage(g, rendered, new Rectangle(0, 0, imageWidth, imageHeight), edgeRect, outline, imageAlpha, true);
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
        if (s.Shape == "Dot") { float d = Math.Max(2, width * 1.5f); g.FillEllipse(fill, cx - d / 2, cy - d / 2, d, d); return; }
        if (s.Shape is "Circle" or "Square") { float d = arm * 2; var r = new RectangleF(cx - d / 2, cy - d / 2, d, d); if (s.Shape == "Circle") { if (s.Outline) g.DrawEllipse(edge, r); g.DrawEllipse(main, r); } else { if (s.Outline) g.DrawRectangle(edge, r.X, r.Y, r.Width, r.Height); g.DrawRectangle(main, r.X, r.Y, r.Width, r.Height); } return; }
        if (s.Shape is "Cross" or "Cross + Dot" or "T-shape" or "Predator") { Line(cx - arm, cy, cx - gap, cy); Line(cx + gap, cy, cx + arm, cy); if (s.Shape != "Predator") Line(cx, cy - arm, cx, cy - gap); if (s.Shape != "T-shape") Line(cx, cy + gap, cx, cy + arm); }
        else if (s.Shape == "Diagonal") { Line(cx - arm, cy - arm, cx - gap, cy - gap); Line(cx + gap, cy + gap, cx + arm, cy + arm); Line(cx + arm, cy - arm, cx + gap, cy - gap); Line(cx - gap, cy + gap, cx - arm, cy + arm); }
        else if (s.Shape == "Chevron") { Line(cx - arm, cy + arm * .35f, cx, cy - arm * .45f); Line(cx, cy - arm * .45f, cx + arm, cy + arm * .35f); }
        else if (s.Shape == "3-ray") { for (int i = 0; i < 3; i++) { double a = (i * 120 - 90) * Math.PI / 180; Line(cx + (float)Math.Cos(a) * gap, cy + (float)Math.Sin(a) * gap, cx + (float)Math.Cos(a) * arm, cy + (float)Math.Sin(a) * arm); } }
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
