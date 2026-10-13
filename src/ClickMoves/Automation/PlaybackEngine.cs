using System.Diagnostics;

namespace ClickMoves.Automation;

public interface IActionSender
{
    void Send(ActionStep step);
    void ReleaseAll();
}
public sealed class PlaybackEngine : IDisposable
{
    private readonly IActionSender sender;
    private readonly object gate = new();
    private readonly AutoResetEvent wake = new(false);
    private readonly Dictionary<string, ActionStep> held = [];
    private Thread? thread;
    private bool cancelled, paused, disposed;
    private long count;
    private string? error;
    public PlaybackEngine(IActionSender sender) => this.sender = sender;
    public long Count => Interlocked.Read(ref count);
    public bool IsRunning { get { lock (gate) return thread?.IsAlive == true; } }
    public bool IsPaused { get { lock (gate) return paused && thread?.IsAlive == true; } }
    public string? TakeError() => Interlocked.Exchange(ref error, null);
    public void Start(AutomationDocument document, decimal speed, int repeats)
    {
        document.Validate();
        if (document.Steps.Count == 0) throw new ArgumentException("Añade acciones antes de reproducir.");
        if (speed is < 0.1m or > 10m || repeats is < 1 or > 1000) throw new ArgumentException("Velocidad: 0,1–10×. Ciclos: 1–1000.");
        var steps = document.Steps.ToArray();
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (thread?.IsAlive == true) throw new InvalidOperationException("Ya hay una reproducción activa.");
            cancelled = paused = false; count = 0; error = null; held.Clear();
            thread = new Thread(() => Run(steps, speed, repeats)) { IsBackground = true, Name = "Reproducción" };
            thread.Start();
        }
    }
    private void Run(ActionStep[] steps, decimal speed, int repeats)
    {
        try
        {
            for (int cycle = 0; cycle < repeats; cycle++)
            {
                foreach (var step in steps)
                {
                    if (!Delay(step.DelayMs / (double)speed)) return;
                    lock (gate)
                    {
                        while (paused && !cancelled) Monitor.Wait(gate);
                        if (cancelled) return;
                        if (step.Kind != ActionKind.Wait) sender.Send(step);
                        Track(step);
                        Interlocked.Increment(ref count);
                    }
                    if ((Count & 255) == 0) Thread.Sleep(1);
                }
                // A cycle never leaves inputs pressed into the next one.
                lock (gate) { sender.ReleaseAll(); held.Clear(); }
            }
        }
        catch (Exception ex) { Interlocked.Exchange(ref error, ex.Message); }
        finally
        {
            lock (gate)
            {
                try { sender.ReleaseAll(); } catch (Exception ex) { Interlocked.Exchange(ref error, ex.Message); }
                held.Clear(); paused = false;
            }
        }
    }
    private bool Delay(double remaining)
    {
        while (true)
        {
            bool wasPaused;
            lock (gate) { if (cancelled) return false; wasPaused = paused; }
            if (!wasPaused && remaining <= 0) return true;
            long before = Stopwatch.GetTimestamp();
            wake.WaitOne(wasPaused ? Timeout.Infinite : (int)Math.Clamp(Math.Ceiling(remaining), 1, int.MaxValue));
            if (!wasPaused) remaining -= Stopwatch.GetElapsedTime(before).TotalMilliseconds;
        }
    }
    private void Track(ActionStep step)
    {
        string? key = step.Kind switch
        {
            ActionKind.KeyDown or ActionKind.KeyUp => "K" + step.KeyCode,
            ActionKind.LeftDown or ActionKind.LeftUp or ActionKind.LeftClick => "L",
            ActionKind.RightDown or ActionKind.RightUp or ActionKind.RightClick => "R", _ => null
        };
        if (key == null) return;
        if (step.Kind is ActionKind.KeyDown or ActionKind.LeftDown or ActionKind.RightDown) held[key] = step;
        else held.Remove(key);
    }
    public void Pause()
    {
        lock (gate)
        {
            if (thread?.IsAlive != true || paused) return;
            sender.ReleaseAll(); paused = true; wake.Set();
        }
    }
    public void Resume()
    {
        lock (gate)
        {
            if (!paused) return;
            foreach (var step in held.Values) sender.Send(step);
            paused = false; Monitor.PulseAll(gate); wake.Set();
        }
    }
    public void Stop()
    {
        Thread? active;
        lock (gate) { cancelled = true; paused = false; active = thread; Monitor.PulseAll(gate); if (!disposed) wake.Set(); }
        if (active != null && active != Thread.CurrentThread) active.Join();
    }
    public void Dispose()
    {
        Stop();
        lock (gate) { if (!disposed) { disposed = true; wake.Dispose(); } }
    }
}
