using ClickMoves.Automation;

namespace ClickMoves.UI;

public sealed class StepEditor : Form
{
    private readonly ComboBox action = new() { Name = "StepKind", DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly ComboBox key = new() { Name = "StepKey", DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly NumericUpDown delay = new() { Name = "StepDelay", Minimum = 0, Maximum = 3_600_000, Width = 130 };
    private readonly NumericUpDown x = new() { Name = "StepX", Minimum = -1_000_000, Maximum = 1_000_000, Width = 100 };
    private readonly NumericUpDown y = new() { Name = "StepY", Minimum = -1_000_000, Maximum = 1_000_000, Width = 100 };
    private readonly CheckBox position = new() { Name = "StepPosition", Text = "Usar coordenadas", AutoSize = true };
    private readonly CheckBox extended = new() { Name = "StepExtended", Text = "Tecla extendida", AutoSize = true };
    private readonly Label notice = new() { AutoSize = true, MaximumSize = new(310, 0), ForeColor = ToolStyle.Blue };
    public ActionStep Result() => new()
    {
        Kind = ((KindItem)action.SelectedItem!).Kind, DelayMs = (int)delay.Value,
        X = position.Enabled && position.Checked ? (int)x.Value : null,
        Y = position.Enabled && position.Checked ? (int)y.Value : null,
        KeyCode = key.Enabled ? ((KeyItem)key.SelectedItem!).Code : null, Extended = key.Enabled && extended.Checked
    };
    public StepEditor(ActionStep? initial = null)
    {
        Text = initial == null ? "Agregar paso" : "Editar paso"; StartPosition = FormStartPosition.CenterParent;
        ClientSize = new(350, 345); Font = ToolStyle.DialogFont; FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false; BackColor = ToolStyle.Canvas; ForeColor = ToolStyle.Text; Icon = Branding.LoadIcon();
        foreach (var kind in Enum.GetValues<ActionKind>()) action.Items.Add(new KindItem(kind));
        for (int code = 8; code <= 254; code++) if (Enum.IsDefined((Keys)code)) key.Items.Add(new KeyItem(code));
        action.SelectedIndex = initial == null ? 0 : (int)initial.Kind;
        key.SelectedItem = key.Items.Cast<KeyItem>().FirstOrDefault(k => k.Code == (initial?.KeyCode ?? 65)) ?? key.Items[0];
        delay.Value = initial?.DelayMs ?? 100; position.Checked = initial?.X != null;
        x.Value = initial?.X ?? Cursor.Position.X; y.Value = initial?.Y ?? Cursor.Position.Y; extended.Checked = initial?.Extended ?? false;
        var page = ToolStyle.Page();
        ToolStyle.Add(page, ToolStyle.Row(ToolStyle.Label("Acción"), action));
        ToolStyle.Add(page, ToolStyle.Row(ToolStyle.Label("Antes (ms)"), delay));
        ToolStyle.Add(page, ToolStyle.Row(ToolStyle.Label("Tecla"), key));
        ToolStyle.Add(page, extended);
        ToolStyle.Add(page, position);
        ToolStyle.Add(page, ToolStyle.Row(ToolStyle.Label("X"), x, ToolStyle.Label("Y"), y));
        var ok = ToolStyle.Button("Aceptar", "StepAccept"); var cancel = ToolStyle.Button("Cancelar", "StepCancel");
        ToolStyle.Add(page, ToolStyle.Row(ok, cancel)); ToolStyle.Add(page, notice); Controls.Add(page);
        ToolStyle.Primary(ok); ToolStyle.Inputs(this);
        action.AccessibleName = "Tipo de acción"; delay.AccessibleName = "Espera en milisegundos"; key.AccessibleName = "Tecla";
        x.AccessibleName = "Coordenada X"; y.AccessibleName = "Coordenada Y";
        void UpdateFields()
        {
            var kind = ((KindItem)action.SelectedItem!).Kind;
            bool keyboard = kind is ActionKind.KeyDown or ActionKind.KeyUp;
            key.Enabled = extended.Enabled = keyboard; position.Enabled = !keyboard && kind != ActionKind.Wait;
            if (kind == ActionKind.MouseMove) position.Checked = true;
            x.Enabled = y.Enabled = position.Enabled && position.Checked;
        }
        action.SelectedIndexChanged += (_, _) => UpdateFields(); position.CheckedChanged += (_, _) => UpdateFields();
        ok.Click += (_, _) => { try { Result().Validate(); DialogResult = DialogResult.OK; } catch (ArgumentException ex) { notice.Text = ex.Message; } };
        cancel.DialogResult = DialogResult.Cancel; AcceptButton = ok; CancelButton = cancel; UpdateFields();
    }
    private sealed record KindItem(ActionKind Kind) { public override string ToString() => new ActionStep { Kind = Kind }.Label; }
    private sealed record KeyItem(int Code) { public override string ToString() => ActionStep.KeyLabel(Code); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) Icon?.Dispose();
        base.Dispose(disposing);
    }
}
