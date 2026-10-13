using System.Drawing.Drawing2D;
using ClickMoves.Configuration;

namespace ClickMoves.UI;

/// <summary>Vector paths; only the two top buttons hit-test.</summary>
public sealed class MouseSelector : Control
{
    public static readonly Color Accent = ToolStyle.Accent;
    private ClickButton selected;
    public event EventHandler? SelectionChanged;
    [System.ComponentModel.DefaultValue(ClickButton.Left)]
    public ClickButton SelectedButton
    {
        get => selected;
        set
        {
            if (value == selected) return;
            selected = value;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.Selection, (int)value);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public MouseSelector()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        TabStop = true;
        AccessibleName = "Botón del mouse";
        AccessibleDescription = "Usa las flechas izquierda y derecha para seleccionar el lado.";
        Size = new(150, 208);
    }
    private (float scale, float x, float y) LayoutVector()
    {
        float scale = Math.Min((ClientSize.Width - 8) / 150f, (ClientSize.Height - 8) / 210f);
        return (Math.Max(0.01f, scale), (ClientSize.Width - 150 * scale) / 2, (ClientSize.Height - 210 * scale) / 2);
    }
    private static GraphicsPath LeftPath()
    {
        var p = new GraphicsPath();
        p.StartFigure();
        p.AddBezier(72, 9, 31, 9, 9, 30, 7, 86);
        p.AddBezier(7, 86, 7, 108, 17, 115, 31, 115);
        p.AddLine(31, 115, 65, 115);
        p.AddBezier(65, 115, 70, 115, 72, 113, 72, 108);
        p.AddLine(72, 108, 72, 89);
        p.AddBezier(72, 89, 59, 89, 57, 82, 57, 75);
        p.AddLine(57, 75, 57, 57);
        p.AddBezier(57, 57, 57, 47, 62, 43, 72, 43);
        p.AddLine(72, 43, 72, 9);
        p.CloseFigure();
        return p;
    }
    private static GraphicsPath RightPath()
    {
        var p = LeftPath();
        using var mirror = new Matrix(-1, 0, 0, 1, 150, 0);
        p.Transform(mirror);
        return p;
    }
    public ClickButton? HitTest(Point point)
    {
        var (scale, x, y) = LayoutVector();
        var p = new PointF((point.X - x) / scale, (point.Y - y) / scale);
        using var left = LeftPath();
        using var right = RightPath();
        return left.IsVisible(p) ? ClickButton.Left : right.IsVisible(p) ? ClickButton.Right : null;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var (scale, x, y) = LayoutVector();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var state = e.Graphics.Save();
        e.Graphics.TranslateTransform(x, y);
        e.Graphics.ScaleTransform(scale, scale);
        using var body = new GraphicsPath();
        body.AddBezier(75, 7, 21, 7, 4, 43, 4, 101);
        body.AddBezier(4, 101, 4, 167, 20, 203, 75, 203);
        body.AddBezier(75, 203, 130, 203, 146, 167, 146, 101);
        body.AddBezier(146, 101, 146, 43, 129, 7, 75, 7);
        body.CloseFigure();
        using var neutral = new SolidBrush(ToolStyle.Selected);
        using var border = new Pen(ToolStyle.Border, 1.2f);
        using var accent = new SolidBrush(Enabled ? Accent : ToolStyle.Disabled);
        e.Graphics.FillPath(neutral, body);
        e.Graphics.DrawPath(border, body);
        using var left = LeftPath();
        using var right = RightPath();
        using var unselected = new SolidBrush(ToolStyle.Surface);
        e.Graphics.FillPath(selected == ClickButton.Left ? accent : unselected, left);
        e.Graphics.FillPath(selected == ClickButton.Right ? accent : unselected, right);
        e.Graphics.DrawPath(border, left);
        e.Graphics.DrawPath(border, right);
        using var wheel = new SolidBrush(ToolStyle.Muted);
        using var wheelPath = new GraphicsPath();
        wheelPath.AddArc(66, 49, 18, 18, 180, 180); wheelPath.AddArc(66, 65, 18, 18, 0, 180); wheelPath.CloseFigure();
        e.Graphics.FillPath(wheel, wheelPath);
        using var line = new Pen(ToolStyle.Surface, 1);
        for (int yy = 57; yy <= 75; yy += 6) e.Graphics.DrawLine(line, 70, yy, 80, yy);
        e.Graphics.Restore(state);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -2, -2));
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left) return;
        Focus();
        if (HitTest(e.Location) is { } side) SelectedButton = side;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = Enabled && HitTest(e.Location) != null ? Cursors.Hand : Cursors.Default;
    }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right || base.IsInputKey(keyData);
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // TabControl processes arrow commands before IsInputKey; consume them here.
        if (Enabled && (keyData & Keys.KeyCode) is Keys.Left or Keys.Right)
        {
            SelectedButton = (keyData & Keys.KeyCode) == Keys.Left ? ClickButton.Left : ClickButton.Right;
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Enabled && e.KeyCode is Keys.Left or Keys.Right)
        {
            SelectedButton = e.KeyCode == Keys.Left ? ClickButton.Left : ClickButton.Right;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override AccessibleObject CreateAccessibilityInstance() => new MouseAccessible(this);
    private sealed class MouseAccessible(MouseSelector owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Grouping;
        public override int GetChildCount() => 2;
        public override AccessibleObject? GetChild(int index) => index is 0 or 1 ? new SideAccessible(owner, index) : null;
    }
    private sealed class SideAccessible(MouseSelector owner, int index) : AccessibleObject
    {
        public override string? Name { get => index == 0 ? "Izquierdo" : "Derecho"; set { } }
        public override AccessibleRole Role => AccessibleRole.RadioButton;
        public override AccessibleObject? Parent => owner.AccessibilityObject;
        public override string DefaultAction => "Seleccionar";
        public override AccessibleStates State => AccessibleStates.Selectable | (owner.SelectedButton == (ClickButton)index ? AccessibleStates.Checked | AccessibleStates.Selected : 0) | (!owner.Enabled ? AccessibleStates.Unavailable : 0);
        public override Rectangle Bounds
        {
            get
            {
                var (s, x, y) = owner.LayoutVector();
                return owner.RectangleToScreen(new((int)(x + (index == 0 ? 7 : 78) * s), (int)(y + 9 * s), (int)(65 * s), (int)(106 * s)));
            }
        }
        public override void DoDefaultAction() { if (owner.Enabled) { owner.Focus(); owner.SelectedButton = (ClickButton)index; } }
    }
}
