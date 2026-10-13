using System.Diagnostics;

namespace ClickMoves.Automation;

public enum RecordingState { Ready, Recording, Paused }
public sealed class RecordingBuffer
{
    private readonly List<ActionStep> steps = [];
    private readonly Dictionary<string, ActionStep> held = [];
    private readonly Stopwatch clock = new();
    private long last, lastMove = -16;
    public bool IncludeMoves { get; private set; }
    public RecordingState State { get; private set; }
    public int Count => steps.Count;
    public string? Error { get; private set; }
    public void Begin(bool includeMoves)
    {
        steps.Clear(); held.Clear(); last = 0; lastMove = -16; Error = null;
        IncludeMoves = includeMoves; State = RecordingState.Recording; clock.Restart();
    }
    public void Observe(ActionStep step)
    {
        if (State != RecordingState.Recording) return;
        long now = clock.ElapsedMilliseconds;
        if (step.Kind == ActionKind.MouseMove && (!IncludeMoves || now - lastMove < 16)) return;
        if (steps.Count >= AutomationDocument.MaxSteps - 256)
        { Error = "Límite de acciones alcanzado."; Stop(); return; }
        string? id = step.Kind switch
        {
            ActionKind.KeyDown or ActionKind.KeyUp => "K" + step.KeyCode,
            ActionKind.LeftDown or ActionKind.LeftUp => "L", ActionKind.RightDown or ActionKind.RightUp => "R", _ => null
        };
        if (step.Kind is ActionKind.KeyUp or ActionKind.LeftUp or ActionKind.RightUp)
        { if (id == null || !held.Remove(id)) return; }
        else if (id != null) held[id] = step;
        int gap = (int)Math.Min(now - last, 3_600_000);
        // Split unusually long gaps rather than losing their elapsed time.
        long extra = now - last - gap;
        while (extra > 0 && steps.Count < AutomationDocument.MaxSteps - 256)
        { int wait = (int)Math.Min(extra, 3_600_000); steps.Add(new() { Kind = ActionKind.Wait, DelayMs = wait }); extra -= wait; }
        steps.Add(step with { DelayMs = gap }); last = now;
        if (step.Kind == ActionKind.MouseMove) lastMove = now;
    }
    private void Balance()
    {
        long elapsed = clock.ElapsedMilliseconds - last;
        while (elapsed > 3_600_000 && steps.Count < AutomationDocument.MaxSteps - 256)
        { steps.Add(new() { Kind = ActionKind.Wait, DelayMs = 3_600_000 }); elapsed -= 3_600_000; }
        int gap = (int)Math.Min(elapsed, 3_600_000);
        if (held.Count == 0 && steps.Count > 0 && gap > 0) steps.Add(new() { Kind = ActionKind.Wait, DelayMs = gap });
        foreach (var step in held.Values)
        {
            steps.Add(step with { DelayMs = gap, Kind = step.Kind switch
            { ActionKind.KeyDown => ActionKind.KeyUp, ActionKind.LeftDown => ActionKind.LeftUp, _ => ActionKind.RightUp } });
            gap = 0;
        }
        last = clock.ElapsedMilliseconds;
        held.Clear();
    }
    public void Pause() { if (State == RecordingState.Recording) { clock.Stop(); Balance(); State = RecordingState.Paused; } }
    public void Resume() { if (State == RecordingState.Paused) { State = RecordingState.Recording; clock.Start(); } }
    public void Stop() { clock.Stop(); Balance(); State = RecordingState.Ready; }
    public AutomationDocument Snapshot(string name) => new() { Kind = DocumentKind.Recording, Name = name, Steps = [.. steps] };
}
