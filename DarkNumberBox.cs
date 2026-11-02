using System.ComponentModel;
using System.Drawing.Drawing2D;
namespace SimpleAimOverlay;

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
        using var path = RoundedUi.Path(new Rectangle(0, 0, Width - 1, Height - 1), 7);
        e.Graphics.DrawPath(border, path);
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

