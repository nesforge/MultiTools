using System.Diagnostics;
using System.Runtime.InteropServices;
using ClickMoves.Automation;
using ClickMoves.Platform;
using ClickMoves.UI;

internal static class NativeAutomationChecks
{
    public static void Target()
    {
        ApplicationConfiguration.Initialize();
        using var target = new TargetForm();
        Application.Run(target);
    }
    public static async Task Run(Form host)
    {
        // Keep the host out of the destination's rectangle: its controls are excluded by the recorder.
        host.Location = new(450, 40);
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Normal };
        start.ArgumentList.Add("--native-target");
        using var process = Process.Start(start)!;
        try
        {
            IntPtr target = IntPtr.Zero;
            for (int i = 0; i < 60 && target == IntPtr.Zero; i++) { await Task.Delay(100); target = FindWindow(null, "Destino externo de automatización"); }
            AutomationChecks.Assert(target != IntPtr.Zero, "Destino externo");
            SendMessage(target, 0, IntPtr.Zero, IntPtr.Zero);
            for (int attempt = 0; attempt < 10 && GetForegroundWindow() != target; attempt++)
            { NativeTestFocus.Give(target); await Task.Delay(30); }
            AutomationChecks.Assert(GetForegroundWindow() == target && WindowFromPoint(new(190, 170)) == target,
                $"Destino listo y libre de otras ventanas: foco {GetForegroundWindow()}/{target}; mouse {WindowFromPoint(new(190, 170))}");
            var input = new WindowsActionSender();
            using var recorder = new WindowsRecorder((int)Keys.F9, allowInjected: true, onlyTaggedForTests: true);
            recorder.Start(true);
            input.Send(new() { Kind = ActionKind.MouseMove, X = 180, Y = 160 }); await Task.Delay(40);
            input.Send(new() { Kind = ActionKind.LeftClick, X = 190, Y = 170 }); await Task.Delay(40);
            input.Send(new() { Kind = ActionKind.RightClick, X = 200, Y = 180 }); await Task.Delay(40);
            input.Send(new() { Kind = ActionKind.KeyDown, KeyCode = 65 }); await Task.Delay(30);
            input.Send(new() { Kind = ActionKind.KeyUp, KeyCode = 65 }); await Task.Delay(40);
            recorder.Pause(); int paused = recorder.Buffer.Count;
            input.Send(new() { Kind = ActionKind.MouseMove, X = 210, Y = 190 }); await Task.Delay(30);
            AutomationChecks.Assert(recorder.Buffer.Count == paused, "Pausa de grabación");
            recorder.Resume(); input.Send(new() { Kind = ActionKind.LeftClick, X = 180, Y = 160 }); await Task.Delay(40);
            recorder.Stop(); var document = recorder.Buffer.Snapshot("Verificación");
            foreach (var kind in new[] { ActionKind.MouseMove, ActionKind.LeftDown, ActionKind.LeftUp, ActionKind.RightDown, ActionKind.RightUp, ActionKind.KeyDown, ActionKind.KeyUp })
                AutomationChecks.Assert(document.Steps.Any(s => s.Kind == kind), "Grabación: " + kind);
            int before = ReadCount(target, 0);
            using var player = new PlaybackEngine(new WindowsActionSender());
            NativeTestFocus.Give(target);
            player.Start(document, 2, 2);
            for (int i = 0; i < 100 && player.IsRunning; i++) await Task.Delay(20);
            AutomationChecks.Assert(!player.IsRunning && player.TakeError() == null && player.Count == document.Steps.Count * 2, "Reproducción grabada y ciclos");
            // SendInput completes before the destination necessarily consumes its input queue.
            await Task.Delay(120);
            int receivedLeft = ReadCount(target, 0), receivedRight = ReadCount(target, 1), receivedKeys = ReadCount(target, 2);
            AutomationChecks.Assert(receivedLeft >= before + 4 && receivedRight >= 3 && receivedKeys >= 3,
                $"Destino recibió clics y teclas de prueba: L={receivedLeft}, base={before}, R={receivedRight}, K={receivedKeys}, foco={GetForegroundWindow()}/{target}");
            recorder.Start(false);
            input.Send(new() { Kind = ActionKind.MouseMove, X = 200, Y = 160 }); await Task.Delay(25);
            input.Send(new() { Kind = ActionKind.LeftClick, X = 190, Y = 170 }); await Task.Delay(25);
            input.Send(new() { Kind = ActionKind.KeyDown, KeyCode = 66 }); input.Send(new() { Kind = ActionKind.KeyUp, KeyCode = 66 }); await Task.Delay(25);
            recorder.Stop(); var noMoves = recorder.Buffer.Snapshot("Sin movimientos");
            AutomationChecks.Assert(noMoves.Steps.All(s => s.Kind != ActionKind.MouseMove) && noMoves.Steps.Any(s => s.Kind == ActionKind.KeyDown) && noMoves.Steps.Any(s => s.Kind == ActionKind.LeftDown), "Ignorar movimiento conserva clics/teclas");
            player.Start(new() { Steps = [new() { Kind = ActionKind.KeyDown, KeyCode = 67 }, new() { Kind = ActionKind.Wait, DelayMs = 1000 }, new() { Kind = ActionKind.KeyUp, KeyCode = 67 }] }, 1, 1);
            await Task.Delay(70); player.Pause(); await Task.Delay(50);
            AutomationChecks.Assert((GetAsyncKeyState(67) & 0x8000) == 0, "Pausa libera tecla física");
            player.Resume(); await Task.Delay(50);
            AutomationChecks.Assert((GetAsyncKeyState(67) & 0x8000) != 0, "Reanudación restaura tecla lógica");
            player.Stop(); await Task.Delay(50);
            AutomationChecks.Assert((GetAsyncKeyState(67) & 0x8000) == 0, "Detención libera tecla");
        }
        finally
        {
            process.CloseMainWindow();
            if (!process.WaitForExit(1500)) process.Kill();
            host.Activate();
        }
    }
    public static async Task MacroUI(MainForm app, string folder)
    {
        var tabs = Find<TabControl>(app, "Tools"); tabs.SelectedIndex = 2; app.Activate(); NativeTestFocus.Give(app.Handle);
        Find<TextBox>(app, "MacroName").Text = "Macro verificada";
        EditDialog(() => Find<Button>(app, "MacroAdd").PerformClick(), (int)ActionKind.Wait, 300);
        var preview = Find<StepsPreview>(app, "MacroPreview");
        AutomationChecks.Assert(preview.RowCount == 1, "Agregar paso");
        Find<Button>(app, "MacroDuplicate").PerformClick(); AutomationChecks.Assert(preview.RowCount == 2, "Duplicar paso");
        EditDialog(() => Find<Button>(app, "MacroEdit").PerformClick(), (int)ActionKind.Wait, 100);
        Find<Button>(app, "MacroUp").PerformClick();
        Find<Button>(app, "MacroSave").PerformClick();
        AutomationChecks.Assert(Find<Label>(app, "MacroNotice").Text == "Macro guardada", "Mensaje de éxito");
        Program.SavePreview(app, "macro-guardada.png");
        var storage = new ClickMoves.Storage.AutomationStore(Path.Combine(folder, "macros"));
        var saved = storage.ListMacros(out _).Single().Document;
        AutomationChecks.Assert(saved.Steps[0].DelayMs == 100 && saved.Steps[1].DelayMs == 300, "Editar y reordenar antes de guardar");
        Find<Button>(app, "MacroDelete").PerformClick(); AutomationChecks.Assert(preview.RowCount == 1, "Eliminar paso");
        Find<Button>(app, "MacroNew").PerformClick();
        Find<ComboBox>(app, "MacroLibrary").SelectedIndex = -1; Find<ComboBox>(app, "MacroLibrary").SelectedIndex = 0;
        AutomationChecks.Assert(preview.RowCount == 2, "Cargar macro guardada");
        Find<NumericUpDown>(app, "MacrosCycles").Value = 3;
        Find<Button>(app, "MacrosPlay").PerformClick(); await Task.Delay(50);
        Find<Button>(app, "MacrosPause").PerformClick(); await Task.Delay(50);
        AutomationChecks.Assert(Find<Label>(app, "OverallStatus").Text.Contains("Pausado"), "Pausa UI macro");
        tabs.SelectedIndex = 0; await Task.Delay(50);
        AutomationChecks.Assert(!Find<Button>(app, "Toggle").Enabled, "Macro pausada impide AutoClick");
        tabs.SelectedIndex = 2;
        Find<Button>(app, "MacrosPause").PerformClick(); await Task.Delay(50);
        var text = Find<Button>(app, "EmergencyStop").Text; var emergency = Enum.Parse<Keys>(text.Split('·')[1].Trim());
        keybd_event((byte)emergency, 0, 0, UIntPtr.Zero); keybd_event((byte)emergency, 0, 2, UIntPtr.Zero);
        // A native input and its posted UI update need not arrive within a fixed 80 ms.
        // 400 ms is still shorter than the remaining macro, so natural completion cannot pass this check.
        for (int i = 0; i < 20 && Find<Label>(app, "OverallStatus").Text != "Listo"; i++) await Task.Delay(20);
        AutomationChecks.Assert(Find<Label>(app, "OverallStatus").Text == "Listo", $"Parada global de emergencia ({emergency}): {Find<Label>(app, "OverallStatus").Text}");
        using var image = new Bitmap(app.Width, app.Height); app.DrawToBitmap(image, new(Point.Empty, image.Size)); image.Save(Path.Combine(Environment.CurrentDirectory, "TestResults", "macros.png"));
        tabs.SelectedIndex = 1; await Task.Delay(40);
        Find<Button>(app, "RecordStart").PerformClick(); await Task.Delay(50);
        AutomationChecks.Assert(Find<Label>(app, "RecordingStatus").Text.Contains("Grabando"), "Inicio grabador UI");
        Find<Button>(app, "RecordPause").PerformClick(); await Task.Delay(40);
        AutomationChecks.Assert(Find<Label>(app, "RecordingStatus").Text.Contains("Pausado"), "Pausa grabador UI");
        Find<Button>(app, "RecordPause").PerformClick(); Find<Button>(app, "EmergencyStop").PerformClick(); await Task.Delay(40);
        AutomationChecks.Assert(Find<Label>(app, "OverallStatus").Text == "Listo", "Botón parada global grabador");
        tabs.SelectedIndex = 2;
        Find<Button>(app, "MacroNew").PerformClick();
        EditDialog(() => Find<Button>(app, "MacroAdd").PerformClick(), (int)ActionKind.KeyDown, 0, "Ctrl izquierdo");
        EditDialog(() => Find<Button>(app, "MacroAdd").PerformClick(), (int)ActionKind.Wait, 10000);
        Find<Button>(app, "MacrosPlay").PerformClick();
        await WaitForHeldKey(app, Keys.LControlKey);
        keybd_event((byte)emergency, 0, 0, UIntPtr.Zero); keybd_event((byte)emergency, 0, 2, UIntPtr.Zero);
        for (int i = 0; i < 25 && (Find<Label>(app, "OverallStatus").Text != "Listo" || (GetAsyncKeyState((int)Keys.LControlKey) & 0x8000) != 0); i++) await Task.Delay(20);
        AutomationChecks.Assert(Find<Label>(app, "OverallStatus").Text == "Listo" && (GetAsyncKeyState((int)Keys.LControlKey) & 0x8000) == 0,
            $"Emergencia con Ctrl: {Find<Label>(app, "OverallStatus").Text}; Ctrl retenido {(GetAsyncKeyState((int)Keys.LControlKey) & 0x8000) != 0}");
        Find<Button>(app, "MacroNew").PerformClick();
        EditDialog(() => Find<Button>(app, "MacroAdd").PerformClick(), (int)ActionKind.KeyDown, 0, "Mayús izquierda");
        EditDialog(() => Find<Button>(app, "MacroAdd").PerformClick(), (int)ActionKind.Wait, 10000);
        Find<Button>(app, "MacrosPlay").PerformClick();
        await WaitForHeldKey(app, Keys.LShiftKey);
        app.Close();
        AutomationChecks.Assert(SpinWait.SpinUntil(() => (GetAsyncKeyState((int)Keys.LShiftKey) & 0x8000) == 0, 500), "Cierre libera tecla de macro");
    }
    private static async Task WaitForHeldKey(MainForm app, Keys key)
    {
        for (int i = 0; i < 25 && (GetAsyncKeyState((int)key) & 0x8000) == 0; i++) await Task.Delay(20);
        AutomationChecks.Assert((GetAsyncKeyState((int)key) & 0x8000) != 0,
            $"Macro mantiene {key}: {Find<Label>(app, "OverallStatus").Text}");
    }
    private static void EditDialog(Action open, int kind, int delay, string? key = null)
    {
        using var timer = new System.Windows.Forms.Timer { Interval = 40 };
        timer.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.OfType<StepEditor>().FirstOrDefault(); if (dialog == null) return;
            timer.Stop(); Find<ComboBox>(dialog, "StepKind").SelectedIndex = kind; Find<NumericUpDown>(dialog, "StepDelay").Value = delay;
            if (key != null) { var select = Find<ComboBox>(dialog, "StepKey"); select.SelectedItem = select.Items.Cast<object>().Single(x => x.ToString() == key); }
            Find<Button>(dialog, "StepAccept").PerformClick();
        };
        timer.Start(); open();
    }
    private static T Find<T>(Control c, string name) where T : Control => (T)c.Controls.Find(name, true).Single();
    private static int ReadCount(IntPtr target, int which) => SendMessage(target, 0x8001, new(which), IntPtr.Zero).ToInt32();
    private sealed class TargetForm : Form
    {
        private readonly int[] counts = new int[5];
        public TargetForm()
        {
            Text = "Destino externo de automatización"; StartPosition = FormStartPosition.Manual; Location = new(100, 80); Size = new(300, 240); TopMost = true;
            MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) counts[0]++; if (e.Button == MouseButtons.Right) counts[1]++; };
            KeyDown += (_, _) => counts[2]++; KeyUp += (_, _) => counts[3]++; MouseMove += (_, _) => counts[4]++;
        }
        protected override void WndProc(ref Message m) { if (m.Msg == 0x8001) { m.Result = new(counts[m.WParam.ToInt32()]); return; } base.WndProc(ref m); }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? kind, string name);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
