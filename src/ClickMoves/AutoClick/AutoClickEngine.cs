using System.Diagnostics;
using ClickMoves.Configuration;

namespace ClickMoves.AutoClick;

public sealed class AutoClickEngine : IDisposable
{
    private readonly IClickSender sender;
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Thread? worker;
    private long count;
    private string? error;
    private bool disposed;
    public AutoClickEngine(IClickSender sender) => this.sender = sender;
    public long Count => Interlocked.Read(ref count);
    public bool IsRunning { get { lock (gate) return worker?.IsAlive == true; } }
    public string? TakeError() => Interlocked.Exchange(ref error, null);

    public bool Start(AppSettings settings)
    {
        settings.Validate();
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (worker?.IsAlive == true) return false;
            cancellation?.Dispose();
            cancellation = new();
            var token = cancellation.Token;
            error = null;
            worker = new Thread(() => Run(settings, token)) { IsBackground = true, Name = "AutoClick" };
            worker.Start();
            return true;
        }
    }

    private void Run(AppSettings settings, CancellationToken token)
    {
        double interval = Stopwatch.Frequency / (double)settings.ClicksPerSecond;
        double next = Stopwatch.GetTimestamp();
        try
        {
            while (!token.IsCancellationRequested)
            {
                double remaining = next - Stopwatch.GetTimestamp();
                if (remaining > 0)
                {
                    int ms = Math.Max(1, (int)Math.Ceiling(remaining * 1000 / Stopwatch.Frequency));
                    if (token.WaitHandle.WaitOne(ms)) break;
                    continue;
                }
                lock (gate)
                {
                    if (token.IsCancellationRequested) break;
                    sender.Send(settings.Button);
                    Interlocked.Increment(ref count);
                }
                // Do not send catch-up bursts after a delay.
                next = Math.Max(next + interval, Stopwatch.GetTimestamp());
            }
        }
        catch (Exception ex) { Interlocked.Exchange(ref error, ex.Message); }
    }

    public void Stop()
    {
        Thread? thread;
        lock (gate) { cancellation?.Cancel(); thread = worker; }
        // SendInput is synchronous; join guarantees no additional send after Stop returns.
        if (thread != null && thread != Thread.CurrentThread) thread.Join();
    }
    public void ResetCount()
    {
        lock (gate)
        {
            if (worker?.IsAlive == true) throw new InvalidOperationException("Detén AutoClick antes de reiniciar el contador.");
            Interlocked.Exchange(ref count, 0);
        }
    }
    public void Dispose()
    {
        Stop();
        lock (gate)
        {
            cancellation?.Dispose();
            cancellation = null;
            disposed = true;
        }
    }
}
