using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Windows.Forms;

namespace SimpleAimOverlay;

internal sealed class PixelCrosshairEditor : Form
{
    private readonly PixelCanvas canvas;
    private readonly Stack<Bitmap> undo = new(), redo = new();
    private readonly TrackBar zoom = new() { Minimum = 3, Maximum = 20, Value = 10, TickStyle = TickStyle.None, Width = 150, Height = 34 };
    private readonly NumericUpDown brushSize = new() { Minimum = 1, Maximum = 16, Value = 1, Width = 64 };
    private readonly NumericUpDown alpha = new() { Minimum = 0, Maximum = 255, Value = 255, Width = 68 };
    private readonly Label zoomValue = new() { AutoSize = true, Text = "10×", ForeColor = DarkTheme.Muted, Padding = new Padding(2, 8, 8, 0) };
    private readonly Label pixelValue = new() { AutoSize = true, Text = "RGBA —", ForeColor = DarkTheme.Muted, Padding = new Padding(8, 8, 4, 0) };
    private readonly Button penColor = new() { Text = "Колір пензля", AutoSize = true, Height = 36, FlatStyle = FlatStyle.Flat, Padding = new Padding(10, 3, 10, 3), Cursor = Cursors.Hand };
    private readonly CheckBox eraser = new() { Text = "Ластик", AutoSize = true, ForeColor = DarkTheme.Text, BackColor = DarkTheme.Surface, Padding = new Padding(8, 9, 8, 0) };
    private readonly CheckBox tintToAim = new() { Text = "Колір із вкладки «Приціл»", AutoSize = true, Checked = true, ForeColor = DarkTheme.Text, BackColor = DarkTheme.Surface, Padding = new Padding(6, 9, 6, 0) };
    private readonly bool resizableCanvas;
    private readonly ComboBox dimensions = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private Color initialColor;

    public Bitmap? ResultImage { get; private set; }
    public bool TintForOverlay => tintToAim.Checked;

    public PixelCrosshairEditor(string? imagePath, Color color)
    {
        Text = imagePath == null ? "Новий приціл" : "Редактор прицілу";
        StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(880, 640); Size = new Size(1180, 820);
        FormBorderStyle = FormBorderStyle.Sizable; BackColor = DarkTheme.Window; ForeColor = DarkTheme.Text; Font = new Font("Segoe UI", 9.5f);
        initialColor = color; resizableCanvas = imagePath == null;
        if (imagePath == null) canvas = new PixelCanvas(new Bitmap(64, 64, PixelFormat.Format32bppArgb));
        else { using var source = Image.FromFile(imagePath); canvas = new PixelCanvas(new Bitmap(source)); }
        canvas.StrokeStarted += PushUndo;
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(root);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = DarkTheme.Surface, WrapContents = true, Padding = new Padding(4), AutoScroll = true };
        var undoButton = MakeButton("↶  Назад", false); undoButton.Click += (_, _) => Undo(); toolbar.Controls.Add(undoButton);
        var redoButton = MakeButton("↷  Повтор", false); redoButton.Click += (_, _) => Redo(); toolbar.Controls.Add(redoButton);
        var clear = MakeButton("Очистити", false); clear.Click += (_, _) => ClearCanvas(); toolbar.Controls.Add(clear);
        var zoomOut = MakeButton("−", false); zoomOut.Click += (_, _) => zoom.Value = Math.Max(zoom.Minimum, zoom.Value - 1); toolbar.Controls.Add(zoomOut);
        toolbar.Controls.Add(zoom); toolbar.Controls.Add(zoomValue);
        var zoomIn = MakeButton("+", false); zoomIn.Click += (_, _) => zoom.Value = Math.Min(zoom.Maximum, zoom.Value + 1); toolbar.Controls.Add(zoomIn);
        zoom.ValueChanged += (_, _) => { zoomValue.Text = $"{zoom.Value}×"; canvas.PixelSize = zoom.Value; };
        penColor.BackColor = initialColor; penColor.ForeColor = Color.White; penColor.FlatAppearance.BorderColor = DarkTheme.Border; penColor.Click += (_, _) => PickPenColor(); toolbar.Controls.Add(penColor);
        toolbar.Controls.Add(new Label { Text = "Пензель", AutoSize = true, ForeColor = DarkTheme.Muted, Padding = new Padding(6, 9, 0, 0), BackColor = DarkTheme.Surface });
        brushSize.BackColor = DarkTheme.Input; brushSize.ForeColor = DarkTheme.Text; brushSize.ValueChanged += (_, _) => canvas.BrushSize = (int)brushSize.Value; toolbar.Controls.Add(brushSize);
        dimensions.BackColor = DarkTheme.Input; dimensions.ForeColor = DarkTheme.Text;
        alpha.BackColor = DarkTheme.Input; alpha.ForeColor = DarkTheme.Text; alpha.ValueChanged += (_, _) => canvas.PenColor = Color.FromArgb((int)alpha.Value, initialColor.R, initialColor.G, initialColor.B); toolbar.Controls.Add(new Label { Text = "Альфа", AutoSize = true, ForeColor = DarkTheme.Muted, Padding = new Padding(5, 9, 0, 0), BackColor = DarkTheme.Surface }); toolbar.Controls.Add(alpha);
        eraser.CheckedChanged += (_, _) => canvas.Eraser = eraser.Checked; toolbar.Controls.Add(eraser);
        toolbar.Controls.Add(tintToAim);
        if (resizableCanvas)
        {
            dimensions.Items.AddRange(["16 × 16", "32 × 32", "64 × 64", "128 × 128", "256 × 256"]); dimensions.SelectedIndex = 2;
            dimensions.SelectedIndexChanged += (_, _) => { if (dimensions.SelectedItem is string v) { int n = int.Parse(v.Split(' ')[0]); ResizeCanvas(n); } };
            toolbar.Controls.Add(new Label { Text = "Полотно", AutoSize = true, ForeColor = DarkTheme.Muted, Padding = new Padding(6, 9, 0, 0), BackColor = DarkTheme.Surface }); toolbar.Controls.Add(dimensions);
        }
        toolbar.Controls.Add(pixelValue);
        var save = MakeButton("Зберегти в бібліотеку", true); save.Click += (_, _) => { ResultImage = new Bitmap(canvas.Image); DialogResult = DialogResult.OK; Close(); }; toolbar.Controls.Add(save);
        var cancel = MakeButton("Скасувати", false); cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); }; toolbar.Controls.Add(cancel);
        root.Controls.Add(toolbar, 0, 0);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DarkTheme.Card, Padding = new Padding(14) };
        canvas.PixelSize = zoom.Value; scroll.Controls.Add(canvas); root.Controls.Add(scroll, 0, 1);
    }

    private static Button MakeButton(string text, bool primary)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = primary ? DarkTheme.Accent : DarkTheme.SurfaceHover, ForeColor = Color.White, Padding = new Padding(10, 3, 10, 3), Margin = new Padding(4), Cursor = Cursors.Hand };
        b.FlatAppearance.BorderSize = primary ? 0 : 1; b.FlatAppearance.BorderColor = DarkTheme.Border; return b;
    }
    private void PickPenColor()
    {
        using var dialog = new ColorDialog { Color = canvas.PenColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) { initialColor = dialog.Color; canvas.PenColor = Color.FromArgb((int)alpha.Value, dialog.Color.R, dialog.Color.G, dialog.Color.B); penColor.BackColor = dialog.Color; }
    }
    private void PushUndo(Bitmap snapshot)
    {
        undo.Push(snapshot);
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
    private void ReplaceImage(Bitmap image) { var old = canvas.Image; canvas.Image = image; old.Dispose(); }
    private void ClearCanvas()
    {
        PushUndo(new Bitmap(canvas.Image)); using var g = Graphics.FromImage(canvas.Image); g.Clear(Color.Transparent); canvas.Invalidate();
    }
    private void ResizeCanvas(int n)
    {
        PushUndo(new Bitmap(canvas.Image)); var old = canvas.Image; canvas.Image = new Bitmap(n, n, PixelFormat.Format32bppArgb); old.Dispose();
    }

    private sealed class PixelCanvas : Control
    {
        private Bitmap image;
        private bool drawing, eraseStroke;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Bitmap Image { get => image; set { image = value; UpdateSize(); Invalidate(); } }
        private int pixelSize = 10;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int PixelSize { get => pixelSize; set { pixelSize = Math.Clamp(value, 2, 24); UpdateSize(); Invalidate(); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int BrushSize { get; set; } = 1;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color PenColor { get; set; } = Color.LimeGreen;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Eraser { get; set; }
        public event Action<Bitmap>? StrokeStarted;
        public event Action<Color>? PointerColorChanged;
        public PixelCanvas(Bitmap bitmap)
        {
            image = bitmap; BackColor = DarkTheme.Card; DoubleBuffered = true; Cursor = Cursors.Cross;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); UpdateSize();
        }
        private void UpdateSize() { Size = new Size(Math.Max(1, image.Width * pixelSize + 1), Math.Max(1, image.Height * pixelSize + 1)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(DarkTheme.Card); e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            using var first = new SolidBrush(DarkTheme.Input); using var second = new SolidBrush(DarkTheme.Surface);
            for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
            {
                e.Graphics.FillRectangle(((x + y) & 1) == 0 ? first : second, x * PixelSize, y * PixelSize, PixelSize, PixelSize);
            }
            e.Graphics.DrawImage(image, new Rectangle(0, 0, image.Width * PixelSize, image.Height * PixelSize));
            if (PixelSize >= 5)
            {
                using var grid = new Pen(Color.FromArgb(50, 190, 200, 215));
                for (int x = 0; x <= image.Width; x++) e.Graphics.DrawLine(grid, x * PixelSize, 0, x * PixelSize, image.Height * PixelSize);
                for (int y = 0; y <= image.Height; y++) e.Graphics.DrawLine(grid, 0, y * PixelSize, image.Width * PixelSize, y * PixelSize);
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); if (e.Button is not (MouseButtons.Left or MouseButtons.Right)) return;
            drawing = true; eraseStroke = e.Button == MouseButtons.Right || Eraser; Capture = true;
            StrokeStarted?.Invoke(new Bitmap(image)); PaintAt(e.Location);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); if (e.X >= 0 && e.Y >= 0 && e.X < image.Width * PixelSize && e.Y < image.Height * PixelSize)
            {
                int x = e.X / PixelSize, y = e.Y / PixelSize; PointerColorChanged?.Invoke(image.GetPixel(x, y));
                if (drawing && (e.Button & (MouseButtons.Left | MouseButtons.Right)) != 0) PaintAt(e.Location);
            }
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drawing = false; Capture = false; }
        private void PaintAt(Point p)
        {
            int cx = p.X / PixelSize, cy = p.Y / PixelSize, start = (BrushSize - 1) / 2;
            for (int y = cy - start; y < cy - start + BrushSize; y++) for (int x = cx - start; x < cx - start + BrushSize; x++)
                if (x >= 0 && y >= 0 && x < image.Width && y < image.Height) image.SetPixel(x, y, eraseStroke ? Color.Transparent : PenColor);
            Invalidate(new Rectangle((cx - BrushSize) * PixelSize, (cy - BrushSize) * PixelSize, (BrushSize * 2 + 1) * PixelSize, (BrushSize * 2 + 1) * PixelSize));
        }
    }
}
