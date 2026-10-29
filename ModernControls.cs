using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SimpleAimOverlay;

internal static class DarkTheme
{
    public static readonly Color Window = Color.FromArgb(15, 18, 23);
    public static readonly Color Surface = Color.FromArgb(22, 27, 35);
    public static readonly Color Card = Color.FromArgb(27, 33, 43);
    public static readonly Color Input = Color.FromArgb(19, 24, 32);
    public static readonly Color SurfaceHover = Color.FromArgb(35, 43, 55);
    public static readonly Color Border = Color.FromArgb(47, 57, 71);
    public static readonly Color Text = Color.FromArgb(235, 240, 246);
    public static readonly Color Muted = Color.FromArgb(151, 163, 180);
    public static readonly Color Accent = Color.FromArgb(69, 220, 153);
    public static readonly Color AccentHover = Color.FromArgb(52, 195, 133);
    public static readonly Color Preview = Color.FromArgb(9, 13, 19);
}

internal sealed class DarkSlider : Control
{
    private int value;
    private bool dragging;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Minimum { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Maximum { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            int next = Math.Clamp(value, Minimum, Maximum);
            if (this.value == next) return;
            this.value = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? ValueChanged;

    public DarkSlider(int min, int max)
    {
        Minimum = min; Maximum = max; value = min; Height = 34; BackColor = DarkTheme.Surface; TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float left = 12, right = Math.Max(left + 1, Width - 12), y = Height / 2f;
        float t = Maximum <= Minimum ? 0 : (Value - Minimum) / (float)(Maximum - Minimum);
        float x = left + (right - left) * t;
        using var track = new Pen(DarkTheme.Border, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var fill = new Pen(Enabled ? DarkTheme.Accent : DarkTheme.Muted, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLine(track, left, y, right, y); e.Graphics.DrawLine(fill, left, y, x, y);
        using var thumb = new SolidBrush(Enabled ? DarkTheme.Accent : DarkTheme.Muted);
        using var ring = new Pen(DarkTheme.Surface, 2);
        e.Graphics.FillEllipse(thumb, x - 7, y - 7, 14, 14); e.Graphics.DrawEllipse(ring, x - 7, y - 7, 14, 14);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus(); dragging = true; Capture = true; SetFromX(e.X);
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) SetFromX(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Left or Keys.Down) { Value--; e.Handled = true; }
        else if (e.KeyCode is Keys.Right or Keys.Up) { Value++; e.Handled = true; }
        else if (e.KeyCode == Keys.Home) { Value = Minimum; e.Handled = true; }
        else if (e.KeyCode == Keys.End) { Value = Maximum; e.Handled = true; }
    }
    private void SetFromX(int x) { float t = Math.Clamp((x - 12f) / Math.Max(1f, Width - 24f), 0, 1); Value = Minimum + (int)Math.Round(t * (Maximum - Minimum)); }
}

internal sealed class DarkNumberBox : UserControl
{
    private readonly TextBox editor = new();
    private int value;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Minimum { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Maximum { get; set; } = 100;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            int next = Math.Clamp(value, Minimum, Maximum);
            bool changed = this.value != next;
            this.value = next; editor.Text = next.ToString(); Invalidate(); if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? ValueChanged;

    public DarkNumberBox(int min, int max, int initial = 0)
    {
        Minimum = min; Maximum = max; value = Math.Clamp(initial, min, max); Size = new Size(104, 30); MinimumSize = new Size(78, 28); BackColor = DarkTheme.Input;
        editor.BorderStyle = BorderStyle.None; editor.BackColor = DarkTheme.Input; editor.ForeColor = DarkTheme.Text; editor.Font = AppFonts.Regular(10); editor.Text = value.ToString(); editor.TextAlign = HorizontalAlignment.Center;
        editor.KeyDown += (_, e) => { if (e.KeyCode == Keys.Up) { Value++; e.Handled = true; } else if (e.KeyCode == Keys.Down) { Value--; e.Handled = true; } else if (e.KeyCode == Keys.Enter) { Commit(); e.Handled = true; e.SuppressKeyPress = true; } };
        editor.TextChanged += (_, _) => {
            // Allow intermediate empty/minus input without resetting the caret.
            if (int.TryParse(editor.Text, out int n) && n >= Minimum && n <= Maximum && n != value) {
                value = n; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        editor.Leave += (_, _) => Commit(); Controls.Add(editor); Resize += (_, _) => LayoutEditor(); LayoutEditor();
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    private void LayoutEditor() => editor.Bounds = new Rectangle(2, 3, Math.Max(24, Width - 28), Math.Max(18, Height - 6));
    private void Commit() { if (int.TryParse(editor.Text, out int n)) Value = n; else editor.Text = Value.ToString(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(Focused || editor.Focused ? DarkTheme.Accent : DarkTheme.Border);
        e.Graphics.DrawRoundedRectangle(border, new Rectangle(0, 0, Width - 1, Height - 1), 7);
        int x = Width - 24; using var divider = new Pen(DarkTheme.Border); e.Graphics.DrawLine(divider, x, 2, x, Height - 3);
        using var arrow = new Pen(DarkTheme.Muted, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLines(arrow, [new PointF(x + 7, Height / 2f - 2), new PointF(x + 10, Height / 2f - 5), new PointF(x + 13, Height / 2f - 2)]);
        e.Graphics.DrawLines(arrow, [new PointF(x + 7, Height / 2f + 2), new PointF(x + 10, Height / 2f + 5), new PointF(x + 13, Height / 2f + 2)]);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (e.Button != MouseButtons.Left || e.X < Width - 25) return;
        if (e.Y < Height / 2) Value++; else Value--; editor.Focus();
    }
}

internal sealed class DarkCheckBox : CheckBox
{
    public DarkCheckBox()
    {
        Appearance = Appearance.Button; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; AutoSize = false; Height = 42; Padding = new Padding(12, 0, 6, 0); BackColor = DarkTheme.Surface; ForeColor = DarkTheme.Text;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? DarkTheme.Surface); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int y = (Height - 15) / 2; var box = new Rectangle(10, y, 15, 15);
        using var fill = new SolidBrush(Checked ? DarkTheme.Accent : DarkTheme.Input); using var edge = new Pen(Checked ? DarkTheme.Accent : DarkTheme.Border, 1.2f);
        e.Graphics.FillRoundedRectangle(fill, box, 4); e.Graphics.DrawRoundedRectangle(edge, box, 4);
        if (Checked) { using var check = new Pen(DarkTheme.Window, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round }; e.Graphics.DrawLines(check, [new Point(14, y + 8), new Point(17, y + 11), new Point(22, y + 5)]); }
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(34, 0, Math.Max(0, Width - 40), Height), Enabled ? ForeColor : DarkTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class DarkComboBox : ComboBox
{
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x020A && !DroppedDown) { m.Result = IntPtr.Zero; return; }
        base.WndProc(ref m);
    }
    public DarkComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed; DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat; ItemHeight = 28; IntegralHeight = false; BackColor = DarkTheme.Input; ForeColor = DarkTheme.Text;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); DropDownHeight=440; MaxDropDownItems=15;
        DrawItem += DrawItemDark;
    }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(DarkTheme.Input);
        TextRenderer.DrawText(e.Graphics, UiText.T(Text), Font, new Rectangle(10,0,Width-40,Height),DarkTheme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        using var border=new Pen(Focused?DarkTheme.Accent:DarkTheme.Border);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; e.Graphics.DrawRoundedRectangle(border,new Rectangle(0,0,Width-1,Height-1),7);
        using var arrow=new Pen(DarkTheme.Muted,1.5f);int x=Width-14,y=Height/2;
        e.Graphics.DrawLines(arrow,new[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)});
    }    private void DrawItemDark(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) { using var selectedBackground = new SolidBrush(DarkTheme.Input); e.Graphics.FillRectangle(selectedBackground, e.Bounds); TextRenderer.DrawText(e.Graphics, UiText.T(Text), Font, e.Bounds, DarkTheme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left); return; }
        bool selected = (e.State & DrawItemState.Selected) != 0; using var itemBackground = new SolidBrush(selected ? DarkTheme.SurfaceHover : DarkTheme.Input); e.Graphics.FillRectangle(itemBackground, e.Bounds);
        string text = UiText.T(GetItemText(Items[e.Index]) ?? string.Empty); TextRenderer.DrawText(e.Graphics, text, Font, Rectangle.Inflate(e.Bounds, -10, 0), DarkTheme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class DarkTabHost : UserControl
{
    private readonly FlowLayoutPanel navigation = new() { Dock = DockStyle.Top, Height = 46, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = DarkTheme.Window, Padding = new Padding(0, 0, 0, 0) };
    private readonly Panel content = new() { Dock = DockStyle.Fill, BackColor = DarkTheme.Surface };
    private readonly List<(Button Button, Control Page)> pages = [];
    private int selected = -1;

    public DarkTabHost()
    {
        BackColor = DarkTheme.Window; Controls.Add(content); Controls.Add(navigation);
    }
    public void AddPage(string title, string glyph, Control page)
    {
        int index = pages.Count; page.Dock = DockStyle.Fill; page.Visible = false; content.Controls.Add(page);
        var button = new NavigationButton { Text = title, Image = GlyphPainter.Create(glyph, 18), ImageAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Width = 126, Height = 42, Margin = new Padding(0, 0, 5, 0), Padding = new Padding(14, 0, 10, 0), FlatStyle = FlatStyle.Flat, BackColor = DarkTheme.Window, ForeColor = DarkTheme.Muted, Font = AppFonts.Semibold(9.5f), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = DarkTheme.SurfaceHover; button.Click += (_, _) => SelectPage(index); navigation.Controls.Add(button); pages.Add((button, page));
        if (selected < 0) SelectPage(0);
    }
    private void SelectPage(int index)
    {
        selected = index;
        for (int i = 0; i < pages.Count; i++)
        {
            bool active = i == selected; pages[i].Page.Visible = active; pages[i].Page.BringToFront();
            pages[i].Button.BackColor = active ? DarkTheme.Surface : DarkTheme.Window; pages[i].Button.ForeColor = active ? DarkTheme.Text : DarkTheme.Muted;
            pages[i].Button.Image?.Dispose(); pages[i].Button.Image = GlyphPainter.Create(new[] { "aim", "edit", "settings" }[i % 3], 18, active ? DarkTheme.Accent : DarkTheme.Muted);
        }
    }
}

internal static class GlyphPainter
{
    public static Bitmap Create(string name, int size, Color? color = null)
    {
        var bmp = new Bitmap(size, size); using var g = Graphics.FromImage(bmp); g.SmoothingMode = SmoothingMode.AntiAlias;
        Color c = color ?? DarkTheme.Text; using var p = new Pen(c, Math.Max(1.5f, size / 10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; using var b = new SolidBrush(c);
        float m = size * .18f, r = size - m * 2, cx = size / 2f, cy = size / 2f;
        switch (name)
        {
            case "aim": g.DrawEllipse(p, m + 2, m + 2, r - 4, r - 4); g.DrawLine(p, cx, m, cx, size * .37f); g.DrawLine(p, cx, size * .63f, cx, size - m); g.DrawLine(p, m, cy, size * .37f, cy); g.DrawLine(p, size * .63f, cy, size - m, cy); g.FillEllipse(b, cx - 1.5f, cy - 1.5f, 3, 3); break;
            case "library": for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) g.DrawRoundedRectangle(p, m + x * (r / 2 + 1), m + y * (r / 2 + 1), r / 2 - 2, r / 2 - 2, 2); break;
            case "profiles": g.DrawEllipse(p, cx - r * .18f, m, r * .36f, r * .36f); g.DrawArc(p, m, cy - 1, r * .62f, r * .56f, 190, 160); g.DrawArc(p, cx - 2, cy, r * .68f, r * .52f, 190, 160); break;
            case "settings": g.DrawEllipse(p, cx - r * .27f, cy - r * .27f, r * .54f, r * .54f); for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(p, cx + (float)Math.Cos(a) * r * .34f, cy + (float)Math.Sin(a) * r * .34f, cx + (float)Math.Cos(a) * r * .5f, cy + (float)Math.Sin(a) * r * .5f); } break;
            case "edit": g.DrawLine(p, m, size - m, size - m, m); g.DrawLine(p, m, size - m, size * .38f, size * .75f); break;
            case "import": g.DrawLine(p, cx, m, cx, cy + 2); g.DrawLines(p, [new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2)]); g.DrawLine(p, m, size - m, size - m, size - m); break;
            case "delete": g.DrawRectangle(p, m + 2, cy - 2, r - 4, r * .58f); g.DrawLine(p, m, cy - 4, size - m, cy - 4); g.DrawLine(p, cx - 3, m, cx + 3, m); break;
            case "save": g.DrawRectangle(p, m, m, r, r); g.DrawRectangle(p, cx - 4, m, 8, 5); g.DrawRectangle(p, cx - 4, cy + 1, 8, r * .32f); break;
            case "load": g.DrawLine(p, cx, m, cx, cy + 2); g.DrawLines(p, [new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2)]); g.DrawRectangle(p, m + 1, cy + 4, r - 2, size - cy - m - 4); break;
            case "refresh": g.DrawArc(p, m, m, r, r, 35, 255); g.DrawLines(p, [new PointF(size - m, cy - 4), new PointF(size - m, m + 1), new PointF(size - m - 5, m + 1)]); break;
            case "color": g.DrawEllipse(p, m, m, r, r); g.DrawLine(p, cx, m + 2, cx, size - m - 2); g.DrawLine(p, m + 2, cy, size - m - 2, cy); break;
            case "visibility": g.DrawArc(p, m, cy - r * .35f, r, r * .7f, 0, 180); g.DrawArc(p, m, cy - r * .35f, r, r * .7f, 180, 180); g.DrawEllipse(p, cx - 2, cy - 2, 4, 4); break;
            case "hotkey": g.DrawRoundedRectangle(p, m, cy - r * .28f, r, r * .56f, 4); for (int i = 0; i < 3; i++) g.DrawLine(p, m + 4 + i * 4, cy - 1, m + 6 + i * 4, cy - 1); break;
            case "monitor": g.DrawRectangle(p, m, m, r, r * .67f); g.DrawLine(p, cx, cy + r * .34f, cx, size - m); g.DrawLine(p, cx - 4, size - m, cx + 4, size - m); break;
            case "image": g.DrawRoundedRectangle(p, m, m, r, r, 2); g.FillEllipse(b, m + 3, m + 3, 3, 3); g.DrawLines(p, [new PointF(m + 2, size - m - 3), new PointF(cx - 2, cy + 1), new PointF(cx + 1, cy + 4), new PointF(size - m - 2, cy - 1)]); break;
            case "undo": g.DrawArc(p, m, m + 2, r, r, 210, 220); g.DrawLines(p, [new PointF(m + 1, cy), new PointF(m + 1, cy - 5), new PointF(m + 6, cy - 5)]); break;
            case "redo": g.DrawArc(p, m, m + 2, r, r, -30, 220); g.DrawLines(p, [new PointF(size - m - 1, cy), new PointF(size - m - 1, cy - 5), new PointF(size - m - 6, cy - 5)]); break;
            case "clear": g.DrawRectangle(p, m + 2, m + 2, r - 4, r - 4); g.DrawLine(p, m + 3, m + 3, size - m - 3, size - m - 3); g.DrawLine(p, size - m - 3, m + 3, m + 3, size - m - 3); break;
            case "eraser": g.DrawLines(p, [new PointF(m, cy), new PointF(cx - 1, m), new PointF(size - m, cy), new PointF(cx + 1, size - m), new PointF(m, cy)]); break;
            default: g.DrawEllipse(p, m, m, r, r); break;
        }
        return bmp;
    }
}

internal static class BrandIcon
{
    public static Icon Load()
    {
        using var stream = typeof(BrandIcon).Assembly.GetManifestResourceStream("SimpleAimOverlay.Assets.simple-aim-overlay.ico");
        return stream != null ? new Icon(stream) : SystemIcons.Application;
    }
}

internal static class DarkPrompt
{
    public static string? Show(IWin32Window owner, string title, string prompt, string initial)
    {
        using var form = new Form { Text = title, Icon = BrandIcon.Load(), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, ClientSize = new Size(420, 172), BackColor = DarkTheme.Window, ForeColor = DarkTheme.Text, Font = AppFonts.Regular(9.5f) };
        var label = new Label { Text = prompt, AutoSize = false, Dock = DockStyle.Top, Height = 46, ForeColor = DarkTheme.Muted, TextAlign = ContentAlignment.MiddleLeft };
        var input = new TextBox { Text = initial, Dock = DockStyle.Top, Height = 34, BorderStyle = BorderStyle.FixedSingle, BackColor = DarkTheme.Input, ForeColor = DarkTheme.Text, Font = AppFonts.Regular(10) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 4, 0, 0), BackColor = DarkTheme.Window };
        var save = new RoundedButton { Text = "Save", DialogResult = DialogResult.OK, Width = 92, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = DarkTheme.Accent, ForeColor = DarkTheme.Window, Font = AppFonts.Semibold(9), Image = GlyphPainter.Create("save", 15, DarkTheme.Window), ImageAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText, Cursor = Cursors.Hand };
        save.FlatAppearance.BorderSize = 0;
        var cancel = new RoundedButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 92, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = DarkTheme.SurfaceHover, ForeColor = DarkTheme.Text, Font = AppFonts.Semibold(9), Cursor = Cursors.Hand };
        cancel.FlatAppearance.BorderColor = DarkTheme.Border; buttons.Controls.Add(save); buttons.Controls.Add(cancel);
        form.Padding = new Padding(20, 14, 20, 12); form.Controls.Add(input); form.Controls.Add(label); form.Controls.Add(buttons); form.AcceptButton = save; form.CancelButton = cancel;
        form.Shown += (_, _) => { UiText.Apply(form); RoundedUi.Apply(form); input.Focus(); input.SelectAll(); };
        return form.ShowDialog(owner) == DialogResult.OK ? input.Text.Trim() : null;
    }
}

internal static class RoundedDrawing
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius) { using var path=Create(rect,radius);g.FillPath(brush,path); }
    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius) { using var path=Create(rect,radius);g.DrawPath(pen,path); }
    public static void DrawRoundedRectangle(this Graphics g, Pen pen, float x, float y, float w, float h, int radius) { using var path=Create(Rectangle.Round(new RectangleF(x,y,w,h)),radius);g.DrawPath(pen,path); }
    private static GraphicsPath Create(Rectangle r, int radius) { var p = new GraphicsPath(); float d = radius * 2; p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
}

internal sealed class NavigationButton : Button
{
    public NavigationButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override bool ShowFocusCues => false;
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) {
        // Never invoke native Button painting: it adds a default-button border on focus changes.
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var background=new SolidBrush(BackColor);
        e.Graphics.FillRoundedRectangle(background,new Rectangle(0,0,Width-1,Height-1),8);
        int left=Padding.Left;
        if(Image!=null){e.Graphics.DrawImage(Image,left,(Height-Image.Height)/2);left+=Image.Width+5;}
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(left,0,Width-left-Padding.Right,Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused && FindForm()?.ContainsFocus == true){using var accent=new Pen(DarkTheme.Accent,2);e.Graphics.DrawLine(accent,16,Height-3,Width-16,Height-3);}
    }
}
internal sealed class DarkListBox : UserControl
{
    private readonly NativeList list = new();
    private readonly Panel viewport = new();
    private readonly ScrollThumb thumb;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public ListBox.ObjectCollection Items => list.Items;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public object? SelectedItem { get=>list.SelectedItem; set=>list.SelectedItem=value; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public DrawMode DrawMode { get=>list.DrawMode;set=>list.DrawMode=value; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int ItemHeight {get=>list.ItemHeight;set {list.ItemHeight=value;thumb.Invalidate();}}
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool IntegralHeight {get=>list.IntegralHeight;set=>list.IntegralHeight=value;}
    public event EventHandler? ItemDoubleClick;
    public event DrawItemEventHandler? DrawItem;
    public event EventHandler? SelectedIndexChanged;
    public DarkListBox() {
        thumb=new ScrollThumb(this){Dock=DockStyle.Right,Width=14};
        viewport.Dock=DockStyle.Fill; viewport.Controls.Add(list);
        Controls.Add(viewport);Controls.Add(thumb);
        list.BorderStyle=BorderStyle.None;list.IntegralHeight=false;list.ScrollAlwaysVisible=true;
        list.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left && list.IndexFromPoint(e.Location) >= 0) ItemDoubleClick?.Invoke(this, EventArgs.Empty); };
        list.DrawItem+=(_,e)=>DrawItem?.Invoke(this,e);
        list.SelectedIndexChanged+=(_,e)=>SelectedIndexChanged?.Invoke(this,e);
        list.Changed=()=>thumb.Invalidate(); list.Wheel=ScrollWheel;
        viewport.SizeChanged+=(_,_)=>ResizeList();
        list.BackColor=DarkTheme.Card;list.ForeColor=DarkTheme.Text;
    }
    private void ResizeList() {list.SetBounds(0,0,viewport.ClientSize.Width+SystemInformation.VerticalScrollBarWidth,viewport.ClientSize.Height);thumb.Invalidate();}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(list!=null)list.Font=Font;}
    protected override void OnBackColorChanged(EventArgs e){base.OnBackColorChanged(e);if(list!=null)list.BackColor=BackColor;}
    protected override void OnForeColorChanged(EventArgs e){base.OnForeColorChanged(e);if(list!=null)list.ForeColor=ForeColor;}
    protected override void OnInvalidated(InvalidateEventArgs e){base.OnInvalidated(e);list?.Invalidate();}
    private int VisibleRows=>Math.Max(1,list.ClientSize.Height/Math.Max(1,list.ItemHeight));
    private int MaxTop=>Math.Max(0,list.Items.Count-VisibleRows);
    private void SetTop(int value){if(list.Items.Count>0)list.TopIndex=Math.Clamp(value,0,MaxTop);thumb.Invalidate();}
    private int wheelRemainder;
    private void ScrollWheel(int delta) {
        wheelRemainder += delta;
        int steps = wheelRemainder / 120; wheelRemainder %= 120;
        if (steps != 0) SetTop(list.TopIndex - steps);
    }
    private sealed class NativeList : ListBox {
        public Action? Changed; public Action<int>? Wheel;
        protected override void WndProc(ref Message m){if(m.Msg == 0x20A) { Wheel?.Invoke(unchecked((short)(m.WParam.ToInt64() >> 16))); m.Result=IntPtr.Zero; return; } base.WndProc(ref m);if(m.Msg is 0x115 or 0x20A or 0x100 or 0x180 or 0x181 or 0x184 or 0x197 or 0x186)Changed?.Invoke();}
    }
    private sealed class ScrollThumb : Control {
        readonly DarkListBox owner; bool dragging; int offset;
        public ScrollThumb(DarkListBox owner){this.owner=owner;BackColor=DarkTheme.Card;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
        int ThumbHeight=>Math.Min(Height,Math.Max(28,Height*owner.VisibleRows/Math.Max(1,owner.Items.Count)));
        int Travel=>Math.Max(0,Height-ThumbHeight);
        int ThumbTop=>owner.MaxTop==0?0:Travel*owner.list.TopIndex/owner.MaxTop;
        protected override void OnPaint(PaintEventArgs e){if(owner.MaxTop==0)return;using var b=new SolidBrush(dragging?DarkTheme.Accent:DarkTheme.Muted);e.Graphics.FillRectangle(b,4,ThumbTop,6,ThumbHeight);}
        void MoveTo(int y){owner.SetTop(Travel==0?0:(int)Math.Round((double)(y-offset)*owner.MaxTop/Travel));}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;offset=e.Y>=ThumbTop&&e.Y<ThumbTop+ThumbHeight?e.Y-ThumbTop:ThumbHeight/2;dragging=true;Capture=true;owner.list.Focus();MoveTo(e.Y);}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)MoveTo(e.Y);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;Invalidate();}
        protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);owner.ScrollWheel(e.Delta);}
    }
}
