using ClickMoves.Automation;

namespace ClickMoves.UI;

internal static class ToolStyle
{
    // Restored from the pre-BrioCraft MiniTools 1.3 source snapshot.
    public static readonly Color Accent = Color.FromArgb(24, 96, 177);
    public static readonly Color Blue = Accent;
    public static readonly Color Canvas = Color.FromArgb(241, 247, 253);
    public static readonly Color Surface = Color.White;
    public static readonly Color Text = Color.FromArgb(28, 48, 70);
    public static readonly Color Muted = Color.FromArgb(83, 101, 119);
    public static readonly Color Border = Color.FromArgb(180, 201, 221);
    public static readonly Color Selected = Color.FromArgb(221, 237, 252);
    public static readonly Color Pressed = Color.FromArgb(200, 223, 246);
    public static readonly Color PrimaryHover = Color.FromArgb(20, 81, 151);
    public static readonly Color PrimaryPressed = Color.FromArgb(16, 66, 124);
    public static readonly Color Disabled = Color.FromArgb(165, 177, 196);
    public const int Small = 4, Gap = 8, PageInset = 16, Large = 24;
    // Windows Forms' native buttons and inputs have square corners (radius 0).
    public const int CornerRadius = 0;
    public static readonly Font BodyFont = new("Segoe UI", 10.5f);
    public static readonly Font SmallFont = new("Segoe UI", 9);
    public static readonly Font HeadingFont = new("Segoe UI", 20, FontStyle.Bold);
    public static readonly Font CounterFont = new("Segoe UI", 23, FontStyle.Bold);
    public static readonly Font DialogFont = new("Segoe UI", 10);
    public static Button Button(string text, string name) => new()
    {
        Text = text, Name = name, AccessibleName = text, AutoSize = true,
        FlatStyle = FlatStyle.Flat, BackColor = Surface, ForeColor = Text,
        FlatAppearance = { BorderColor = Border, MouseOverBackColor = Selected, MouseDownBackColor = Pressed },
        Padding = new(Gap, Small, Gap, Small), MinimumSize = new(0, 36), Cursor = Cursors.Hand
    };
    public static void Primary(Button button)
    {
        button.BackColor = Accent; button.ForeColor = Color.White; button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = PrimaryHover;
        button.FlatAppearance.MouseDownBackColor = PrimaryPressed;
    }
    public static void State(Label label, bool active)
    {
        label.ForeColor = active ? Blue : Muted;
        label.BackColor = active ? Selected : Canvas;
    }
    public static void Inputs(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is TextBoxBase or NumericUpDown or ComboBox)
            { control.BackColor = Surface; control.ForeColor = Text; }
            if (control is TextBoxBase text) text.BorderStyle = BorderStyle.FixedSingle;
            if (control is NumericUpDown number) number.BorderStyle = BorderStyle.FixedSingle;
            Inputs(control);
        }
    }
    public static FlowLayoutPanel Row(params Control[] controls)
    { var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true }; row.Controls.AddRange(controls); return row; }
    public static TableLayoutPanel Page() => new() { Dock = DockStyle.Fill, Padding = new(PageInset), ColumnCount = 1, RowCount = 0, BackColor = Canvas, ForeColor = Text };
    public static void Add(TableLayoutPanel page, Control control, bool stretch = false)
    {
        page.ColumnStyles.Clear(); page.ColumnStyles.Add(new(SizeType.Percent, 100));
        page.RowStyles.Add(new(stretch ? SizeType.Percent : SizeType.AutoSize, stretch ? 100 : 0));
        control.Margin = new(0, 0, 0, Gap); page.Controls.Add(control, 0, page.RowCount++);
    }
    public static Label Label(string text) => new() { Text = text, AutoSize = true, Margin = new(4, 7, 5, 4) };
}

internal sealed class ToolNavigation : TabControl
{
    public ToolNavigation()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed; ItemSize = new(100, 36);
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected = e.Index == SelectedIndex;
        using var background = new SolidBrush(selected ? ToolStyle.Accent : ToolStyle.Canvas);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, e.Bounds,
            selected ? Color.White : ToolStyle.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues && selected)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -ToolStyle.Small, -ToolStyle.Small), Color.White, ToolStyle.Accent);
        base.OnDrawItem(e);
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}

public sealed class StepsPreview : DataGridView
{
    private IReadOnlyList<ActionStep> steps = [];
    public int SelectedStep => CurrentCell?.RowIndex ?? -1;
    public StepsPreview()
    {
        Dock = DockStyle.Fill; ReadOnly = true; VirtualMode = true; AllowUserToAddRows = false; AllowUserToDeleteRows = false;
        MultiSelect = false; SelectionMode = DataGridViewSelectionMode.FullRowSelect; RowHeadersVisible = false;
        BackgroundColor = ToolStyle.Surface; BorderStyle = BorderStyle.FixedSingle;
        EnableHeadersVisualStyles = false; GridColor = ToolStyle.Border;
        ColumnHeadersDefaultCellStyle.BackColor = ToolStyle.Selected;
        ColumnHeadersDefaultCellStyle.ForeColor = ToolStyle.Text;
        ColumnHeadersDefaultCellStyle.SelectionBackColor = ToolStyle.Selected;
        ColumnHeadersDefaultCellStyle.SelectionForeColor = ToolStyle.Text;
        DefaultCellStyle.ForeColor = ToolStyle.Text;
        DefaultCellStyle.SelectionBackColor = ToolStyle.Accent; DefaultCellStyle.SelectionForeColor = Color.White;
        AlternatingRowsDefaultCellStyle.BackColor = ToolStyle.Canvas;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AccessibleName = "Vista previa de pasos";
        Columns.Add("Index", "#"); Columns[0].FillWeight = 15;
        Columns.Add("Action", "Acción"); Columns[1].FillWeight = 70;
        Columns.Add("Delay", "Antes (ms)"); Columns[2].FillWeight = 40;
        Columns.Add("Parameters", "Parámetros"); Columns[3].FillWeight = 80;
        CellValueNeeded += (_, e) =>
        {
            if (e.RowIndex >= steps.Count) return;
            var step = steps[e.RowIndex];
            e.Value = e.ColumnIndex switch { 0 => e.RowIndex + 1, 1 => step.Label, 2 => step.DelayMs, _ => step.Parameters };
        };
    }
    public void ShowSteps(IReadOnlyList<ActionStep> value, int selection = -1)
    {
        steps = value; RowCount = value.Count;
        if (selection >= 0 && selection < RowCount) CurrentCell = Rows[selection].Cells[0];
        Invalidate();
    }
}
