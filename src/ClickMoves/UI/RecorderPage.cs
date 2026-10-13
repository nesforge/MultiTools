using System.Text.Json;
using ClickMoves.Automation;
using ClickMoves.Configuration;
using ClickMoves.Platform;
using ClickMoves.Storage;

namespace ClickMoves.UI;

public sealed class RecorderPage : UserControl
{
    private readonly WindowsRecorder recorder;
    private readonly PlaybackEngine player;
    private readonly AutomationCoordinator coordinator;
    private readonly AutomationStore storage;
    private readonly TextBox name = new() { Name = "RecordingName", Text = "Grabación", MaxLength = 80, Width = 200, AccessibleName = "Nombre de la grabación" };
    private readonly CheckBox moves = new() { Name = "RecordMoves", Text = "Incluir movimientos", AutoSize = true };
    private readonly Button record = ToolStyle.Button("Grabar", "RecordStart"), pause = ToolStyle.Button("Pausar grabación", "RecordPause"), stop = ToolStyle.Button("Detener grabación", "RecordStop");
    private readonly Button save = ToolStyle.Button("Guardar", "RecordSave"), load = ToolStyle.Button("Cargar", "RecordLoad");
    private readonly Label state = new() { Name = "RecordingStatus", AutoSize = true };
    private readonly Label notice = new() { AutoSize = true, ForeColor = ToolStyle.Blue };
    private readonly StepsPreview preview = new() { Name = "RecordingPreview" };
    private readonly PlaybackControls playback;
    private AutomationDocument current = new() { Kind = DocumentKind.Recording, Name = "Grabación" };
    public RecorderPage(WindowsRecorder recorder, PlaybackEngine player, AutomationCoordinator coordinator, AutomationStore storage, int emergency, ToolPreferences preferences, Action<bool, decimal, int> saveOptions)
    {
        this.recorder = recorder; this.player = player; this.coordinator = coordinator; this.storage = storage;
        Dock = DockStyle.Fill; moves.Checked = preferences.IncludeMoves; ToolStyle.Primary(record);
        var page = ToolStyle.Page();
        ToolStyle.Add(page, ToolStyle.Row(ToolStyle.Label("Nombre"), name, moves));
        ToolStyle.Add(page, ToolStyle.Row(record, pause, stop, state));
        ToolStyle.Add(page, ToolStyle.Row(save, load, notice));
        ToolStyle.Add(page, preview, true);
        playback = new("Grabador", player, coordinator, Document, emergency, preferences.RecordingSpeed, preferences.RecordingCycles);
        ToolStyle.Add(page, playback); Controls.Add(page);
        record.Click += (_, _) =>
        {
            if (!coordinator.TryBegin("Grabación", StopRecording)) { notice.Text = "Detén la herramienta activa primero."; return; }
            try { recorder.Start(moves.Checked); notice.Text = "Graba fuera de MiniTools. Evita datos privados."; preview.ShowSteps([]); }
            catch (Exception) { coordinator.End("Grabación"); notice.Text = "No se pudo iniciar la grabación."; }
            RefreshState();
        };
        pause.Click += (_, _) => { if (recorder.Buffer.State == RecordingState.Paused) recorder.Resume(); else recorder.Pause(); RefreshState(); };
        stop.Click += (_, _) => StopRecording();
        save.Click += (_, _) => FileAction(false);
        load.Click += (_, _) => FileAction(true);
        decimal lastSpeed = preferences.RecordingSpeed; int lastCycles = preferences.RecordingCycles;
        moves.CheckedChanged += (_, _) => saveOptions(moves.Checked, lastSpeed, lastCycles);
        playback.OptionsChanged += (speed, cycles) => { lastSpeed = speed; lastCycles = cycles; saveOptions(moves.Checked, speed, cycles); };
        RefreshState();
    }
    private AutomationDocument Document() => current with { Name = name.Text.Trim() };
    public void StopRecording()
    {
        recorder.Stop(); current = recorder.Buffer.Snapshot(string.IsNullOrWhiteSpace(name.Text) ? "Grabación" : name.Text.Trim());
        coordinator.End("Grabación"); preview.ShowSteps(current.Steps); RefreshState();
    }
    private void FileAction(bool loading)
    {
        try
        {
            using FileDialog dialog = loading ? new OpenFileDialog() : new SaveFileDialog { FileName = "grabacion.json", DefaultExt = "json", AddExtension = true };
            dialog.Filter = "Grabaciones JSON|*.json";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (loading)
            {
                var value = storage.Load(dialog.FileName);
                if (value.Kind != DocumentKind.Recording) throw new ArgumentException("El archivo no es una grabación.");
                current = value; name.Text = value.Name; preview.ShowSteps(value.Steps);
            }
            else storage.Save(dialog.FileName, Document());
            notice.Text = loading ? "Grabación cargada" : "Grabación guardada";
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or IOException or UnauthorizedAccessException) { notice.Text = "Archivo inválido o no accesible."; }
    }
    public void RefreshState()
    {
        if (coordinator.Owner == "Grabación" && recorder.Buffer.State == RecordingState.Ready)
        { StopRecording(); notice.Text = recorder.Buffer.Error ?? "Grabación detenida"; }
        bool recording = coordinator.Owner == "Grabación";
        bool idle = coordinator.Owner == null;
        record.Enabled = moves.Enabled = name.Enabled = save.Enabled = load.Enabled = idle;
        pause.Enabled = stop.Enabled = recording;
        pause.Text = recorder.Buffer.State == RecordingState.Paused ? "Reanudar grabación" : "Pausar grabación";
        bool playing = coordinator.Owner == "Grabador";
        state.Text = recording ? recorder.Buffer.State == RecordingState.Paused ? "Pausado" : "Grabando" : playing ? player.IsPaused ? "Pausado" : "Reproduciendo" : "Listo";
        if (recording) state.Text += $" · {recorder.Buffer.Count:N0} acciones registradas";
        else if (playing) state.Text += $" · {player.Count:N0} pasos procesados";
        else state.Text += $" · {current.Steps.Count:N0} acciones registradas";
        ToolStyle.State(state, recording || playing);
        playback.RefreshState();
    }
}
