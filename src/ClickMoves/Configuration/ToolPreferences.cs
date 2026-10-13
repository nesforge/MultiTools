using System.Text.Json;

namespace ClickMoves.Configuration;

public sealed record ToolPreferences
{
    public bool IncludeMoves { get; init; } = true;
    public decimal RecordingSpeed { get; init; } = 1;
    public decimal MacroSpeed { get; init; } = 1;
    public int RecordingCycles { get; init; } = 1;
    public int MacroCycles { get; init; } = 1;
    public Guid? LastMacro { get; init; }
    public void Validate()
    {
        if (RecordingSpeed is < 0.1m or > 10m || MacroSpeed is < 0.1m or > 10m || RecordingCycles is < 1 or > 1000 || MacroCycles is < 1 or > 1000)
            throw new ArgumentException("Preferencias de reproducción inválidas.");
    }
}
public sealed class ToolPreferencesStore(string path)
{
    public ToolPreferences Load()
    {
        if (!File.Exists(path)) return new();
        try { var value = ConfigurationJson.Read<ToolPreferences>(path) ?? new(); value.Validate(); return value; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return new(); }
    }
    public void Save(ToolPreferences value)
    {
        value.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value)); File.Move(temp, path, true);
    }
}
