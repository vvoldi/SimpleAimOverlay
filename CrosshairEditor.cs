using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Windows.Forms;

namespace SimpleAimOverlay;

internal sealed class PixelCrosshairEditor : Form
{
    private readonly DrawingCanvas canvas;
    private readonly Stack<Bitmap> undo = new(), redo = new();
    private readonly DarkSlider zoom = new(3, 20) { Value = 10, Width = 150, Height = 34 };
    private readonly DarkNumberBox brushSize = new(1, 16, 1) { Width = 72, Height = 30 };
    private readonly DarkNumberBox alpha = new(0, 255, 255) { Width = 78, Height = 30 };
    private readonly Label zoomValue = new() { AutoSize = true, Text = "10×", ForeColor = DarkTheme.Muted, Padding = new Padding(2, 8, 8, 0) };
    private readonly Label pixelValue = new() { AutoSize = true, Text = "RGBA —", ForeColor = DarkTheme.Muted, Padding = new Padding(8, 8, 4, 0) };
    private readonly Button penColor = new() { Text = "Pen color", AutoSize = true, Height = 36, FlatStyle = FlatStyle.Flat, Padding = new Padding(10, 3, 10, 3), Cursor = Cursors.Hand };
    private readonly DarkCheckBox eraser = new() { Text = "Eraser", AutoSize = false, Width = 100, Height = 36, ForeColor = DarkTheme.Text, BackColor = DarkTheme.Surface, Padding = new Padding(8, 9, 8, 0) };
    private readonly DarkCheckBox tintToAim = new() { Text = "Use crosshair color", AutoSize = false, Width = 180, Height = 36, Checked = true, ForeColor = DarkTheme.Text, BackColor = DarkTheme.Surface, Padding = new Padding(6, 9, 6, 0) };
    private readonly bool resizableCanvas;
    private readonly Panel livePreview = new() { BackColor = DarkTheme.Preview, Dock = DockStyle.Fill };
    private readonly DarkComboBox dimensions = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 96 };
    private Color initialColor;
    private bool syncingDimensions;

    public Bitmap? ResultImage { get; private set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool TintForOverlay { get => tintToAim.Checked; set => tintToAim.Checked = value; }

    public PixelCrosshairEditor(string? imagePath, Color color)
    {
        Text = imagePath == null ? "New crosshair" : "Crosshair editor"; Icon = BrandIcon.Load();
        StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(880, 640); Size = new Size(1180, 820);
        FormBorderStyle = FormBorderStyle.Sizable; BackColor = DarkTheme.Window; ForeColor = DarkTheme.Text; Font = AppFonts.Regular(9.5f);
        initialColor = color; resizableCanvas = true; KeyPreview = true;
        if (imagePath == null) canvas = new DrawingCanvas(new Bitmap(64, 64, PixelFormat.Format32bppArgb));
        else { using var source = Image.FromFile(imagePath); canvas = new DrawingCanvas(new Bitmap(source)); }
        canvas.PenColor = color; alpha.Value = color.A;
        canvas.StrokeStarted += PushUndo;
        canvas.Changed += () => livePreview.Invalidate();
        canvas.ColorPicked += c => { initialColor = c; alpha.Value = c.A; canvas.PenColor = c; penColor.BackColor = c; };
        KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.SuppressKeyPress = true; } if (e.Control && e.KeyCode == Keys.Y) { Redo(); e.SuppressKeyPress = true; } };
        canvas.PointerColorChanged += c => pixelValue.Text = $"RGBA {c.R}, {c.G}, {c.B}, {c.A}";
        BuildEditorLayout();
        ApplyDarkCaption();
        FormClosed += (_, _) => { foreach (var b in undo) b.Dispose(); foreach (var b in redo) b.Dispose(); canvas.Image.Dispose(); };
    }

    private void ApplyDarkCaption()
    {
        if (!IsHandleCreated) CreateControl();
        int dark = 1, caption = 0x00191510, border = 0x00191510, text = 0x00F0EAE8;
        try
        {
            _ = DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            _ = DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
            _ = DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
            _ = DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
            _ = DwmSetWindowAttribute(Handle, 36, ref text, sizeof(int));
        }
        catch { }
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void BuildEditorLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12), BackColor = DarkTheme.Window };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 144)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(root);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = DarkTheme.Surface, WrapContents = true, Padding = new Padding(4), AutoScroll = true };
        var undoButton = MakeButton("Undo", false, "undo"); undoButton.Click += (_, _) => Undo(); toolbar.Controls.Add(undoButton);
        var redoButton = MakeButton("Redo", false, "redo"); redoButton.Click += (_, _) => Redo(); toolbar.Controls.Add(redoButton);
        var clear = MakeButton("Clear", false, "clear"); clear.Click += (_, _) => ClearCanvas(); toolbar.Controls.Add(clear);
        var zoomOut = MakeButton("−", false); zoomOut.Click += (_, _) => zoom.Value = Math.Max(zoom.Minimum, zoom.Value - 1); toolbar.Controls.Add(zoomOut);
        toolbar.Controls.Add(zoom); toolbar.Controls.Add(zoomValue);
        var zoomIn = MakeButton("+", false); zoomIn.Click += (_, _) => zoom.Value = Math.Min(zoom.Maximum, zoom.Value + 1); toolbar.Controls.Add(zoomIn);
        zoom.ValueChanged += (_, _) => { zoomValue.Text = $"{zoom.Value}×"; canvas.PixelSize = zoom.Value; };
        penColor.BackColor = initialColor; penColor.ForeColor = DarkTheme.Text; penColor.Image = GlyphPainter.Create("color", 16); penColor.ImageAlign = ContentAlignment.MiddleLeft; penColor.TextImageRelation = TextImageRelation.ImageBeforeText; penColor.FlatAppearance.BorderColor = DarkTheme.Border; penColor.Click += (_, _) => PickPenColor(); toolbar.Controls.Add(penColor);
        toolbar.Controls.Add(new Label { Text = "Brush", AutoSize = true, ForeColor = DarkTheme.Muted, Padding = new Padding(6, 9, 0, 0), BackColor = DarkTheme.Surface });
        brushSize.BackColor = DarkTheme.Input; brushSize.ForeColor = DarkTheme.Text; brushSize.ValueChanged += (_, _) => canvas.BrushSize = (int)brushSize.Value; toolbar.Controls.Add(brushSize);
        dimensions.BackColor = DarkTheme.Input; dimensions.ForeColor = DarkTheme.Text;
        alpha.ValueChanged += (_, _) => canvas.PenColor = Color.FromArgb(alpha.Value, initialColor.R, initialColor.G, initialColor.B); toolbar.Controls.Add(new Label { Text = "Alpha", AutoSize = true, ForeColor = DarkTheme.Muted, Padding = new Padding(5, 9, 0, 0), BackColor = DarkTheme.Surface }); toolbar.Controls.Add(alpha);
        eraser.CheckedChanged += (_, _) => canvas.Eraser = eraser.Checked; toolbar.Controls.Add(eraser);
        toolbar.Controls.Add(tintToAim);
        if (resizableCanvas)
        {
            dimensions.Items.AddRange(["16 × 16", "32 × 32", "64 × 64", "128 × 128", "256 × 256"]); dimensions.SelectedIndex = Array.IndexOf(new[] {16,32,64,128,256}, canvas.Image.Width);
            dimensions.SelectedIndexChanged += (_, _) => { if (!syncingDimensions && dimensions.SelectedItem is string v) { int n = int.Parse(v.Split(' ')[0]); ResizeCanvas(n); } };
            toolbar.Controls.Add(new Label { Text = "Canvas", AutoSize = true, ForeColor = DarkTheme.Muted, Padding = new Padding(6, 9, 0, 0), BackColor = DarkTheme.Surface }); toolbar.Controls.Add(dimensions);
        }
        var tools = new DarkComboBox { Width = 130 }; tools.Items.AddRange(["Pencil", "Line", "Rectangle", "Ellipse", "Fill", "Eyedropper"]); tools.SelectedIndex = 0;
        tools.SelectedIndexChanged += (_, _) => canvas.Tool = tools.SelectedItem?.ToString() ?? "Pencil"; toolbar.Controls.Add(tools);
        var mirrorX = new DarkCheckBox { Text = "Mirror X", Width = 105 }; mirrorX.CheckedChanged += (_, _) => canvas.MirrorX = mirrorX.Checked; toolbar.Controls.Add(mirrorX);
        var mirrorY = new DarkCheckBox { Text = "Mirror Y", Width = 105 }; mirrorY.CheckedChanged += (_, _) => canvas.MirrorY = mirrorY.Checked; toolbar.Controls.Add(mirrorY);
        toolbar.Controls.Add(pixelValue);
        var save = MakeButton("Save drawing", true, "save"); save.Click += (_, _) => { ResultImage = new Bitmap(canvas.Image); DialogResult = DialogResult.OK; Close(); }; toolbar.Controls.Add(save);
        var cancel = MakeButton("Cancel", false); cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); }; toolbar.Controls.Add(cancel);
        root.Controls.Add(toolbar, 0, 0);
        var scroll = new EditorViewport(canvas) { Dock = DockStyle.Fill };
                canvas.PixelSize = zoom.Value;
        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 }; workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        workspace.Controls.Add(scroll, 0, 0);
        var sidebar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = DarkTheme.Window };
        sidebar.Controls.Add(livePreview); sidebar.Controls.Add(new Label { Text = "PREVIEW · 1× / 4×\nRight mouse: erase\nCtrl+Z / Ctrl+Y: undo / redo", Dock = DockStyle.Top, Height = 76, ForeColor = DarkTheme.Muted });
        livePreview.Paint += (_, e) => { e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; e.Graphics.PixelOffsetMode = PixelOffsetMode.Half; int w = canvas.Image.Width, h = canvas.Image.Height; e.Graphics.DrawImage(canvas.Image, (livePreview.Width-w)/2, 24, w, h); float scale = Math.Min(4, Math.Min((livePreview.Width-16f)/w, (livePreview.Height-h-60f)/h)); if (scale > 0) e.Graphics.DrawImage(canvas.Image, (livePreview.Width-w*scale)/2, h+56, w*scale, h*scale); };
        workspace.Controls.Add(sidebar, 1, 0); root.Controls.Add(workspace, 0, 1);
    }

    private static Button MakeButton(string text, bool primary, string? glyph = null)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = primary ? DarkTheme.Accent : DarkTheme.SurfaceHover, ForeColor = primary ? DarkTheme.Window : DarkTheme.Text, Padding = new Padding(10, 3, 10, 3), Margin = new Padding(4), Cursor = Cursors.Hand, Font = AppFonts.Semibold(9) };
        if (glyph != null) { b.Image = GlyphPainter.Create(glyph, 16, primary ? DarkTheme.Window : DarkTheme.Text); b.ImageAlign = ContentAlignment.MiddleLeft; b.TextImageRelation = TextImageRelation.ImageBeforeText; }
        b.FlatAppearance.BorderSize = primary ? 0 : 1; b.FlatAppearance.BorderColor = DarkTheme.Border; return b;
    }
    private void PickPenColor()
    {
        using var dialog = new DarkColorPicker { Color = canvas.PenColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) { alpha.Value = dialog.Color.A; initialColor = dialog.Color; canvas.PenColor = Color.FromArgb((int)alpha.Value, dialog.Color.R, dialog.Color.G, dialog.Color.B); penColor.BackColor = dialog.Color; }
    }
    private void PushUndo(Bitmap snapshot)
    {
        undo.Push(snapshot);
        if (undo.Count > 50) { var keep = undo.Take(50).ToArray(); foreach (var old in undo.Skip(50)) old.Dispose(); undo.Clear(); for (int i=keep.Length-1;i>=0;i--) undo.Push(keep[i]); }
        foreach (var b in redo) b.Dispose(); redo.Clear();
    }
    private void Undo()
    {
        if (undo.Count == 0) return; redo.Push(new Bitmap(canvas.Image)); ReplaceImage(undo.Pop());
    }
    private void Redo()
    {
        if (redo.Count == 0) return; undo.Push(new Bitmap(canvas.Image)); ReplaceImage(redo.Pop());
    }
    private void ReplaceImage(Bitmap image) { var old = canvas.Image; canvas.Image = image; old.Dispose(); syncingDimensions = true; dimensions.SelectedIndex = Array.IndexOf(new[] {16,32,64,128,256}, image.Width); syncingDimensions = false; }
    private void ClearCanvas()
    {
        PushUndo(new Bitmap(canvas.Image)); using var g = Graphics.FromImage(canvas.Image); g.Clear(Color.Transparent); canvas.Invalidate(); livePreview.Invalidate();
    }
    private void ResizeCanvas(int n)
    {
        PushUndo(new Bitmap(canvas.Image)); var old = canvas.Image; var resized = new Bitmap(n, n, PixelFormat.Format32bppArgb); using (var g = Graphics.FromImage(resized)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(old, (n-old.Width)/2, (n-old.Height)/2); } canvas.Image = resized; old.Dispose();
    }

}
