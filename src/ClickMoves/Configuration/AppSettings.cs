namespace ClickMoves.Configuration;

public enum ClickButton { Left, Right }

public sealed record AppSettings
{
    public const decimal MinCps = 0.1m;
    public const decimal MaxCps = 100m;
    public decimal ClicksPerSecond { get; init; } = 10m;
    public ClickButton Button { get; init; } = ClickButton.Left;
    public string Hotkey { get; init; } = "F6";

    public void Validate()
    {
        if (ClicksPerSecond < MinCps || ClicksPerSecond > MaxCps)
            throw new ArgumentException("La velocidad debe estar entre 0,1 y 100 clics por segundo.");
        if (!Enum.IsDefined(Button)) throw new ArgumentException("El tipo de clic no es válido.");
        HotkeySpec.Parse(Hotkey);
    }
}

public sealed record HotkeySpec(uint Modifiers, Keys Key)
{
    public static HotkeySpec Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Presiona una tecla para el atajo.");
        string[] parts = value.Split('+', StringSplitOptions.TrimEntries);
        uint modifiers = 0;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            uint modifier = parts[i].ToUpperInvariant() switch
            {
                "ALT" => 1, "CTRL" => 2, "SHIFT" => 4,
                _ => throw new ArgumentException("Usa Ctrl, Alt o Shift como modificadores.")
            };
            if ((modifiers & modifier) != 0) throw new ArgumentException("No repitas un modificador en el atajo.");
            modifiers |= modifier;
        }
        string token = parts[^1].ToUpperInvariant();
        Keys key;
        if (token.Length == 1 && token[0] is >= 'A' and <= 'Z') key = (Keys)token[0];
        else if (token.Length == 1 && token[0] is >= '0' and <= '9') key = Keys.D0 + (token[0] - '0');
        else if (token.StartsWith('F') && int.TryParse(token[1..], out int f) && f is >= 1 and <= 24) key = Keys.F1 + f - 1;
        else throw new ArgumentException("Usa F1–F24 o una letra/número con Ctrl o Alt. F12 está reservado por Windows.");
        if (key == Keys.F12) throw new ArgumentException("F12 está reservado por Windows. Elige otro atajo.");
        return new(modifiers, key);
    }

    public static string FromKeys(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        string token = key is >= Keys.A and <= Keys.Z ? key.ToString()
            : key is >= Keys.D0 and <= Keys.D9 ? ((int)(key - Keys.D0)).ToString()
            : key is >= Keys.F1 and <= Keys.F24 ? key.ToString()
            : throw new ArgumentException("Elige una letra, un número o F1–F24 (excepto F12).");
        var parts = new List<string>();
        if ((keyData & Keys.Control) != 0) parts.Add("Ctrl");
        if ((keyData & Keys.Alt) != 0) parts.Add("Alt");
        if ((keyData & Keys.Shift) != 0) parts.Add("Shift");
        parts.Add(token);
        string value = string.Join("+", parts);
        Parse(value);
        return value;
    }
    public static string Display(string value) => value.Replace("+", " + ");
}
