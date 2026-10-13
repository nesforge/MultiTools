using System.Text.Json;

namespace ClickMoves.Configuration;

public sealed class SettingsStore
{
    public string FilePath { get; }
    public SettingsStore(string? path = null) => FilePath = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickMoves", "settings.json");

    public AppSettings Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(FilePath)) return new();
        try
        {
            var settings = ConfigurationJson.Read<AppSettings>(FilePath)
                ?? throw new ArgumentException("Configuración vacía.");
            settings.Validate();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            warning = "No se pudo leer la configuración guardada. Se usarán los valores iniciales.";
            return new();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}
