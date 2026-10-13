using System.Diagnostics;
using ClickMoves.AutoClick;
using ClickMoves.Configuration;
using ClickMoves.Platform;
using ClickMoves.Automation;
using ClickMoves.Storage;

namespace ClickMoves.UI;

public sealed class MainForm : Form
{
    private readonly SettingsStore store;
    private readonly AutoClickEngine engine;
    private GlobalHotkey? hotkey;
    private HotkeyCapture? keyboardCapture;
    private AppSettings settings;
    private readonly AutomationCoordinator coordinator = new();
    private readonly PlaybackEngine player;
    private readonly WindowsRecorder recorder;
    private readonly GlobalHotkey emergencyHotkey;
    private readonly int emergencyKey;
    private EmergencyMonitor? emergencyMonitor;
    private readonly RecorderPage recorderPage;
    private readonly MacroPage macroPage;
    private readonly Label overall = new() { Name = "OverallStatus", Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new(ToolStyle.Gap) };
    private readonly ToolPreferencesStore toolStore;
    private ToolPreferences preferences;
    private string? preferencesWarning;
    private readonly NumericUpDown speed = new() { Name = "Speed", Minimum = AppSettings.MinCps, Maximum = AppSettings.MaxCps, DecimalPlaces = 2, Increment = 0.1m, Width = 94, BorderStyle = BorderStyle.FixedSingle };
    private readonly MouseSelector mouse = new() { Name = "ClickType", Anchor = AnchorStyles.None };
    private readonly Label side = new() { Name = "SelectedSide", AutoSize = true, Anchor = AnchorStyles.None };
    private readonly Label interval = new() { Name = "Interval", AutoSize = true, ForeColor = ToolStyle.Muted };
    private readonly Button shortcut = ToolStyle.Button("F6", "Shortcut");
    private readonly Button toggle = ToolStyle.Button("Iniciar", "Toggle");
    private readonly Button reset = ToolStyle.Button("Reiniciar", "Reset");
    private readonly Label status = new() { Name = "Status", Text = "Detenido", AutoSize = true, ForeColor = ToolStyle.Muted, Anchor = AnchorStyles.Right };
    private readonly Label count = new() { Name = "Count", Text = "0", AutoSize = true, Font = ToolStyle.CounterFont };
    private readonly Label notice = new() { Name = "Notice", AutoSize = true, MaximumSize = new(380, 0), ForeColor = ToolStyle.Blue, Font = ToolStyle.SmallFont };
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 30 };
    private readonly ToolTip tips = new();
    private readonly Stopwatch countdown = new();
    private bool pending, closing, initializing = true;
    private bool capturing, waitingForRelease;
    private string? captured;

    public MainForm(SettingsStore? store = null, IClickSender? sender = null)
    {
        this.store = store ?? new();
        engine = new(sender ?? new WindowsClickSender());
        settings = this.store.Load(out string? warning);
        string folder = Path.GetDirectoryName(Path.GetFullPath(this.store.FilePath))!;
        toolStore = new(Path.Combine(folder, "automation-settings.json")); preferences = toolStore.Load();
        var storage = new AutomationStore(Path.Combine(folder, "macros"));
        emergencyHotkey = new(Handle, 30000);
        foreach (string candidate in new[] { "F9", "F10", "F11", "F8" })
        {
            if (HotkeySpec.Parse(candidate).Key == HotkeySpec.Parse(settings.Hotkey).Key) continue;
            try { emergencyHotkey.Set(candidate); emergencyKey = (int)HotkeySpec.Parse(candidate).Key; break; }
            catch (System.ComponentModel.Win32Exception) { }
        }
        if (emergencyKey == 0) { emergencyKey = (int)Keys.F9; warning = "Parada global no disponible. Cierra las otras aplicaciones y vuelve a abrir."; }
        player = new(new WindowsActionSender()); recorder = new(emergencyKey);
        recorderPage = new(recorder, player, coordinator, storage, emergencyKey, preferences, (moves, rate, cycles) =>
        { preferences = preferences with { IncludeMoves = moves, RecordingSpeed = rate, RecordingCycles = cycles }; SavePreferences(); });
        macroPage = new(player, coordinator, storage, emergencyKey, preferences, (rate, cycles) =>
        { preferences = preferences with { MacroSpeed = rate, MacroCycles = cycles }; SavePreferences(); }, id =>
        { preferences = preferences with { LastMacro = id }; SavePreferences(); });
        Text = Branding.WindowTitle;
        Icon = Branding.LoadIcon();
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new(760, 700);
        MinimumSize = new(710, 670);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = ToolStyle.BodyFont;
        BackColor = ToolStyle.Canvas;
        ForeColor = ToolStyle.Text;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(ToolStyle.Large), ColumnCount = 1, AutoScroll = true };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        void Add(Control c) { root.RowStyles.Add(new(SizeType.AutoSize)); c.Margin = new(0, 0, 0, 12); root.Controls.Add(c, 0, root.RowCount++); }
        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        header.ColumnStyles.Add(new(SizeType.Percent, 65)); header.ColumnStyles.Add(new(SizeType.Percent, 35));
        header.Controls.Add(new Label { Text = "AutoClick", AutoSize = true, Font = ToolStyle.HeadingFont, ForeColor = ToolStyle.Text }, 0, 0);
        header.Controls.Add(status, 1, 0);
        Add(header);
        var tools = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        tools.ColumnStyles.Add(new(SizeType.Percent, 47)); tools.ColumnStyles.Add(new(SizeType.Percent, 53));
        var left = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1 };
        left.ColumnStyles.Add(new(SizeType.Percent, 100));
        left.Controls.Add(mouse, 0, 0); left.Controls.Add(side, 0, 1);
        tools.Controls.Add(left, 0, 0);
        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new(10, 14, 0, 0) };
        fields.Controls.Add(new Label { Text = "Velocidad", AutoSize = true, Margin = new(0, 0, 0, 7) });
        var speedRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new(0) };
        speedRow.Controls.Add(speed); speedRow.Controls.Add(new Label { Text = "clics/s", AutoSize = true, Margin = new(6, 5, 0, 0) });
        fields.Controls.Add(speedRow);
        interval.Margin = new(0, 5, 0, 21); fields.Controls.Add(interval);
        fields.Controls.Add(new Label { Text = "Atajo global", AutoSize = true, Margin = new(0, 0, 0, 7) });
        shortcut.Width = 172; shortcut.Height = 37; shortcut.AutoSize = false; shortcut.Margin = new(0);
        fields.Controls.Add(shortcut);
        tools.Controls.Add(fields, 1, 0); Add(tools);
        var counterRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
        counterRow.ColumnStyles.Add(new(SizeType.AutoSize)); counterRow.ColumnStyles.Add(new(SizeType.Percent, 100)); counterRow.ColumnStyles.Add(new(SizeType.AutoSize));
        counterRow.Controls.Add(count, 0, 0);
        counterRow.Controls.Add(new Label { Text = "Clics", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new(8, 0, 0, 0) }, 1, 0);
        reset.Font = ToolStyle.SmallFont; reset.Anchor = AnchorStyles.Right; counterRow.Controls.Add(reset, 2, 0);
        Add(counterRow);
        toggle.AutoSize = false; toggle.Height = 42; toggle.Dock = DockStyle.Top;
        ToolStyle.Primary(toggle);
        Add(toggle); Add(notice);
        var tabs = new ToolNavigation { Name = "Tools", Dock = DockStyle.Fill, AccessibleName = "Herramientas" };
        var autoPage = new TabPage("AutoClick") { BackColor = BackColor };
        root.Dock = DockStyle.None;
        autoPage.Controls.Add(root);
        autoPage.Resize += (_, _) =>
        {
            int width = (int)(420 * DeviceDpi / 96f), height = (int)(460 * DeviceDpi / 96f);
            root.SetBounds(Math.Max(0, (autoPage.ClientSize.Width - width) / 2), 12, width, height);
        };
        tabs.TabPages.Add(autoPage);
        var recordingTab = new TabPage("Grabador") { BackColor = BackColor }; recordingTab.Controls.Add(recorderPage); tabs.TabPages.Add(recordingTab);
        var macroTab = new TabPage("Macros") { BackColor = BackColor }; macroTab.Controls.Add(macroPage); tabs.TabPages.Add(macroTab);
        var stopAll = ToolStyle.Button($"Detener todo · {(Keys)emergencyKey}", "EmergencyStop");
        stopAll.Click += (_, _) => { coordinator.StopAll(); UpdateState(); };
        var footer = new TableLayoutPanel { Name = "DeveloperFooter", Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, Padding = new(ToolStyle.Gap, 0, ToolStyle.Gap, 0), BackColor = ToolStyle.Surface };
        footer.ColumnStyles.Add(new(SizeType.AutoSize)); footer.ColumnStyles.Add(new(SizeType.Percent, 100)); footer.ColumnStyles.Add(new(SizeType.AutoSize));
        stopAll.Anchor = AnchorStyles.Left;
        footer.Controls.Add(stopAll, 0, 0); footer.Controls.Add(overall, 1, 0);
        footer.Controls.Add(new Label { Name = "AuthorCredit", Text = Branding.Credit, AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = ToolStyle.Muted, Font = ToolStyle.SmallFont, Margin = new(ToolStyle.Gap) }, 2, 0);
        var brand = new TableLayoutPanel { Name = "ProductHeader", Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Padding = new(ToolStyle.Large, ToolStyle.Gap, ToolStyle.Large, ToolStyle.Gap), BackColor = ToolStyle.Surface };
        brand.ColumnStyles.Add(new(SizeType.AutoSize)); brand.ColumnStyles.Add(new(SizeType.Percent, 100));
        var productLogo = new BrandMark { Name = "ProductLogo", Size = new(56, 56), Anchor = AnchorStyles.None, Margin = new(0, 0, ToolStyle.Gap, 0) };
        brand.Controls.Add(productLogo, 0, 0); brand.SetRowSpan(productLogo, 2);
        brand.Controls.Add(new Label { Name = "ProductName", Text = Branding.ProductName, AutoSize = true, Font = ToolStyle.HeadingFont, ForeColor = ToolStyle.Text, Margin = new(0, 0, 0, ToolStyle.Small) }, 1, 0);
        brand.Controls.Add(new Label { Name = "ProductDescription", Text = Branding.Description, AutoSize = true, ForeColor = ToolStyle.Muted, Margin = new(0) }, 1, 1);
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        shell.ColumnStyles.Add(new(SizeType.Percent, 100));
        shell.RowStyles.Add(new(SizeType.AutoSize)); shell.RowStyles.Add(new(SizeType.Percent, 100)); shell.RowStyles.Add(new(SizeType.AutoSize));
        shell.Controls.Add(brand, 0, 0); shell.Controls.Add(tabs, 0, 1); shell.Controls.Add(footer, 0, 2);
        Controls.Add(shell);
        ToolStyle.Inputs(this);
        tips.SetToolTip(speed, "0,1–100 clics/s. La velocidad real depende del equipo.");
        tips.SetToolTip(mouse, "Selecciona un lado o usa las flechas ← y →.");
        tips.SetToolTip(count, "Clics enviados; la recepción depende de la aplicación de destino.");
        tips.SetToolTip(toggle, "El botón inicia en 3 segundos. El atajo inicia al instante.");
        tips.SetToolTip(shortcut, "Pulsa para capturar. Escape cancela.");
        speed.Value = settings.ClicksPerSecond;
        mouse.SelectedButton = settings.Button;
        shortcut.Text = HotkeySpec.Display(settings.Hotkey);
        speed.ValueChanged += (_, _) => { UpdateInterval(); SaveControls(); };
        mouse.SelectionChanged += (_, _) => { UpdateSide(); SaveControls(); };
        shortcut.Click += (_, _) => BeginCapture();
        toggle.Click += (_, _) => Toggle(true);
        reset.Click += (_, _) => { engine.ResetCount(); UpdateState(); };
        refresh.Tick += (_, _) => Tick();
        Deactivate += (_, _) => { if (capturing && !waitingForRelease) CancelCapture(); };
        Shown += (_, _) =>
        {
            hotkey = new(Handle);
            try { hotkey.Set(settings.Hotkey); } catch (Exception) { warning = "Atajo no disponible. Elige otro."; }
            try { emergencyMonitor = new(emergencyKey, () => BeginInvoke((Action)(() => { if (!closing) { coordinator.StopAll(); UpdateState(); } }))); }
            catch (System.ComponentModel.Win32Exception) { warning = "Parada con modificadores no disponible; usa el botón Detener todo."; }
            notice.Text = warning ?? "";
            refresh.Start();
        };
        initializing = false;
        UpdateSide(); UpdateInterval(); UpdateState();
    }
    private void UpdateInterval() => interval.Text = $"{1000m / speed.Value:0.##} ms";
    private void UpdateSide() => side.Text = mouse.SelectedButton == ClickButton.Left ? "Izquierdo" : "Derecho";
    private void SavePreferences()
    {
        try { toolStore.Save(preferences); preferencesWarning = null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { preferencesWarning = "No se pudieron guardar las preferencias."; }
    }
    private bool SaveControls()
    {
        if (initializing || closing) return true;
        var value = settings with { ClicksPerSecond = speed.Value, Button = mouse.SelectedButton };
        try { store.Save(value); settings = value; notice.Text = ""; return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { notice.Text = "No se pudieron guardar los ajustes."; return false; }
    }
    private void Stop() { pending = false; countdown.Reset(); engine.Stop(); coordinator.End("AutoClick"); }
    private void Toggle(bool fromButton)
    {
        if (closing || capturing) return;
        if (pending || engine.IsRunning) Stop();
        else if (SaveControls() && coordinator.TryBegin("AutoClick", Stop))
        {
            if (fromButton) { pending = true; countdown.Restart(); }
            else engine.Start(settings);
        }
        UpdateState();
    }
    private void BeginCapture()
    {
        if (capturing || closing) return;
        coordinator.StopAll(); Stop();
        coordinator.TryBegin("Atajo", AbortCapture);
        capturing = true; waitingForRelease = false; captured = null;
        if (hotkey != null) hotkey.Suspended = true;
        try { keyboardCapture = new(); keyboardCapture.KeyPressed += CaptureKey; }
        catch (System.ComponentModel.Win32Exception)
        {
            capturing = false;
            coordinator.End("Atajo");
            if (hotkey != null) hotkey.Suspended = false;
            notice.Text = "No se pudo capturar el teclado.";
            UpdateState(); return;
        }
        shortcut.Text = "Presiona tu atajo…";
        notice.Text = "Escape cancela";
        shortcut.Focus(); UpdateState();
    }
    private void CaptureKey(Keys keys)
    {
        if (waitingForRelease) return;
        var key = keys & Keys.KeyCode;
        if (key == Keys.Escape) { CancelCapture(); return; }
        if (key is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or Keys.Menu or Keys.LMenu or Keys.RMenu or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey)
        { notice.Text = "Añade una tecla a los modificadores."; return; }
        try
        {
            captured = HotkeySpec.FromKeys(keys);
            if (HotkeySpec.Parse(captured).Key == (Keys)emergencyKey)
                throw new ArgumentException($"{(Keys)emergencyKey} está reservado para detener todo.");
            waitingForRelease = true;
            shortcut.Text = HotkeySpec.Display(captured);
            notice.Text = "Suelta las teclas…";
        }
        catch (ArgumentException ex) { notice.Text = ex.Message; }
    }
    private void CancelCapture()
    {
        captured = null; waitingForRelease = true;
        shortcut.Text = HotkeySpec.Display(settings.Hotkey);
        notice.Text = "Captura cancelada";
    }
    private void CompleteCapture()
    {
        keyboardCapture?.Dispose(); keyboardCapture = null;
        string previous = settings.Hotkey;
        if (captured != null)
        {
            try
            {
                hotkey?.Set(captured);
                var next = settings with { Hotkey = captured };
                store.Save(next); settings = next; notice.Text = "";
            }
            catch (System.ComponentModel.Win32Exception) { notice.Text = "Atajo ocupado. Se conserva el anterior."; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try { hotkey?.Set(previous); } catch (System.ComponentModel.Win32Exception) { notice.Text = "Atajo no disponible. Elige otro."; }
                notice.Text = "No se pudo guardar el atajo.";
            }
        }
        captured = null; capturing = false; waitingForRelease = false;
        coordinator.End("Atajo");
        if (hotkey != null) hotkey.Suspended = false;
        shortcut.Text = HotkeySpec.Display(settings.Hotkey); UpdateState();
    }
    private void AbortCapture()
    {
        keyboardCapture?.Dispose(); keyboardCapture = null; captured = null; capturing = waitingForRelease = false;
        if (hotkey != null) hotkey.Suspended = false;
        shortcut.Text = HotkeySpec.Display(settings.Hotkey); notice.Text = "Captura cancelada"; coordinator.End("Atajo");
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (capturing) { CaptureKey(keyData); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    private void Tick()
    {
        if (capturing && waitingForRelease && keyboardCapture?.AllReleased == true) CompleteCapture();
        if (pending && countdown.Elapsed.TotalSeconds >= 3) { pending = false; countdown.Reset(); engine.Start(settings); }
        string? error = engine.TakeError();
        if (error != null) { Stop(); notice.Text = "Windows no pudo enviar el clic. AutoClick detenido."; }
        recorderPage.RefreshState(); macroPage.RefreshState();
        UpdateState();
    }
    private void UpdateState()
    {
        bool running = engine.IsRunning;
        bool busy = running || pending;
        status.Text = running ? "Activo" : "Detenido";
        ToolStyle.State(status, running);
        toggle.Text = busy ? "Detener" : "Iniciar";
        count.Text = engine.Count.ToString("N0");
        speed.Enabled = mouse.Enabled = reset.Enabled = !busy && !capturing;
        toggle.Enabled = !capturing && coordinator.Owner is null or "AutoClick";
        overall.Text = coordinator.Owner switch
        {
            "AutoClick" => pending ? "AutoClick · Preparando" : "AutoClick · Activo",
            "Grabación" => recorder.Buffer.State == RecordingState.Paused ? "Grabación · Pausado" : "Grabación · Grabando",
            "Grabador" or "Macros" => $"{coordinator.Owner} · {(player.IsPaused ? "Pausado" : "Reproduciendo")} · {player.Count:N0} pasos",
            "Atajo" => "Capturando atajo", _ => preferencesWarning ?? "Listo"
        };
        if (pending) notice.Text = $"Inicio en {Math.Max(1, 3 - (int)countdown.Elapsed.TotalSeconds)}…";
        else if (!capturing && notice.Text.StartsWith("Inicio en")) notice.Text = "";
    }
    protected override void WndProc(ref Message m)
    {
        // While capturing, ignore even hotkey messages queued before the hook started.
        if (m.Msg == GlobalHotkey.Message && hotkey?.Matches(m.WParam) == true) Toggle(false);
        if (m.Msg == GlobalHotkey.Message && emergencyHotkey?.Matches(m.WParam) == true) { coordinator.StopAll(); UpdateState(); }
        base.WndProc(ref m);
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        closing = true; refresh.Stop(); coordinator.StopAll(); Stop(); player.Stop(); recorder.Stop(); keyboardCapture?.Dispose(); emergencyMonitor?.Dispose(); hotkey?.Dispose(); emergencyHotkey.Dispose(); base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { refresh.Dispose(); tips.Dispose(); keyboardCapture?.Dispose(); emergencyMonitor?.Dispose(); hotkey?.Dispose(); emergencyHotkey?.Dispose(); engine.Dispose(); player?.Dispose(); recorder?.Dispose(); Icon?.Dispose(); }
        base.Dispose(disposing);
    }
}
