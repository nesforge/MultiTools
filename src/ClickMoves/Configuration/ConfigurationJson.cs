using System.Text.Json;

namespace ClickMoves.Configuration;

internal static class ConfigurationJson
{
    // Settings are small; check the open stream before allocating JSON buffers.
    internal const int MaxFileBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Options = new() { MaxDepth = 8 };

    internal static T? Read<T>(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxFileBytes) throw new ArgumentException("Configuración demasiado grande.");
        return JsonSerializer.Deserialize<T>(stream, Options);
    }
}
