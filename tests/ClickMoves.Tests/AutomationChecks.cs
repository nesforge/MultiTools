using System.Diagnostics;
using ClickMoves.Automation;
using ClickMoves.Storage;

internal static class AutomationChecks
{
    public static void Core()
    {
        string folder = Path.Combine(Environment.CurrentDirectory, "TestResults", Guid.NewGuid().ToString("N"));
        var storage = new AutomationStore(Path.Combine(folder, "macros"));
        var preferences = new ClickMoves.Configuration.ToolPreferencesStore(Path.Combine(folder, "preferences.json"));
        preferences.Save(new() { IncludeMoves = false, RecordingSpeed = 2, MacroSpeed = 3, MacroCycles = 4 });
        Assert(preferences.Load().RecordingSpeed == 2 && preferences.Load().MacroSpeed == 3 && preferences.Load().MacroCycles == 4 && !preferences.Load().IncludeMoves, "Preferencias independientes persistidas");
        var document = new AutomationDocument { Kind = DocumentKind.Macro, Name = "Prueba", Steps = [
            new() { Kind = ActionKind.LeftClick, DelayMs = 80 }, new() { Kind = ActionKind.RightClick, DelayMs = 80 },
            new() { Kind = ActionKind.KeyDown, KeyCode = (int)Keys.A }, new() { Kind = ActionKind.KeyUp, KeyCode = (int)Keys.A },
            new() { Kind = ActionKind.Wait, DelayMs = 80 } ] };
        Guid id = storage.SaveMacro(document);
        var loaded = storage.ListMacros(out bool skipped);
        Assert(!skipped && loaded.Count == 1 && loaded[0].Id == id && loaded[0].Document.Steps.SequenceEqual(document.Steps), "Guardar/cargar macro");
        storage.SaveMacro(document with { Name = "Otra" });
        Assert(storage.ListMacros(out _).Count == 2, "Varias macros");
        string file = Path.Combine(folder, "import.json");
        foreach (string json in new[] { "{}", "null", "{bad}", "{\"version\":1,\"version\":1}", "{\"version\":2,\"kind\":\"Macro\",\"name\":\"X\",\"steps\":[]}", "{\"version\":1,\"kind\":\"Macro\",\"name\":\"X\",\"steps\":[],\"text\":\"prohibido\"}", "{\"version\":1,\"kind\":\"Macro\",\"name\":\"X\",\"steps\":[{\"delayMs\":0}]}" })
        { File.WriteAllText(file, json); Throws(() => storage.Load(file)); }
        foreach (var invalid in new[] { new ActionStep { Kind = ActionKind.Wait, DelayMs = -1 }, new() { Kind = ActionKind.KeyDown }, new() { Kind = ActionKind.MouseMove }, new() { Kind = ActionKind.LeftClick, KeyCode = 65 }, new() { Kind = (ActionKind)900 } }) Throws(invalid.Validate);
        using (var tooLarge = File.Create(file)) tooLarge.SetLength(AutomationStore.MaxFileBytes + 1);
        Throws(() => storage.Load(file));
        string export = Path.Combine(folder, "export.json"); storage.Save(export, document);
        Assert(storage.Load(export).Steps.SequenceEqual(document.Steps), "Exportar/importar validado");
        var buffer = new RecordingBuffer(); buffer.Begin(false);
        buffer.Observe(new() { Kind = ActionKind.MouseMove, X = 10, Y = 10 }); Assert(buffer.Count == 0, "Ignorar movimientos");
        buffer.Observe(new() { Kind = ActionKind.KeyDown, KeyCode = 65 }); Thread.Sleep(60); buffer.Pause();
        var snapshot = buffer.Snapshot("Grabación"); Assert(snapshot.Steps[^1].Kind == ActionKind.KeyUp && snapshot.Steps[^1].DelayMs >= 40, "Duración de tecla conservada al pausar");
        Thread.Sleep(120); buffer.Resume(); buffer.Observe(new() { Kind = ActionKind.KeyDown, KeyCode = 66 }); buffer.Stop();
        Assert(buffer.Snapshot("Grabación").Steps.Sum(s => s.DelayMs) < 180, "Tiempo pausado no grabado");
        var sink = new Sink();
        using var player = new PlaybackEngine(sink);
        var sw = Stopwatch.StartNew();
        player.Start(document, 2, 2);
        while (player.IsRunning && sw.ElapsedMilliseconds < 2000) Thread.Sleep(5);
        Assert(player.Count == 10 && sink.Steps.Count == 8, "Repetición y pasos/esperas");
        Assert(sw.ElapsedMilliseconds is >= 200 and < 1500, "Esperas escaladas por velocidad");
        Assert(sink.Steps.Take(4).Select(s => s.Kind).SequenceEqual(new[] { ActionKind.LeftClick, ActionKind.RightClick, ActionKind.KeyDown, ActionKind.KeyUp }), "Orden");
        player.Start(new() { Steps = [new() { Kind = ActionKind.KeyDown, KeyCode = 65 }, new() { Kind = ActionKind.Wait, DelayMs = 300 }, new() { Kind = ActionKind.KeyUp, KeyCode = 65 }] }, 1, 1);
        Thread.Sleep(50); player.Pause(); long paused = player.Count;
        Thread.Sleep(400); Assert(player.Count == paused && player.IsPaused, "Pausa congela avance");
        player.Resume(); Thread.Sleep(50); Assert(player.Count == paused, "Pausa conserva espera pendiente");
        player.Stop(); Assert(!player.IsRunning && sink.Releases > 0, "Parada libera entradas");
        player.Start(new() { Steps = [new() { Kind = ActionKind.Wait, DelayMs = 30000 }] }, 1, 1);
        sw.Restart(); player.Stop(); Assert(sw.ElapsedMilliseconds < 250, "Parada interrumpe espera larga");
        var coordinator = new AutomationCoordinator();
        bool stopped = false;
        Assert(coordinator.TryBegin("A", () => stopped = true) && !coordinator.TryBegin("B", () => { }), "Exclusión central");
        coordinator.StopAll(); Assert(stopped && coordinator.Owner == null, "Parada central");
        Assert(coordinator.TryBegin("Reproducción", () => { Assert(coordinator.Owner == "Reproducción", "Propietario permanece durante parada"); player.Stop(); }), "Reserva de reproducción");
        coordinator.StopAll();
    }
    internal static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Faltó validación"); }
    private sealed class Sink : IActionSender
    {
        public readonly List<ActionStep> Steps = [];
        public int Releases;
        public void Send(ActionStep step) => Steps.Add(step);
        public void ReleaseAll() => Releases++;
    }
}
