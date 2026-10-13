using ClickMoves.Automation;

namespace ClickMoves.UI;

public sealed class PlaybackControls : UserControl
{
    private readonly PlaybackEngine player;
    private readonly AutomationCoordinator coordinator;
    private readonly Func<AutomationDocument> document;
    private readonly string owner;
    private readonly int emergency;
    private long lastCount;
    private readonly Button play, pause, stop;
    private readonly Label state = new() { AutoSize = true }, counter = new() { AutoSize = true };
    private readonly NumericUpDown speed = new() { Minimum = 0.1m, Maximum = 10, DecimalPlaces = 1, Increment = 0.1m, Width = 75 };
    private readonly NumericUpDown cycles = new() { Minimum = 1, Maximum = 1000, Width = 65 };
    private readonly Label error = new() { AutoSize = true, MaximumSize = new(650, 0), ForeColor = ToolStyle.Blue };
    public event Action<decimal, int>? OptionsChanged;
    public PlaybackControls(string owner, PlaybackEngine player, AutomationCoordinator coordinator, Func<AutomationDocument> document, int emergency, decimal initialSpeed, int initialCycles)
    {
        this.owner = owner; this.player = player; this.coordinator = coordinator; this.document = document; this.emergency = emergency;
        Dock = DockStyle.Top; AutoSize = false; Height = 150;
        play = ToolStyle.Button("Reproducir", owner + "Play"); pause = ToolStyle.Button("Pausar", owner + "Pause"); stop = ToolStyle.Button("Detener", owner + "Stop");
        speed.Name = owner + "Speed"; speed.AccessibleName = "Velocidad de reproducción";
        cycles.Name = owner + "Cycles"; cycles.AccessibleName = "Número de ciclos";
        speed.Value = initialSpeed; cycles.Value = initialCycles;
        var layout = ToolStyle.Page(); layout.Padding = new(0); layout.Dock = DockStyle.Top; layout.AutoSize = true;
        ToolStyle.Primary(play);
        ToolStyle.Add(layout, ToolStyle.Row(ToolStyle.Label("Velocidad"), speed, ToolStyle.Label("×"), ToolStyle.Label("Ciclos"), cycles));
        ToolStyle.Add(layout, ToolStyle.Row(play, pause, stop, state, counter));
        ToolStyle.Add(layout, error); Controls.Add(layout);
        play.Click += (_, _) =>
        {
            try
            {
                var value = document(); value.Validate();
                if (value.Steps.Any(s => s.KeyCode == emergency)) throw new ArgumentException($"La tecla {(Keys)emergency} está reservada para detener.");
                if (!coordinator.TryBegin(owner, Stop)) throw new ArgumentException("Detén la herramienta activa primero.");
                player.Start(value, speed.Value, (int)cycles.Value); lastCount = 0; error.Text = "";
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { coordinator.End(owner); error.Text = ex.Message; }
            RefreshState();
        };
        pause.Click += (_, _) =>
        {
            try { if (player.IsPaused) player.Resume(); else player.Pause(); }
            catch (Exception) { Stop(); error.Text = "Windows no pudo reanudar la secuencia."; }
            RefreshState();
        };
        stop.Click += (_, _) => Stop();
        speed.ValueChanged += (_, _) => OptionsChanged?.Invoke(speed.Value, (int)cycles.Value);
        cycles.ValueChanged += (_, _) => OptionsChanged?.Invoke(speed.Value, (int)cycles.Value);
        RefreshState();
    }
    public void Stop() { if (coordinator.Owner == owner) { player.Stop(); lastCount = player.Count; coordinator.End(owner); } RefreshState(); }
    public void RefreshState()
    {
        bool mine = coordinator.Owner == owner;
        if (mine) lastCount = player.Count;
        if (mine && !player.IsRunning)
        {
            string? failure = player.TakeError(); if (failure != null) error.Text = "Reproducción detenida: " + failure;
            coordinator.End(owner); mine = false;
        }
        bool idle = coordinator.Owner == null;
        play.Enabled = speed.Enabled = cycles.Enabled = idle;
        pause.Enabled = stop.Enabled = mine;
        pause.Text = player.IsPaused && mine ? "Reanudar" : "Pausar";
        state.Text = mine ? player.IsPaused ? "Pausado" : "Reproduciendo" : "Listo";
        ToolStyle.State(state, mine);
        counter.Text = $"{lastCount:N0} pasos procesados";
    }
}
