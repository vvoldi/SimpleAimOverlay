using System.Drawing.Drawing2D;
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
    public int Thickness { get; set; } = 3;
    public int Gap { get; set; } = 7;
    public int Opacity { get; set; } = 100;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public string Monitor { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public bool Visible { get; set; } = true;
    public string Hotkey { get; set; } = "F8";
    public bool HideWhileHeld { get; set; } = true;
    public string HoldKey { get; set; } = "ПКМ";
    public bool StartWithWindows { get; set; }
}

internal sealed class StoreData
{
    public AimSettings Current { get; set; } = new();
    public List<Profile> Profiles { get; set; } = [new Profile()];
    public List<string> Images { get; set; } = [];
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
    private readonly TabControl tabs = new();
    private readonly ComboBox shape = new(), monitor = new(), hotkey = new();
    private readonly ComboBox holdKey = new();
    private readonly ListBox images = new(), profiles = new();
    private readonly CheckBox outline = new(), visible = new(), startup = new();
    private readonly CheckBox hideWhileHeld = new();
    private readonly TrackBar size = Slider(8, 180), thickness = Slider(1, 24), gap = Slider(0, 100), opacity = Slider(10, 100);
    private readonly NumericUpDown offsetX = new(), offsetY = new();
    private readonly Button colorButton = new(), outlineButton = new();
    private readonly Button toggleButton = new();
    private readonly Panel preview = new();
    private readonly Label status = new();
    private readonly NotifyIcon tray = new();
    private readonly System.Windows.Forms.Timer holdTimer = new() { Interval = 12 };
    private bool initializing = true;
    private bool holdDown, desiredVisible = true;
    private Color aimColor = Color.LimeGreen, borderColor = Color.FromArgb(16, 24, 32);
    private static readonly string[] Shapes = ["Cross", "Dot", "Circle", "Cross + Dot", "T-shape", "Diagonal", "Chevron", "3-ray", "Square", "Predator", "Image"];

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

    public ControlWindow()
    {
        Text = "Simple Aim Overlay"; MinimumSize = new Size(840, 620); Size = new Size(960, 690);
        StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9.5f); BackColor = Color.FromArgb(244, 246, 250);
        BuildLayout(); LoadCurrent(); initializing = false; Apply(); PopulateProfiles(); PopulateImages();
        tray.Icon = SystemIcons.Application; tray.Text = "Simple Aim Overlay"; tray.Visible = true;
        var menu = new ContextMenuStrip(); menu.Items.Add("Відкрити", null, (_, _) => RestoreWindow()); menu.Items.Add("Показати / сховати", null, (_, _) => ToggleVisible()); menu.Items.Add("Вихід", null, (_, _) => Close()); tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => RestoreWindow();
        holdTimer.Tick += (_, _) => PollHoldKey();
        Shown += (_, _) => { overlay.Show(); BindHotkey(); holdTimer.Start(); };
        FormClosing += (_, _) => { holdTimer.Stop(); holdTimer.Dispose(); UnregisterHotKey(Handle, HotkeyId); tray.Visible = false; tray.Dispose(); overlay.Close(); };
    }

    private static TrackBar Slider(int min, int max) => new() { Minimum = min, Maximum = max, TickStyle = TickStyle.None, AutoSize = false, Height = 34, Dock = DockStyle.Fill };
    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(20) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 65)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(root);
        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "AIM OVERLAY", Font = new Font("Segoe UI Semibold", 18), ForeColor = Color.FromArgb(27, 38, 59), AutoSize = true, Location = new Point(0, 0) });
        heading.Controls.Add(new Label { Text = "Налаштуй свій приціл", ForeColor = Color.Gray, AutoSize = true, Location = new Point(2, 34) });
        root.Controls.Add(heading, 0, 0); root.SetColumnSpan(heading, 2);
        tabs.Dock = DockStyle.Fill; tabs.Font = new Font("Segoe UI", 10); tabs.Padding = new Point(16, 7);
        tabs.TabPages.Add(MakeCrosshairTab()); tabs.TabPages.Add(MakeLibraryTab()); tabs.TabPages.Add(MakeProfilesTab()); tabs.TabPages.Add(MakeSettingsTab());
        root.Controls.Add(tabs, 0, 1);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18, 10, 0, 0) };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); right.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        right.Controls.Add(new Label { Text = "ПОПЕРЕДНІЙ ПЕРЕГЛЯД", Font = new Font("Segoe UI Semibold", 9), ForeColor = Color.Gray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        preview.Dock = DockStyle.Fill; preview.BackColor = Color.FromArgb(27, 35, 49); preview.Paint += (_, e) => AimRenderer.Draw(e.Graphics, preview.ClientRectangle, data.Current, aimColor, borderColor, true); right.Controls.Add(preview, 0, 1);
        var bottom = new Panel { Dock = DockStyle.Fill };
        toggleButton.Text = "Приціл: УВІМК"; toggleButton.BackColor = Color.FromArgb(29, 127, 87); toggleButton.ForeColor = Color.White; toggleButton.FlatStyle = FlatStyle.Flat; toggleButton.Dock = DockStyle.Top; toggleButton.Height = 42; toggleButton.Click += (_, _) => ToggleVisible(); bottom.Controls.Add(toggleButton);
        status.Text = "F8 перемикає видимість"; status.Dock = DockStyle.Bottom; status.Height = 40; status.ForeColor = Color.DimGray; status.TextAlign = ContentAlignment.MiddleCenter; bottom.Controls.Add(status); right.Controls.Add(bottom, 0, 2); root.Controls.Add(right, 1, 1);
    }

    private TabPage Page(string name) { var p = new TabPage(name) { BackColor = Color.White, Padding = new Padding(18), AutoScroll = true }; return p; }
    private TabPage MakeCrosshairTab()
    {
        var p = Page("Приціл"); var grid = Grid(); p.Controls.Add(grid);
        shape.DropDownStyle = ComboBoxStyle.DropDownList; shape.Items.AddRange(Shapes); AddRow(grid, "Стиль", shape);
        colorButton.Text = "Колір прицілу"; colorButton.TextAlign = ContentAlignment.MiddleLeft; colorButton.Click += (_, _) => PickColor(false); AddRow(grid, "Колір", colorButton);
        AddSliderRow(grid, "Непрозорість", opacity, s => s.Opacity, "opacity");
        AddSliderRow(grid, "Розмір", size, s => s.Size, "size");
        AddSliderRow(grid, "Товщина", thickness, s => s.Thickness, "thickness");
        AddSliderRow(grid, "Проміжок", gap, s => s.Gap, "gap");
        outline.Text = "Показувати контур"; outline.AutoSize = true; AddRow(grid, "Обведення", outline);
        outlineButton.Text = "Колір контуру"; outlineButton.ForeColor = Color.White; outlineButton.Click += (_, _) => PickColor(true); AddRow(grid, "Контур", outlineButton);
        ConfigureNumeric(offsetX, -2000, 2000); ConfigureNumeric(offsetY, -2000, 2000);
        var offsets = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        offsets.Controls.Add(new Label { Text = "X", AutoSize = true, Padding = new Padding(0, 7, 5, 0) }); offsets.Controls.Add(offsetX); offsets.Controls.Add(new Label { Text = "Y", AutoSize = true, Padding = new Padding(10, 7, 5, 0) }); offsets.Controls.Add(offsetY);
        AddRow(grid, "Зсув від центру", offsets);
        monitor.DropDownStyle = ComboBoxStyle.DropDownList; AddRow(grid, "Дисплей", monitor);
        var refresh = Button("Оновити список дисплеїв", Color.FromArgb(235, 239, 245), Color.FromArgb(40, 50, 65)); refresh.Dock = DockStyle.Fill; refresh.Click += (_, _) => PopulateMonitors(); AddRow(grid, "", refresh);
        shape.SelectedIndexChanged += (_, _) => Apply(); outline.CheckedChanged += (_, _) => Apply(); offsetX.ValueChanged += (_, _) => Apply(); offsetY.ValueChanged += (_, _) => Apply(); monitor.SelectedIndexChanged += (_, _) => Apply();
        return p;
    }
    private TabPage MakeLibraryTab()
    {
        var p = Page("Бібліотека"); var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); p.Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "Додавай власні зображення прицілу у PNG, BMP або JPG.", ForeColor = Color.DimGray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        images.Dock = DockStyle.Fill; images.IntegralHeight = false; layout.Controls.Add(images, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var add = Button("Додати зображення", Color.FromArgb(27, 101, 225), Color.White); add.Click += (_, _) => AddImage(); buttons.Controls.Add(add);
        var remove = Button("Видалити", Color.FromArgb(235, 239, 245), Color.FromArgb(40, 50, 65)); remove.Click += (_, _) => RemoveImage(); buttons.Controls.Add(remove);
        layout.Controls.Add(buttons, 0, 2);
        images.SelectedIndexChanged += (_, _) => { if (!initializing && images.SelectedItem is string filename) { data.Current.ImagePath = Path.Combine(Store.Root, "crosshairs", filename); shape.SelectedItem = "Image"; Apply(); } };
        return p;
    }
    private TabPage MakeProfilesTab()
    {
        var p = Page("Профілі"); var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); p.Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "Зберігай набори прицілів і швидко перемикайся між ними.", ForeColor = Color.DimGray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        profiles.Dock = DockStyle.Fill; profiles.IntegralHeight = false; layout.Controls.Add(profiles, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var save = Button("Зберегти як…", Color.FromArgb(27, 101, 225), Color.White); save.Click += (_, _) => SaveProfile(); buttons.Controls.Add(save);
        var load = Button("Завантажити", Color.FromArgb(235, 239, 245), Color.FromArgb(40, 50, 65)); load.Click += (_, _) => LoadProfile(); buttons.Controls.Add(load);
        var delete = Button("Видалити", Color.FromArgb(235, 239, 245), Color.FromArgb(40, 50, 65)); delete.Click += (_, _) => DeleteProfile(); buttons.Controls.Add(delete); layout.Controls.Add(buttons, 0, 2);
        return p;
    }
    private TabPage MakeSettingsTab()
    {
        var p = Page("Параметри"); var grid = Grid(); p.Controls.Add(grid);
        visible.Text = "Показувати приціл"; visible.AutoSize = true; AddRow(grid, "Видимість", visible);
        hotkey.DropDownStyle = ComboBoxStyle.DropDownList; hotkey.Items.AddRange(["F6", "F7", "F8", "F9", "F10", "F11", "F12", "Insert", "Pause"]); AddRow(grid, "Клавіша перемикання", hotkey);
        hideWhileHeld.Text = "Ховати при затисканні"; hideWhileHeld.AutoSize = true; AddRow(grid, "Тимчасово ховати", hideWhileHeld);
        holdKey.DropDownStyle = ComboBoxStyle.DropDownList; holdKey.Items.AddRange(["ПКМ", "ЛКМ", "Середня кнопка", "Бічна кнопка X1", "Бічна кнопка X2", "Space", "Shift", "Ctrl", "Alt", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Insert", "Pause"]); AddRow(grid, "Клавіша утримання", holdKey);
        startup.Text = "Запускати разом із Windows"; startup.AutoSize = true; AddRow(grid, "Автозапуск", startup);
        var text = new Label { Text = "UIAccess overlay. Для використання у fullscreen гра має дозволяти зовнішні оверлеї. Сумісність залежить від гри та графічного режиму.", Dock = DockStyle.Fill, ForeColor = Color.DimGray, AutoSize = false };
        grid.Controls.Add(text, 0, grid.RowCount); grid.SetColumnSpan(text, 2); grid.RowCount++;
        visible.CheckedChanged += (_, _) => { if (!initializing) ToggleVisible(visible.Checked); };
        hotkey.SelectedIndexChanged += (_, _) => Apply();
        hideWhileHeld.CheckedChanged += (_, _) => { if (!initializing) Apply(); };
        holdKey.SelectedIndexChanged += (_, _) => { if (!initializing) Apply(); };
        startup.CheckedChanged += (_, _) => SetStartup();
        return p;
    }

    private TableLayoutPanel Grid() => new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 0, Padding = new Padding(0, 2, 4, 2) }.Also(g => { g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); });
    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount++; grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        var l = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(65, 75, 90) };
        control.Dock = DockStyle.Fill; grid.Controls.Add(l, 0, row); grid.Controls.Add(control, 1, row);
    }
    private void AddSliderRow(TableLayoutPanel g, string label, TrackBar bar, Func<AimSettings, int> get, string key)
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 }; host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        var value = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.Gray }; host.Controls.Add(bar, 0, 0); host.Controls.Add(value, 1, 0);
        bar.ValueChanged += (_, _) => { value.Text = key == "opacity" ? $"{bar.Value}%" : bar.Value.ToString(); if (!initializing) Apply(); };
        AddRow(g, label, host);
    }
    private static void ConfigureNumeric(NumericUpDown n, int min, int max) { n.Minimum = min; n.Maximum = max; n.Width = 85; n.Height = 28; }
    private static Button Button(string label, Color back, Color fore) => new() { Text = label, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 36, Padding = new Padding(10, 3, 10, 3), Margin = new Padding(0, 4, 8, 4) };

    private void LoadCurrent()
    {
        var s = data.Current;
        shape.SelectedItem = Shapes.Contains(s.Shape) ? s.Shape : "Cross";
        SetColor(s.Color, false); SetColor(s.OutlineColor, true);
        size.Value = Math.Clamp(s.Size, size.Minimum, size.Maximum); thickness.Value = Math.Clamp(s.Thickness, thickness.Minimum, thickness.Maximum); gap.Value = Math.Clamp(s.Gap, gap.Minimum, gap.Maximum); opacity.Value = Math.Clamp(s.Opacity, opacity.Minimum, opacity.Maximum);
        outline.Checked = s.Outline; visible.Checked = s.Visible; offsetX.Value = Math.Clamp(s.OffsetX, (int)offsetX.Minimum, (int)offsetX.Maximum); offsetY.Value = Math.Clamp(s.OffsetY, (int)offsetY.Minimum, (int)offsetY.Maximum);
        desiredVisible = s.Visible;
        hotkey.SelectedItem = hotkey.Items.Contains(s.Hotkey) ? s.Hotkey : "F8";
        hideWhileHeld.Checked = s.HideWhileHeld; holdKey.SelectedItem = holdKey.Items.Contains(s.HoldKey) ? s.HoldKey : "ПКМ";
        startup.Checked = s.StartWithWindows;
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
        images.Items.Clear(); foreach (var x in data.Images) images.Items.Add(x);
    }
    private void Apply()
    {
        if (initializing) return;
        var s = data.Current;
        s.Shape = shape.SelectedItem?.ToString() ?? "Cross"; s.Color = ColorTranslator.ToHtml(aimColor); s.OutlineColor = ColorTranslator.ToHtml(borderColor);
        s.Outline = outline.Checked; s.Size = size.Value; s.Thickness = thickness.Value; s.Gap = gap.Value; s.Opacity = opacity.Value;
        s.OffsetX = (int)offsetX.Value; s.OffsetY = (int)offsetY.Value; desiredVisible = visible.Checked; s.Visible = desiredVisible;
        s.HideWhileHeld = hideWhileHeld.Checked; s.HoldKey = holdKey.SelectedItem?.ToString() ?? "ПКМ";
        toggleButton.Text = s.Visible ? "Приціл: УВІМК" : "Приціл: ВИМК";
        toggleButton.BackColor = s.Visible ? Color.FromArgb(29, 127, 87) : Color.FromArgb(100, 105, 115);
        if (monitor.SelectedItem is MonitorItem mi) s.Monitor = mi.Screen.DeviceName;
        s.Hotkey = hotkey.SelectedItem?.ToString() ?? "F8";
        RefreshRuntimeVisibility(); preview.Invalidate(); Store.Save(data); BindHotkey();
    }
    private Screen SelectedScreen() => monitor.SelectedItem is MonitorItem mi ? mi.Screen : Screen.PrimaryScreen ?? Screen.AllScreens[0];
    private void PickColor(bool isOutline)
    {
        using var d = new ColorDialog { Color = isOutline ? borderColor : aimColor, FullOpen = true };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        if (isOutline) { borderColor = d.Color; outlineButton.BackColor = d.Color; } else { aimColor = d.Color; colorButton.BackColor = d.Color; }
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
        toggleButton.Text = value ? "Приціл: УВІМК" : "Приціл: ВИМК";
        toggleButton.BackColor = value ? Color.FromArgb(29, 127, 87) : Color.FromArgb(100, 105, 115);
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
        string aim = hidden.Value ? $"{current.HoldKey} затиснуто — приціл приховано" : current.Visible ? $"{current.Hotkey} перемикає · {current.HoldKey} тимчасово ховає" : $"Приціл вимкнено · {current.Hotkey} перемикає видимість";
        return aim;
    }
    private void BindHotkey()
    {
        if (!IsHandleCreated) return;
        UnregisterHotKey(Handle, HotkeyId);
        string keyName = data.Current.Hotkey;
        if (Enum.TryParse<Keys>(keyName, out var key) && !RegisterHotKey(Handle, HotkeyId, 0, (uint)key)) status.Text = $"Не вдалося зареєструвати {keyName}: клавіша вже зайнята";
    }
    private void PollHoldKey()
    {
        bool pressed = hideWhileHeld.Checked && desiredVisible && (GetAsyncKeyState(HoldKeyCode(holdKey.SelectedItem?.ToString() ?? "ПКМ")) & 0x8000) != 0;
        if (pressed == holdDown) return;
        holdDown = pressed;
        RefreshRuntimeVisibility();
    }
    private static int HoldKeyCode(string key) => key switch
    {
        "ЛКМ" => 0x01, "ПКМ" => 0x02, "Середня кнопка" => 0x04, "Бічна кнопка X1" => 0x05, "Бічна кнопка X2" => 0x06,
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
        catch { MessageBox.Show(this, "Не вдалося змінити параметр автозапуску.", "Simple Aim Overlay", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void AddImage()
    {
        using var d = new OpenFileDialog { Title = "Додати зображення прицілу", Filter = "Зображення|*.png;*.bmp;*.jpg;*.jpeg|Усі файли|*.*" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string folder = Path.Combine(Store.Root, "crosshairs"); Directory.CreateDirectory(folder);
            string name = Path.GetFileNameWithoutExtension(d.FileName); string ext = Path.GetExtension(d.FileName).ToLowerInvariant();
            string dest = Path.Combine(folder, name + ext); int i = 2; while (File.Exists(dest)) dest = Path.Combine(folder, $"{name} ({i++}){ext}");
            File.Copy(d.FileName, dest); data.Images.Add(Path.GetFileName(dest)); data.Current.ImagePath = dest; PopulateImages(); images.SelectedItem = Path.GetFileName(dest); shape.SelectedItem = "Image"; Apply();
        }
        catch (Exception ex) { MessageBox.Show(this, "Не вдалося додати файл: " + ex.Message, "Бібліотека", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private void RemoveImage()
    {
        if (images.SelectedItem is not string name) return;
        string p = Path.Combine(Store.Root, "crosshairs", name); try { if (File.Exists(p)) File.Delete(p); } catch { }
        data.Images.Remove(name); if (data.Current.ImagePath == p) { data.Current.ImagePath = ""; shape.SelectedItem = "Cross"; } PopulateImages(); Apply();
    }
    private void SaveProfile()
    {
        string name = Microsoft.VisualBasic.Interaction.InputBox("Назва профілю", "Зберегти профіль", "Мій приціл").Trim(); if (name.Length == 0) return;
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

    private sealed class MonitorItem(Screen screen) { public Screen Screen { get; } = screen; public override string ToString() => $"{(Screen.Primary ? "Основний · " : "")}{Screen.DeviceName}  ({Screen.Bounds.Width} × {Screen.Bounds.Height})"; }
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
        if (s.Shape == "Image" && File.Exists(s.ImagePath)) { try { using var im = Image.FromFile(s.ImagePath); float d = s.Size * scale; g.DrawImage(im, cx - d / 2, cy - d / 2, d, d); return; } catch { } }
        if (s.Shape == "Dot") { float d = Math.Max(2, width * 1.5f); g.FillEllipse(fill, cx - d / 2, cy - d / 2, d, d); return; }
        if (s.Shape is "Circle" or "Square") { float d = arm * 2; var r = new RectangleF(cx - d / 2, cy - d / 2, d, d); if (s.Shape == "Circle") { if (s.Outline) g.DrawEllipse(edge, r); g.DrawEllipse(main, r); } else { if (s.Outline) g.DrawRectangle(edge, r.X, r.Y, r.Width, r.Height); g.DrawRectangle(main, r.X, r.Y, r.Width, r.Height); } return; }
        if (s.Shape is "Cross" or "Cross + Dot" or "T-shape" or "Predator") { Line(cx - arm, cy, cx - gap, cy); Line(cx + gap, cy, cx + arm, cy); if (s.Shape != "Predator") Line(cx, cy - arm, cx, cy - gap); if (s.Shape != "T-shape") Line(cx, cy + gap, cx, cy + arm); }
        else if (s.Shape == "Diagonal") { Line(cx - arm, cy - arm, cx - gap, cy - gap); Line(cx + gap, cy + gap, cx + arm, cy + arm); Line(cx + arm, cy - arm, cx + gap, cy - gap); Line(cx - gap, cy + gap, cx - arm, cy + arm); }
        else if (s.Shape == "Chevron") { Line(cx - arm, cy + arm * .35f, cx, cy - arm * .45f); Line(cx, cy - arm * .45f, cx + arm, cy + arm * .35f); }
        else if (s.Shape == "3-ray") { for (int i = 0; i < 3; i++) { double a = (i * 120 - 90) * Math.PI / 180; Line(cx + (float)Math.Cos(a) * gap, cy + (float)Math.Sin(a) * gap, cx + (float)Math.Cos(a) * arm, cy + (float)Math.Sin(a) * arm); } }
        if (s.Shape is "Cross + Dot" or "Predator") { float d = Math.Max(2, width * 1.4f); g.FillEllipse(fill, cx - d / 2, cy - d / 2, d, d); }
    }
}

internal static class ControlExtensions
{
    public static T Also<T>(this T value, Action<T> action) { action(value); return value; }
}
