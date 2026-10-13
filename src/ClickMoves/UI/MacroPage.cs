using System.Text.Json;
using ClickMoves.Automation;
using ClickMoves.Configuration;
using ClickMoves.Storage;

namespace ClickMoves.UI;

public sealed class MacroPage : UserControl
{
    private readonly AutomationStore storage;
    private readonly AutomationCoordinator coordinator;
    private readonly Action<Guid?> remember;
    private readonly TextBox name = new() { Name = "MacroName", Text = "Nueva macro", MaxLength = 80, Width = 190, AccessibleName = "Nombre de la macro" };
    private readonly ComboBox library = new() { Name = "MacroLibrary", DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, AccessibleName = "Macros guardadas" };
    private readonly StepsPreview preview = new() { Name = "MacroPreview" };
    private readonly Label notice = new() { Name = "MacroNotice", AutoSize = true, ForeColor = ToolStyle.Blue };
    private readonly PlaybackControls playback;
    private readonly List<Button> editButtons = [];
    private List<ActionStep> steps = [];
    private Guid? currentId;
    private bool loading;
    public MacroPage(PlaybackEngine player, AutomationCoordinator coordinator, AutomationStore storage, int emergency, ToolPreferences preferences, Action<decimal, int> saveOptions, Action<Guid?> remember)
    {
        this.storage = storage; this.coordinator = coordinator; this.remember = remember;
        Dock = DockStyle.Fill;
        var page = ToolStyle.Page();
        Button Button(string label, string id, Action action)
        {
            var button = ToolStyle.Button(label, id); editButtons.Add(button);
            button.Click += (_, _) => { try { action(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException) { notice.Text = "No se pudo completar: " + (ex is ArgumentException ? ex.Message : "archivo inválido o no accesible."); } };
            return button;
        }
        ToolStyle.Add(page, ToolStyle.Row(ToolStyle.Label("Nombre"), name, ToolStyle.Label("Guardadas"), library));
        ToolStyle.Add(page, ToolStyle.Row(Button("Nueva", "MacroNew", () => { steps = []; currentId = null; library.SelectedIndex = -1; name.Text = "Nueva macro"; preview.ShowSteps(steps); notice.Text = "Añade pasos para empezar."; }),
            Button("Guardar", "MacroSave", Save), Button("Importar", "MacroImport", () => FileAction(true)), Button("Exportar", "MacroExport", () => FileAction(false))));
        ToolStyle.Add(page, ToolStyle.Row(Button("Agregar", "MacroAdd", () => Edit(true)), Button("Editar", "MacroEdit", () => Edit(false)),
            Button("Duplicar", "MacroDuplicate", Duplicate), Button("Eliminar", "MacroDelete", Delete), Button("↑", "MacroUp", () => MoveStep(-1)), Button("↓", "MacroDown", () => MoveStep(1))));
        editButtons[^2].AccessibleName = "Subir paso"; editButtons[^1].AccessibleName = "Bajar paso";
        ToolStyle.Add(page, preview, true); ToolStyle.Add(page, notice);
        playback = new("Macros", player, coordinator, Document, emergency, preferences.MacroSpeed, preferences.MacroCycles);
        playback.OptionsChanged += saveOptions; ToolStyle.Add(page, playback); Controls.Add(page);
        preview.CellDoubleClick += (_, _) => { if (coordinator.Owner == null) Edit(false); };
        library.SelectedIndexChanged += (_, _) =>
        {
            if (loading || library.SelectedItem is not LibraryItem item) return;
            currentId = item.Id; steps = [.. item.Document.Steps]; name.Text = item.Document.Name; preview.ShowSteps(steps); notice.Text = "Macro cargada"; remember(currentId);
        };
        Reload(preferences.LastMacro); RefreshState();
    }
    private AutomationDocument Document() => new() { Kind = DocumentKind.Macro, Name = name.Text.Trim(), Steps = [.. steps] };
    private void Edit(bool adding)
    {
        int index = preview.SelectedStep;
        if (!adding && index < 0) { notice.Text = "Selecciona un paso."; return; }
        if (adding && steps.Count >= AutomationDocument.MaxSteps) { notice.Text = "Límite de pasos alcanzado."; return; }
        using var editor = new StepEditor(adding ? null : steps[index]);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        var value = editor.Result();
        if (adding) { steps.Add(value); index = steps.Count - 1; } else steps[index] = value;
        preview.ShowSteps(steps, index); notice.Text = "";
    }
    private void Duplicate()
    {
        int index = preview.SelectedStep;
        if (index < 0 || steps.Count >= AutomationDocument.MaxSteps) return;
        steps.Insert(index + 1, steps[index] with { }); preview.ShowSteps(steps, index + 1);
    }
    private void Delete()
    { int i = preview.SelectedStep; if (i < 0) return; steps.RemoveAt(i); preview.ShowSteps(steps, Math.Min(i, steps.Count - 1)); }
    private void MoveStep(int direction)
    {
        int from = preview.SelectedStep, to = from + direction;
        if (from < 0 || to < 0 || to >= steps.Count) return;
        (steps[from], steps[to]) = (steps[to], steps[from]); preview.ShowSteps(steps, to);
    }
    private void Save()
    {
        currentId = storage.SaveMacro(Document(), currentId); Reload(currentId); remember(currentId); notice.Text = "Macro guardada";
    }
    private void Reload(Guid? select)
    {
        var items = storage.ListMacros(out bool skipped); loading = true; library.Items.Clear();
        foreach (var item in items) library.Items.Add(new LibraryItem(item.Id, item.Document));
        loading = false;
        var selected = library.Items.Cast<LibraryItem>().FirstOrDefault(x => x.Id == select);
        if (selected != null) library.SelectedItem = selected;
        if (skipped) notice.Text = "Se ignoraron archivos de biblioteca inválidos.";
    }
    private void FileAction(bool importing)
    {
        using FileDialog dialog = importing ? new OpenFileDialog() : new SaveFileDialog { FileName = "macro.json", DefaultExt = "json", AddExtension = true };
        dialog.Filter = "Macros JSON|*.json";
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (importing)
        {
            var value = storage.Load(dialog.FileName);
            if (value.Kind != DocumentKind.Macro) throw new ArgumentException("El archivo no es una macro.");
            steps = [.. value.Steps]; currentId = null; library.SelectedIndex = -1; name.Text = value.Name; preview.ShowSteps(steps); notice.Text = "Macro importada. Pulsa Guardar para añadirla a la biblioteca.";
        }
        else { storage.Save(dialog.FileName, Document()); notice.Text = "Macro exportada"; }
    }
    public void RefreshState()
    {
        bool idle = coordinator.Owner == null;
        name.Enabled = library.Enabled = idle;
        foreach (var button in editButtons) button.Enabled = idle;
        playback.RefreshState();
    }
    private sealed record LibraryItem(Guid Id, AutomationDocument Document) { public override string ToString() => Document.Name; }
}
