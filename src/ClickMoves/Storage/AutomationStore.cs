using System.Text.Json;
using System.Text.Json.Serialization;
using ClickMoves.Automation;

namespace ClickMoves.Storage;

public sealed class AutomationStore
{
    public const int MaxFileBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public string LibraryFolder { get; }
    public AutomationStore(string? folder = null) => LibraryFolder = folder ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickMoves", "macros");
    public AutomationDocument Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxFileBytes) throw new ArgumentException("El archivo supera 16 MB.");
        using var data = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 8 });
        CheckDuplicates(data.RootElement);
        var result = data.Deserialize<AutomationDocument>(Options) ?? throw new ArgumentException("Archivo vacío.");
        result.Validate();
        return result;
    }
    private static void CheckDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in value.EnumerateObject())
            { if (!names.Add(p.Name)) throw new ArgumentException("El archivo contiene campos duplicados."); CheckDuplicates(p.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) CheckDuplicates(item);
    }
    public void Save(string path, AutomationDocument document)
    {
        document.Validate();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, Options);
        if (bytes.Length > MaxFileBytes) throw new ArgumentException("La secuencia supera 16 MB.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public Guid SaveMacro(AutomationDocument document, Guid? id = null)
    {
        if (document.Kind != DocumentKind.Macro) throw new ArgumentException("Se esperaba una macro.");
        Guid key = id ?? Guid.NewGuid();
        Save(Path.Combine(LibraryFolder, key.ToString("N") + ".json"), document);
        return key;
    }
    public List<(Guid Id, AutomationDocument Document)> ListMacros(out bool skipped)
    {
        skipped = false;
        var result = new List<(Guid, AutomationDocument)>();
        if (!Directory.Exists(LibraryFolder)) return result;
        foreach (string file in Directory.EnumerateFiles(LibraryFolder, "*.json").Take(1000))
        {
            try
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out Guid id)) { skipped = true; continue; }
                var doc = Load(file);
                if (doc.Kind == DocumentKind.Macro) result.Add((id, doc)); else skipped = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { skipped = true; }
        }
        return result.OrderBy(x => x.Item2.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
