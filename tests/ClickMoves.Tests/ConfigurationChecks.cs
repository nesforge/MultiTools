using ClickMoves.Configuration;

internal static class ConfigurationChecks
{
    public static void Run()
    {
        string folder = Path.Combine(Environment.CurrentDirectory, "TestResults", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var settings = new SettingsStore(Path.Combine(folder, "settings.json"));
        var value = new AppSettings { ClicksPerSecond = 25.5m, Button = ClickButton.Right, Hotkey = "Ctrl+Alt+K" };
        settings.Save(value);
        AutomationChecks.Assert(settings.Load(out var warning) == value && warning == null, "Configuración válida conservada");
        string validJson = File.ReadAllText(settings.FilePath);
        File.WriteAllText(settings.FilePath, new string(' ', 64 * 1024) + validJson);
        AutomationChecks.Assert(settings.Load(out warning) == new AppSettings() && warning != null, "Configuración sobredimensionada rechazada aunque el JSON sea válido");
        File.WriteAllText(settings.FilePath, "{\"extra\":{" + string.Concat(Enumerable.Repeat("\"extra\":{", 10)) + "\"value\":1" + new string('}', 12));
        AutomationChecks.Assert(settings.Load(out warning) == new AppSettings() && warning != null, "Profundidad limitada");
        using (var locked = new FileStream(settings.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            AutomationChecks.Assert(settings.Load(out warning) == new AppSettings() && warning != null && !warning.Contains(folder), "Error sin ruta privada");
        string path = Path.Combine(folder, "preferences.json");
        var preferences = new ToolPreferencesStore(path);
        preferences.Save(new() { RecordingSpeed = 2, MacroCycles = 3 });
        validJson = File.ReadAllText(path);
        File.WriteAllText(path, new string(' ', 64 * 1024) + validJson);
        AutomationChecks.Assert(preferences.Load() == new ToolPreferences(), "Preferencias sobredimensionadas recuperan valores iniciales");
    }
}
