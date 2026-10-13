using System.Diagnostics;
using ClickMoves.AutoClick;
using ClickMoves.Configuration;

internal static class CoreAutoClickChecks
{
    public static void Run()
    {
        foreach (decimal cps in new[] { 0.1m, 1m, 10m, 100m })
        foreach (ClickButton button in Enum.GetValues<ClickButton>())
        {
            var sender = new Sink();
            using var engine = new AutoClickEngine(sender);
            engine.Start(new() { ClicksPerSecond = cps, Button = button });
            AutomationChecks.Assert(!engine.Start(new()), "Inicio duplicado bloqueado");
            var watch = Stopwatch.StartNew();
            while (engine.Count == 0 && watch.ElapsedMilliseconds < 1000) Thread.Sleep(5);
            watch.Restart(); engine.Stop();
            long count = engine.Count;
            AutomationChecks.Assert(watch.ElapsedMilliseconds < 250 && count > 0 && count == sender.Count && sender.Last == button, "Parada, contador y lado a " + cps);
            Thread.Sleep(30);
            AutomationChecks.Assert(engine.Count == count, "Sin envíos después de parar");
            engine.ResetCount();
            AutomationChecks.Assert(engine.Count == 0, "Reinicio del contador");
        }
    }
    private sealed class Sink : IClickSender
    {
        public long Count;
        public ClickButton Last;
        public void Send(ClickButton button) { Last = button; Interlocked.Increment(ref Count); }
    }
}
