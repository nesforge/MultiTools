using System.Diagnostics;
using System.Runtime.InteropServices;
using ClickMoves.AutoClick;
using ClickMoves.Configuration;
using ClickMoves.Platform;
using ClickMoves.UI;

internal static class Program
{
    private static int failures;
    private static readonly List<string> results = [];
    private static readonly string TestRoot = Path.Combine(Environment.CurrentDirectory, "TestResults", Guid.NewGuid().ToString("N"));
    [STAThread]
    private static int Main(string[] args)
    {
        Directory.CreateDirectory(TestRoot);
        if (args.Contains("--brand-assets")) { BrandAssetChecks.Verify(); return 0; }
        if (args.Contains("--benchmark")) { PackagingMeasurements.Run(); return 0; }
        if (args.Contains("--native-target")) { NativeAutomationChecks.Target(); return 0; }
        if (args.Contains("--core-only"))
        {
            Check("Configuración: tamaño, profundidad, compatibilidad y errores privados", ConfigurationChecks.Run);
            Check("AutoClick sin escritorio: ambos lados, velocidades, parada y contador", CoreAutoClickChecks.Run);
            Check("Automatización: JSON validado, varias macros, orden, velocidad, esperas, ciclos, pausa/reanudación y exclusión", AutomationChecks.Core);
            File.WriteAllLines(Path.Combine(Environment.CurrentDirectory, "AutomationResults.txt"), results);
            return failures == 0 ? 0 : 1;
        }
        if (args.Contains("--smoke-only"))
        {
            ApplicationConfiguration.Initialize();
            SmokeBinary();
            File.WriteAllLines(Path.Combine(Environment.CurrentDirectory, "SmokeResults.txt"), results);
            return failures == 0 ? 0 : 1;
        }
        Check("MiniTools: logo e icono originales recuperados", BrandAssetChecks.Verify);
        Check("Configuración: tamaño, profundidad, compatibilidad y errores privados", ConfigurationChecks.Run);
        Check("Validación de velocidades y atajos", () =>
        {
            foreach (decimal value in new[] { -1m, 0m, 0.09m, 100.1m })
                Throws(() => new AppSettings { ClicksPerSecond = value }.Validate());
            foreach (string value in new[] { "", "F12", "F25", "Ctrl+Ctrl+K", "Ctrl+NOPE" })
                Throws(() => HotkeySpec.Parse(value));
            foreach (string value in new[] { "A", "Shift+1", "F6", "F1", "F24", "Ctrl+Alt+K", "Ctrl+1", "Shift+F9" })
                HotkeySpec.Parse(value);
        });
        Check("Configuración: persistencia, archivo corrupto y valores iniciales", () =>
        {
            var store = new SettingsStore(Path.Combine(TestRoot, "config.json"));
            Assert(store.Load(out _) == new AppSettings(), "Valores iniciales");
            var value = new AppSettings { ClicksPerSecond = 25.5m, Button = ClickButton.Right, Hotkey = "Ctrl+Alt+K" };
            store.Save(value);
            Assert(store.Load(out var warning) == value && warning == null, "Persistencia");
            File.WriteAllText(store.FilePath, "invalid");
            Assert(store.Load(out warning) == new AppSettings() && warning != null, "Recuperación");
        });
        Check("Motor: velocidades 0,1 / 1 / 10 / 100, ambos botones y contador", () =>
        {
            foreach (var (cps, duration, min, max) in new[] { (0.1m, 150, 1, 1), (1m, 2200, 2, 4), (10m, 650, 4, 9), (100m, 350, 15, 45) })
            {
                foreach (var button in Enum.GetValues<ClickButton>())
                {
                    var sender = new RecordingSender();
                    using var engine = new AutoClickEngine(sender);
                    engine.Start(new() { ClicksPerSecond = cps, Button = button });
                    Assert(!engine.Start(new()), "Inicio duplicado bloqueado");
                    Thread.Sleep(duration);
                    var sw = Stopwatch.StartNew();
                    engine.Stop();
                    Assert(sw.ElapsedMilliseconds < 250, $"Detención interrumpible: {sw.ElapsedMilliseconds} ms a {cps} clics/s ({button})");
                    long sent = engine.Count;
                    Assert(sent >= min && sent <= max, $"Velocidad {cps}: {sent}");
                    Assert(sent == sender.Count && sender.Last == button, "Contador y botón");
                    Thread.Sleep(70);
                    Assert(engine.Count == sent, "Sin clics después de detener");
                    engine.ResetCount();
                    Assert(engine.Count == 0, "Reinicio");
                }
            }
        });
        Check("Motor: error de envío y cierre durante actividad", () =>
        {
            using var failed = new AutoClickEngine(new FailingSender());
            failed.Start(new());
            Thread.Sleep(100);
            Assert(!failed.IsRunning && failed.Count == 0 && failed.TakeError() != null, "Error no contado");
            var sender = new RecordingSender();
            var engine = new AutoClickEngine(sender);
            engine.Start(new());
            Thread.Sleep(100);
            Throws(engine.ResetCount);
            engine.Dispose();
            long n = sender.Count;
            Thread.Sleep(100);
            Assert(sender.Count == n, "Dispose detiene");
        });
        Check("Automatización: almacenamiento y validación, esperas, ciclos, velocidad, pausa y exclusión", AutomationChecks.Core);
        ApplicationConfiguration.Initialize();
        var uiSender = new RecordingSender();
        using var app = new MainForm(new SettingsStore(Path.Combine(TestRoot, "ui.json")), uiSender);
        app.Shown += async (_, _) =>
        {
            try
            {
                await Task.Delay(100);
                using (var preview = new Bitmap(app.Width, app.Height))
                {
                    app.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
                    preview.Save(Path.Combine(Environment.CurrentDirectory, "TestResults", "interfaz.png"));
                }
                Button toggle = Find<Button>(app, "Toggle");
                Assert(app.Text == "MiniTools" && app.Icon != null, "Nombre e icono de MiniTools");
                Assert(Find<Label>(app, "AuthorCredit").Text == "Desarrollado por BrioCraft", "Crédito exacto");
                Assert(Find<Label>(app, "ProductName").Text == "MiniTools" && app.Font.Name == "Segoe UI", "Producto y tipografía originales");
                Assert(Find<BrandMark>(app, "ProductLogo").Parent!.Name == "ProductHeader", "Logo de MiniTools en la cabecera");
                Assert(!app.Controls.Find("DeveloperLogo", true).Any(), "Autoría discreta sin sustituir el logo del producto");
                Assert(toggle.BackColor.ToArgb() == Color.FromArgb(24, 96, 177).ToArgb(), "Azul original #1860B1, anterior al cambio BrioCraft");
                Assert(BrandAssetChecks.Contrast(toggle.ForeColor, toggle.BackColor) >= 4.5 &&
                    BrandAssetChecks.Contrast(toggle.ForeColor, toggle.FlatAppearance.MouseOverBackColor) >= 4.5 &&
                    BrandAssetChecks.Contrast(toggle.ForeColor, toggle.FlatAppearance.MouseDownBackColor) >= 4.5, "Contraste en botón principal y estados interactivos");
                Assert(BrandAssetChecks.Contrast(Find<Label>(app, "ProductDescription").ForeColor, app.BackColor) >= 4.5, "Contraste de texto secundario");
                var tools = Find<TabControl>(app, "Tools");
                foreach (int index in new[] { 1, 2 })
                {
                    tools.SelectedIndex = index; app.PerformLayout(); await Task.Delay(80);
                    string owner = index == 1 ? "Grabador" : "Macros";
                    foreach (string action in new[] { "Play", "Pause", "Stop" })
                    {
                        var button = Find<Button>(app, owner + action);
                        Assert(button.Visible && button.RectangleToScreen(button.ClientRectangle).Bottom <= app.RectangleToScreen(app.ClientRectangle).Bottom,
                            "Reproducción visible: " + button.Name);
                    }
                    using var pageImage = new Bitmap(app.Width, app.Height);
                    app.DrawToBitmap(pageImage, new Rectangle(Point.Empty, pageImage.Size));
                    pageImage.Save(Path.Combine(Environment.CurrentDirectory, "TestResults", index == 1 ? "grabador.png" : "macros-inicial.png"));
                }
                tools.SelectedIndex = 0;
                Size normalSize = app.Size;
                app.Size = app.MinimumSize; app.PerformLayout();
                foreach (int index in new[] { 0, 1, 2 })
                {
                    tools.SelectedIndex = index; app.PerformLayout(); await Task.Delay(50);
                    string[] names = index == 0 ? ["Toggle", "Reset", "Shortcut"] : index == 1 ? ["RecordStart", "GrabadorPlay", "GrabadorStop"] : ["MacroAdd", "MacrosPlay", "MacrosStop"];
                    foreach (string buttonName in names.Append("EmergencyStop"))
                    {
                        var button = Find<Button>(app, buttonName);
                        Assert(button.Visible && app.RectangleToScreen(app.ClientRectangle).Contains(button.RectangleToScreen(button.ClientRectangle)), "Control visible al tamaño mínimo: " + buttonName);
                    }
                    var credit = Find<Label>(app, "AuthorCredit");
                    Assert(credit.Width >= TextRenderer.MeasureText(credit.Text, credit.Font).Width && app.RectangleToScreen(app.ClientRectangle).Contains(credit.RectangleToScreen(credit.ClientRectangle)), "Firma visible al tamaño mínimo");
                }
                app.Size = normalSize; tools.SelectedIndex = 0;
                Pass("MiniTools: nombre, icono, crédito y acciones visibles en tres pestañas, también al tamaño mínimo");
                Button reset = Find<Button>(app, "Reset");
                Assert(Find<Label>(app, "Status").Text == "Detenido", "Abre detenido");
                var selector = Find<MouseSelector>(app, "ClickType");
                Assert(selector.SelectedButton == ClickButton.Left, "Izquierdo inicial");
                Assert(selector.HitTest(new(40, 60)) == ClickButton.Left && selector.HitTest(new(110, 60)) == ClickButton.Right, "Dos zonas del mouse");
                Assert(selector.HitTest(new(75, 65)) == null && selector.HitTest(new(75, 160)) == null, "Rueda y cuerpo decorativos");
                ClickControl(selector, new(110, 60));
                Assert(selector.SelectedButton == ClickButton.Right, "Selección derecha con mouse");
                ClickControl(selector, new(40, 60));
                Assert(selector.SelectedButton == ClickButton.Left, "Selección izquierda con mouse");
                app.TopMost = true;
                for (int attempt = 0; attempt < 10 && GetForegroundWindow() != app.Handle; attempt++)
                {
                    NativeTestFocus.Give(app.Handle);
                    var point = selector.PointToScreen(new(40, 60));
                    new WindowsActionSender().Send(new() { Kind = ClickMoves.Automation.ActionKind.LeftClick, X = point.X, Y = point.Y });
                    await Task.Delay(30);
                }
                Assert(GetForegroundWindow() == app.Handle, "El entorno debe permitir enfocar la ventana antes de probar el teclado");
                bool selectorFocus = selector.Focus(); Key(Keys.Right); await Task.Delay(80);
                Assert(selector.SelectedButton == ClickButton.Right, $"Derecha por teclado: Focus={selectorFocus}/{selector.Focused}, activo={app.ActiveControl?.Name}, pestaña={Find<TabControl>(app, "Tools").SelectedIndex}, fg={GetForegroundWindow()}/{app.Handle}, habilitado={selector.Enabled}");
                Key(Keys.Left); await Task.Delay(80);
                Assert(selector.SelectedButton == ClickButton.Left, "Izquierda por teclado");
                tools.Focus(); Down(Keys.ControlKey); Key(Keys.Tab); Up(Keys.ControlKey); await Task.Delay(80);
                Assert(tools.SelectedIndex == 1, "Ctrl+Tab navega al grabador");
                Down(Keys.ControlKey); Down(Keys.ShiftKey); Key(Keys.Tab); Up(Keys.ShiftKey); Up(Keys.ControlKey); await Task.Delay(80);
                Assert(tools.SelectedIndex == 0, "Ctrl+Mayús+Tab vuelve a AutoClick");
                toggle.Focus();
                using (var focusImage = new Bitmap(app.Width, app.Height))
                { app.DrawToBitmap(focusImage, new(Point.Empty, focusImage.Size)); focusImage.Save(Path.Combine(Environment.CurrentDirectory, "TestResults", "foco.png")); }
                Assert(toggle.Focused, "Acción principal accesible por foco");
                Pass("MiniTools: azul original, Segoe UI, firma discreta y navegación por teclado con foco visible");
                Find<NumericUpDown>(app, "Speed").Value = 20;
                Assert(Find<Label>(app, "Interval").Text.Contains("50"), "Intervalo equivalente");
                var persisted = new SettingsStore(Path.Combine(TestRoot, "ui.json")).Load(out _);
                Assert(persisted.ClicksPerSecond == 20 && persisted.Button == ClickButton.Left, $"Guardado automático: {persisted.ClicksPerSecond}/{persisted.Button}; {Find<Label>(app, "Notice").Text}");
                Pass("Mouse vectorial: ambos lados con mouse y teclado; rueda/cuerpo decorativos; velocidad e intervalo persistidos");
                toggle.PerformClick();
                await Task.Delay(200);
                Assert(Find<Label>(app, "Count").Text == "0" && !reset.Enabled, "Cuenta regresiva sin clics: " + Find<Label>(app, "Status").Text + " / " + Find<Label>(app, "Notice").Text);
                SavePreview(app, "preparando.png");
                toggle.PerformClick();
                Assert(reset.Enabled, "Cancelar cuenta regresiva");
                toggle.PerformClick();
                await Task.Delay(3250);
                Assert(Find<Label>(app, "Status").Text == "Activo", "Inicio tras cuenta regresiva");
                toggle.PerformClick();
                reset.PerformClick();
                Assert(Find<Label>(app, "Count").Text == "0", "Reinicio desde interfaz");
                Pass("Interfaz: validación, cuenta regresiva, cancelar, iniciar, detener, reiniciar");
                // A separate foreground window verifies delivery and a shortcut with app unfocused.
                using var target = new Form { Text = "Destino de pruebas ClickMoves", StartPosition = FormStartPosition.Manual, Location = new(100, 80), Size = new(300, 250), TopMost = true };
                int left = 0, right = 0;
                target.MouseDown += (_, e) => { if (GetMessageExtraInfo().ToInt64() != 0x434d0200) return; if (e.Button == MouseButtons.Left) left++; if (e.Button == MouseButtons.Right) right++; };
                app.Location = new(10, 20);
                target.Show();
                target.Activate();
                Cursor.Position = target.PointToScreen(new(100, 100));
                await Task.Delay(150);
                using (var native = new AutoClickEngine(new PositionedTestSender(target.PointToScreen(new(100, 100)))))
                {
                    native.Start(new() { ClicksPerSecond = 10 });
                    await Task.Delay(450);
                    native.Stop();
                    await Task.Delay(100);
                Assert(left == native.Count && left >= 3, $"Clic izquierdo real: {left}/{native.Count}; cursor {Cursor.Position}; destino {target.Bounds}; hwnd cursor {WindowFromPoint(Cursor.Position)} vs {target.Handle}");
                    native.ResetCount();
                    native.Start(new() { ClicksPerSecond = 20, Button = ClickButton.Right });
                    await Task.Delay(350);
                    native.Stop();
                    await Task.Delay(100);
                    Assert(right == native.Count && right >= 4, $"Clic derecho real: {right}/{native.Count}");
                }
                Pass($"SendInput real: izquierdo {left}, derecho {right}; contador coincide con destino controlado");
                Key(Keys.F6);
                await Task.Delay(200);
                Assert(Find<Label>(app, "Status").Text == "Activo", "F6 sin foco inicia");
                Key(Keys.F6);
                await Task.Delay(100);
                Assert(Find<Label>(app, "Status").Text == "Detenido", "F6 sin foco detiene");
                Key(Keys.F6); await Task.Delay(100);
                target.Hide(); app.Activate();
                var shortcut = Find<Button>(app, "Shortcut");
                shortcut.PerformClick();
                Assert(Find<Label>(app, "Status").Text == "Detenido" && !toggle.Enabled, "Captura pausa y suspende");
                Down(Keys.ControlKey); await Task.Delay(80); Up(Keys.ControlKey); await Task.Delay(80);
                Assert(!toggle.Enabled && shortcut.Text == "Presiona tu atajo…", "Solo modificadores rechazados");
                Key(Keys.Escape); await Task.Delay(120);
                Assert(shortcut.Text == "F6" && toggle.Enabled, "Escape conserva F6");
                shortcut.PerformClick();
                Down(Keys.F6); await Task.Delay(100);
                Assert(!toggle.Enabled && Find<Label>(app, "Status").Text == "Detenido", "Capturar el atajo actual no activa");
                Up(Keys.F6); await Task.Delay(100);
                Assert(toggle.Enabled && Find<Label>(app, "Status").Text == "Detenido", "Soltar captura F6 no activa");
                shortcut.PerformClick();
                Down(Keys.ControlKey); Down(Keys.Menu); Down(Keys.A); await Task.Delay(100);
                Assert(shortcut.Text == "Ctrl + Alt + A" && !toggle.Enabled, "Combinación capturada espera liberación");
                Down(Keys.A); Down(Keys.A); await Task.Delay(80);
                Assert(Find<Label>(app, "Status").Text == "Detenido", "Repetición durante captura ignorada");
                Up(Keys.A); await Task.Delay(80);
                Assert(!toggle.Enabled, "Espera también soltar modificadores");
                Up(Keys.Menu); Up(Keys.ControlKey); await Task.Delay(140);
                Assert(toggle.Enabled && Find<Label>(app, "Status").Text == "Detenido", "Captura guardada no inicia");
                persisted = new SettingsStore(Path.Combine(TestRoot, "ui.json")).Load(out _);
                Assert(persisted.Hotkey == "Ctrl+Alt+A", "Atajo guardado automáticamente");
                Down(Keys.ControlKey); await Task.Delay(80);
                shortcut.PerformClick();
                Down(Keys.Menu); Down(Keys.A); await Task.Delay(80);
                Assert(shortcut.Text == "Ctrl + Alt + A", "Modificador previo a captura conservado");
                Up(Keys.A); Up(Keys.Menu); Up(Keys.ControlKey); await Task.Delay(140);
                Assert((GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) == 0 && toggle.Enabled, "Modificador previo liberado sin quedar atascado");
                Pass("Captura: pausa activa, modificadores rechazados, Escape, F6 actual, combinación y liberación completa sin activación");
                // Reserve a candidate elsewhere to exercise registration rollback.
                using (var occupied = new GlobalHotkey(target.Handle))
                {
                    occupied.Set("F8");
                    shortcut.PerformClick(); Key(Keys.F8); await Task.Delay(140);
                    Assert(shortcut.Text == "Ctrl + Alt + A" && Find<Label>(app, "Notice").Text.Contains("ocupado"), "Atajo ocupado conserva anterior: " + shortcut.Text + " / " + Find<Label>(app, "Notice").Text);
                    SavePreview(app, "error-atajo.png");
                }
                target.Show(); target.Activate();
                Down(Keys.ControlKey); Down(Keys.Menu); Down(Keys.A); await Task.Delay(120);
                Assert(Find<Label>(app, "Status").Text == "Activo", "Atajo personalizado global inicia");
                Down(Keys.A); Down(Keys.A); await Task.Delay(120);
                Assert(Find<Label>(app, "Status").Text == "Activo", "Mantener tecla no alterna varias veces");
                Up(Keys.A); Up(Keys.Menu); Up(Keys.ControlKey); await Task.Delay(100);
                Key(Keys.A, true); await Task.Delay(100);
                Assert(Find<Label>(app, "Status").Text == "Detenido", "Segunda pulsación detiene una vez");
                Key(Keys.A, true); await Task.Delay(100);
                Assert(Find<Label>(app, "Status").Text == "Activo", "Tercera pulsación inicia una vez");
                Pass("Atajo ocupado: conserva anterior; Ctrl+Alt+A global alterna una vez por pulsación y evita repetición; cierre activo");
                Find<Button>(app, "Toggle").PerformClick(); target.Hide(); app.TopMost = false;
                await NativeAutomationChecks.Run(app);
                Pass("Grabación/reproducción reales: mouse, teclas, movimientos, opción sin movimientos y pausa que libera/restaura teclas");
                await NativeAutomationChecks.MacroUI(app, TestRoot);
                Pass("Editor: crear, agregar, editar, duplicar, eliminar, reordenar, guardar/cargar, pausas, exclusión, parada global y cierre activo");
            }
            catch (Exception ex) { Fail("Integración Windows", ex); app.Close(); }
        };
        Application.Run(app);
        long afterClose = uiSender.Count;
        Thread.Sleep(100);
        Check("Cierre de interfaz: no se envían clics después de cerrar", () => Assert(uiSender.Count == afterClose, "Envíos posteriores al cierre"));
        File.WriteAllLines(Path.Combine(Environment.CurrentDirectory, "TestResults.txt"), results);
        Console.WriteLine($"Fallos: {failures}");
        return failures == 0 ? 0 : 1;
    }
    private static T Find<T>(Control parent, string name) where T : Control => (T)parent.Controls.Find(name, true).Single();
    public static void SavePreview(Form form, string filename)
    {
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new(Point.Empty, image.Size));
        image.Save(Path.Combine(Environment.CurrentDirectory, "TestResults", filename));
    }
    private static void SmokeBinary()
    {
        string folder = Path.Combine(TestRoot, "isolated");
        Directory.CreateDirectory(folder);
        string exe = Path.Combine(folder, "MiniTools.exe");
        File.Copy(Path.Combine(Environment.CurrentDirectory, "dist", "win-x64", "MiniTools.exe"), exe);
        using var target = new Form { Text = "Prueba del ejecutable final", StartPosition = FormStartPosition.Manual, Location = new(100, 100), Size = new(300, 250), TopMost = true };
        int clicks = 0;
        target.MouseDown += (_, _) => clicks++;
        target.Shown += async (_, _) =>
        {
            Process? process = null;
            using var cursor = new System.Windows.Forms.Timer { Interval = 5 };
            try
            {
                var start = new ProcessStartInfo(exe)
                {
                    UseShellExecute = false, WorkingDirectory = folder, WindowStyle = ProcessWindowStyle.Normal
                };
                string extraction = Path.Combine(TestRoot, "extracted");
                start.Environment["DOTNET_ROOT"] = Path.Combine(TestRoot, "no-runtime");
                start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
                start.Environment["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] = extraction;
                var launchTime = Stopwatch.StartNew();
                process = Process.Start(start)!;
                IntPtr handle = IntPtr.Zero;
                for (int i = 0; i < 100 && handle == IntPtr.Zero; i++)
                {
                    await Task.Delay(100);
                    IntPtr candidate = FindWindow(null, "MiniTools");
                    if (candidate != IntPtr.Zero && FindChild(candidate, "Desarrollado por BrioCraft") != IntPtr.Zero) handle = candidate;
                    Assert(!process.HasExited, "El ejecutable terminó al abrirse");
                }
                Assert(handle != IntPtr.Zero, "Ventana del ejecutable");
                Assert(FindChild(handle, "Desarrollado por BrioCraft") != IntPtr.Zero, "Crédito en el ejecutable final");
                var metadata = FileVersionInfo.GetVersionInfo(exe);
                Assert(metadata.ProductName == "MiniTools" && metadata.CompanyName == "BrioCraft" && metadata.ProductVersion?.StartsWith("1.4.2") == true && metadata.FileDescription == "MiniTools", "Metadatos del ejecutable");
                Pass($"EXE: ventana disponible en {launchTime.ElapsedMilliseconds} ms; memoria de trabajo {process.WorkingSet64 / (1024 * 1024)} MiB");
                Assert(Directory.GetFiles(folder).Length == 1, "Distribución de un solo archivo");
                process.Refresh();
                var modules = process.Modules.Cast<ProcessModule>().ToArray();
                string? core = modules.FirstOrDefault(m => m.ModuleName.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase))?.FileName;
                // SingleFileHost can statically link CoreCLR; then no coreclr.dll module exists.
                if (core != null) Assert(core.StartsWith(extraction, StringComparison.OrdinalIgnoreCase), "El runtime debe proceder del bundle");
                Assert(!modules.Any(m => m.FileName.Contains(@"\dotnet\shared\", StringComparison.OrdinalIgnoreCase)), "No debe cargar un runtime instalado");
                await Task.Delay(500);
                Assert(clicks == 0, "El ejecutable abre detenido");
                Pass("EXE aislado: abre detenido, único archivo, DOTNET_ROOT inexistente y sin módulos de un runtime instalado");
                target.BringToFront();
                target.Activate();
                NativeTestFocus.Give(target.Handle);
                Point point = target.PointToScreen(new(100, 100));
                cursor.Tick += (_, _) => Cursor.Position = point;
                cursor.Start();
                var saved = new SettingsStore().Load(out _);
                var spec = HotkeySpec.Parse(saved.Hotkey);
                SendHotkey(spec);
                await Task.Delay(550);
                Assert(FindChild(handle, "Activo") != IntPtr.Zero && ReadVisibleCounter(handle) > 0, "El atajo del ejecutable inicia envíos: " + DumpVisibleText(handle));
                IntPtr captureButton = FindChild(handle, HotkeySpec.Display(saved.Hotkey));
                Assert(captureButton != IntPtr.Zero, "Botón de captura del ejecutable");
                SetForegroundWindow(handle);
                PostMessage(captureButton, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                await Task.Delay(100);
                Assert(WindowText(captureButton) == "Presiona tu atajo…", "Modo captura del ejecutable");
                Key(Keys.Escape); await Task.Delay(140);
                Assert(WindowText(captureButton) == HotkeySpec.Display(saved.Hotkey), "Escape en ejecutable conserva el atajo");
                PostMessage(captureButton, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                await Task.Delay(100);
                HoldHotkey(spec, false);
                await Task.Delay(150);
                Assert(WindowText(captureButton) == HotkeySpec.Display(saved.Hotkey), "Captura de tecla retenida en ejecutable");
                HoldHotkey(spec, true);
                await Task.Delay(200);
                int paused = clicks;
                await Task.Delay(150);
                Assert(clicks == paused, "Captura del ejecutable pausa y no activa al soltar");
                target.Activate();
                SendHotkey(spec); await Task.Delay(250);
                Assert(FindChild(handle, "Activo") != IntPtr.Zero && ReadVisibleCounter(handle) > 0, "Atajo del ejecutable funciona después de captura");
                Pass("EXE final: captura temporal de teclado, Escape y espera de liberación sin activación");
                SendHotkey(spec); await Task.Delay(100); cursor.Stop(); target.Hide(); NativeTestFocus.Give(handle);
                SelectTool(handle, 1); await Task.Delay(150);
                IntPtr recordButton = FindChild(handle, "Grabar");
                Assert(recordButton != IntPtr.Zero, "Grabador del ejecutable");
                PostMessage(recordButton, 0x00F5, IntPtr.Zero, IntPtr.Zero); await Task.Delay(100);
                Assert(FindChildStarting(handle, "Grabando") != IntPtr.Zero, "Grabación en ejecutable");
                IntPtr recordPause = FindChild(handle, "Pausar grabación");
                PostMessage(recordPause, 0x00F5, IntPtr.Zero, IntPtr.Zero); await Task.Delay(100);
                Assert(FindChild(handle, "Reanudar grabación") != IntPtr.Zero, "Pausa del grabador en ejecutable");
                PostMessage(recordPause, 0x00F5, IntPtr.Zero, IntPtr.Zero); await Task.Delay(70);
                PostMessage(FindChild(handle, "Detener grabación"), 0x00F5, IntPtr.Zero, IntPtr.Zero); await Task.Delay(100);
                Pass("EXE final: grabador inicia, pausa, reanuda y detiene; recursos nativos disponibles");
                NativeTestFocus.Give(handle); SelectTool(handle, 2); await Task.Delay(150);
                PostMessage(FindChild(handle, "Agregar"), 0x00F5, IntPtr.Zero, IntPtr.Zero);
                IntPtr editor = IntPtr.Zero;
                for (int i = 0; i < 30 && editor == IntPtr.Zero; i++) { await Task.Delay(50); editor = FindWindow(null, "Agregar paso"); }
                Assert(editor != IntPtr.Zero, "Editor de paso en ejecutable");
                await Task.Delay(150);
                SendMessage(FindChild(editor, "Clic izquierdo"), 0x014E, new IntPtr(9), IntPtr.Zero);
                IntPtr delayEdit = IntPtr.Zero;
                for (int i = 0; i < 20 && delayEdit == IntPtr.Zero; i++) { delayEdit = FindTopEdit(editor, "100"); if (delayEdit == IntPtr.Zero) await Task.Delay(50); }
                Assert(delayEdit != IntPtr.Zero, "Espera del editor: " + DumpVisibleText(editor));
                GetWindowRect(delayEdit, out var editRect); NativeTestFocus.Give(editor);
                new WindowsActionSender().Send(new() { Kind = ClickMoves.Automation.ActionKind.LeftClick, X = editRect.Left + 20, Y = (editRect.Top + editRect.Bottom) / 2 });
                Down(Keys.ControlKey); Key(Keys.A); Up(Keys.ControlKey); Key(Keys.D5); Key(Keys.D0); Key(Keys.D0); Key(Keys.D0); await Task.Delay(70);
                PostMessage(FindChild(editor, "Aceptar"), 0x00F5, IntPtr.Zero, IntPtr.Zero); await Task.Delay(120);
                IntPtr macroPlay = FindChild(handle, "Reproducir");
                Assert(macroPlay != IntPtr.Zero, "Reproductor de macro en ejecutable");
                PostMessage(macroPlay, 0x00F5, IntPtr.Zero, IntPtr.Zero); await Task.Delay(100);
                IntPtr macroPause = FindChild(handle, "Pausar"); PostMessage(macroPause, 0x00F5, IntPtr.Zero, IntPtr.Zero); await Task.Delay(100);
                Assert(WindowText(macroPause) == "Reanudar", "Pausa de macro en ejecutable: " + DumpVisibleText(handle));
                PostMessage(macroPause, 0x00F5, IntPtr.Zero, IntPtr.Zero); await Task.Delay(70);
                string stopLabel = WindowText(FindChildStarting(handle, "Detener todo"));
                var emergency = Enum.Parse<Keys>(stopLabel.Split('·')[1].Trim());
                Down(Keys.ControlKey); Key(emergency); Up(Keys.ControlKey); await Task.Delay(120);
                Assert(IsWindowEnabled(macroPlay), "Emergencia en ejecutable con Ctrl presionado");
                PostMessage(macroPlay, 0x00F5, IntPtr.Zero, IntPtr.Zero); await Task.Delay(100);
                Pass("EXE final: crear paso de macro, reproducir, pausar/reanudar y parada global con Ctrl");
                PostMessage(handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
                for (int i = 0; i < 50 && !process.HasExited; i++) await Task.Delay(100);
                Assert(process.HasExited && process.ExitCode == 0, "Cierre limpio del ejecutable activo");
                await Task.Delay(100);
                int after = clicks;
                await Task.Delay(250);
                Assert(clicks == after, "Sin clics después de cerrar el ejecutable");
                Pass($"EXE final: atajo global con otra ventana enfocada, {clicks} clics observados, cierre activo sin envíos posteriores");
            }
            catch (Exception ex) { Fail("EXE final", ex); }
            finally
            {
                cursor.Stop();
                if (process != null && !process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(1000)) process.Kill();
                }
                process?.Dispose();
                target.Close();
            }
        };
        Application.Run(target);
    }
    private static void SendHotkey(HotkeySpec spec)
    {
        var modifiers = new List<Keys>();
        if ((spec.Modifiers & 2) != 0) modifiers.Add(Keys.ControlKey);
        if ((spec.Modifiers & 1) != 0) modifiers.Add(Keys.Menu);
        if ((spec.Modifiers & 4) != 0) modifiers.Add(Keys.ShiftKey);
        foreach (var modifier in modifiers) keybd_event((byte)modifier, 0, 0, UIntPtr.Zero);
        Key(spec.Key);
        modifiers.Reverse();
        foreach (var modifier in modifiers) keybd_event((byte)modifier, 0, 2, UIntPtr.Zero);
    }
    private static void HoldHotkey(HotkeySpec spec, bool release)
    {
        var keys = new List<Keys>();
        if ((spec.Modifiers & 2) != 0) keys.Add(Keys.ControlKey);
        if ((spec.Modifiers & 1) != 0) keys.Add(Keys.Menu);
        if ((spec.Modifiers & 4) != 0) keys.Add(Keys.ShiftKey);
        keys.Add(spec.Key);
        if (release) keys.Reverse();
        foreach (var key in keys) { if (release) Up(key); else Down(key); }
    }
    private static string WindowText(IntPtr handle)
    {
        var text = new System.Text.StringBuilder(256);
        GetWindowText(handle, text, text.Capacity);
        return text.ToString();
    }
    private static IntPtr FindChild(IntPtr parent, string text)
    {
        IntPtr result = IntPtr.Zero;
        EnumChildWindows(parent, (handle, _) => { if (IsWindowVisible(handle) && WindowText(handle) == text) { result = handle; return false; } return true; }, IntPtr.Zero);
        return result;
    }
    private static IntPtr FindChildStarting(IntPtr parent, string text)
    {
        IntPtr result = IntPtr.Zero;
        EnumChildWindows(parent, (handle, _) => { if (IsWindowVisible(handle) && WindowText(handle).StartsWith(text)) { result = handle; return false; } return true; }, IntPtr.Zero);
        return result;
    }
    private static long ReadVisibleCounter(IntPtr parent)
    {
        long result = 0;
        EnumChildWindows(parent, (handle, _) =>
        {
            var kind = new System.Text.StringBuilder(256); GetClassName(handle, kind, kind.Capacity);
            if (IsWindowVisible(handle) && long.TryParse(WindowText(handle), System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.CurrentCulture, out long value)) result = Math.Max(result, value);
            return true;
        }, IntPtr.Zero);
        return result;
    }
    private static IntPtr FindTopEdit(IntPtr parent, string text)
    {
        IntPtr result = IntPtr.Zero; int top = int.MaxValue;
        EnumChildWindows(parent, (handle, _) =>
        {
            var kind = new System.Text.StringBuilder(256); GetClassName(handle, kind, kind.Capacity);
            if (IsWindowVisible(handle) && WindowText(handle) == text)
            { GetWindowRect(handle, out var rect); if (rect.Top < top) { top = rect.Top; result = handle; } }
            return true;
        }, IntPtr.Zero);
        return result;
    }
    private static string DumpVisibleText(IntPtr parent)
    {
        var result = new List<string>();
        EnumChildWindows(parent, (handle, _) => { if (IsWindowVisible(handle)) result.Add(WindowText(handle)); return true; }, IntPtr.Zero);
        return string.Join(" | ", result);
    }
    private static void SelectTool(IntPtr parent, int index)
    {
        IntPtr tabs = IntPtr.Zero;
        EnumChildWindows(parent, (handle, _) =>
        {
            var kind = new System.Text.StringBuilder(256); GetClassName(handle, kind, kind.Capacity);
            if (kind.ToString().Contains("SysTabControl32")) { tabs = handle; return false; }
            return true;
        }, IntPtr.Zero);
        Assert(tabs != IntPtr.Zero, "Navegación del ejecutable");
        // TCM_GETITEMRECT contains a process-local pointer and cannot be sent cross-process.
        // Use the system-provided window rectangle and the app's own known header layout.
        GetWindowRect(tabs, out var rect);
        float scale = GetDpiForWindow(tabs) / 96f;
        var point = new Point(rect.Left + (int)((index == 1 ? 150 : 245) * scale), rect.Top + (int)(16 * scale));
        new WindowsActionSender().Send(new() { Kind = ClickMoves.Automation.ActionKind.LeftClick, X = point.X, Y = point.Y });
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    private static void Check(string name, Action action) { try { action(); Pass(name); } catch (Exception ex) { Fail(name, ex); } }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Se esperaba un error de validación"); }
    private static void Pass(string name) { results.Add("PASS: " + name); Console.WriteLine(results[^1]); }
    private static void Fail(string name, Exception ex) { failures++; results.Add("FAIL: " + name + " — " + ex); Console.WriteLine(results[^1]); }
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string windowName);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetMessageExtraInfo();
    private delegate bool EnumWindow(IntPtr handle, IntPtr param);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindow callback, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, System.Text.StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr handle);
    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageText(IntPtr window, uint message, IntPtr wp, string text);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, System.Text.StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
    private static void Down(Keys key) => keybd_event((byte)key, 0, 0, UIntPtr.Zero);
    private static void Up(Keys key) => keybd_event((byte)key, 0, 2, UIntPtr.Zero);
    private static void ClickControl(Control control, Point point)
    {
        var location = new IntPtr(point.X | (point.Y << 16));
        SendMessage(control.Handle, 0x0201, new IntPtr(1), location);
        SendMessage(control.Handle, 0x0202, IntPtr.Zero, location);
    }
    private static void Key(Keys key, bool modifiers = false)
    {
        if (modifiers) { keybd_event((byte)Keys.ControlKey, 0, 0, UIntPtr.Zero); keybd_event((byte)Keys.Menu, 0, 0, UIntPtr.Zero); }
        keybd_event((byte)key, 0, 0, UIntPtr.Zero);
        keybd_event((byte)key, 0, 2, UIntPtr.Zero);
        if (modifiers) { keybd_event((byte)Keys.Menu, 0, 2, UIntPtr.Zero); keybd_event((byte)Keys.ControlKey, 0, 2, UIntPtr.Zero); }
    }
    private sealed class RecordingSender : IClickSender
    {
        public long Count;
        public ClickButton Last;
        public void Send(ClickButton button) { Last = button; Interlocked.Increment(ref Count); }
    }
    private sealed class FailingSender : IClickSender { public void Send(ClickButton button) => throw new IOException("Fallo simulado"); }
    // Only the harness positions the cursor. The production sender never moves it.
    private sealed class PositionedTestSender(Point point) : IClickSender
    {
        private readonly WindowsActionSender sender = new();
        public void Send(ClickButton button) => sender.Send(new() { Kind = button == ClickButton.Left ? ClickMoves.Automation.ActionKind.LeftClick : ClickMoves.Automation.ActionKind.RightClick, X = point.X, Y = point.Y });
    }
}
