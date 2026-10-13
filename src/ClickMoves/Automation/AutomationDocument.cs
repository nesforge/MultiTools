namespace ClickMoves.Automation;

public enum DocumentKind { Recording, Macro }
public enum ActionKind { LeftClick, RightClick, LeftDown, LeftUp, RightDown, RightUp, MouseMove, KeyDown, KeyUp, Wait }

public sealed record ActionStep
{
    [System.Text.Json.Serialization.JsonRequired] public ActionKind Kind { get; init; }
    [System.Text.Json.Serialization.JsonRequired] public int DelayMs { get; init; }
    public int? X { get; init; }
    public int? Y { get; init; }
    public int? KeyCode { get; init; }
    public bool Extended { get; init; }

    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || DelayMs is < 0 or > 3_600_000)
            throw new ArgumentException("Acción o espera inválida (0–3.600.000 ms).");
        if (X.HasValue != Y.HasValue || X is < -1_000_000 or > 1_000_000 || Y is < -1_000_000 or > 1_000_000)
            throw new ArgumentException("Coordenadas inválidas.");
        bool keyboard = Kind is ActionKind.KeyDown or ActionKind.KeyUp;
        if (keyboard && (KeyCode is null or < 8 or > 254 || X != null))
            throw new ArgumentException("La acción de teclado necesita una tecla válida.");
        if (!keyboard && (KeyCode != null || Extended)) throw new ArgumentException("Parámetros de tecla inesperados.");
        if (Kind == ActionKind.MouseMove && X == null) throw new ArgumentException("El movimiento necesita coordenadas.");
        if (Kind == ActionKind.Wait && X != null) throw new ArgumentException("La espera no admite coordenadas.");
    }
    public string Label => Kind switch
    {
        ActionKind.LeftClick => "Clic izquierdo", ActionKind.RightClick => "Clic derecho",
        ActionKind.LeftDown => "Presionar izquierdo", ActionKind.LeftUp => "Soltar izquierdo",
        ActionKind.RightDown => "Presionar derecho", ActionKind.RightUp => "Soltar derecho",
        ActionKind.MouseMove => "Mover mouse", ActionKind.KeyDown => "Presionar tecla",
        ActionKind.KeyUp => "Soltar tecla", _ => "Esperar"
    };
    public string Parameters => KeyCode.HasValue ? $"{KeyLabel(KeyCode.Value)}{(Extended ? " · extendida" : "")}" : X.HasValue ? $"X {X}, Y {Y}" : Kind == ActionKind.Wait ? "—" : "Cursor actual";
    public static string KeyLabel(int code) => (Keys)code switch
    {
        Keys.ControlKey => "Ctrl", Keys.LControlKey => "Ctrl izquierdo", Keys.RControlKey => "Ctrl derecho",
        Keys.ShiftKey => "Mayús", Keys.LShiftKey => "Mayús izquierda", Keys.RShiftKey => "Mayús derecha",
        Keys.Menu => "Alt", Keys.LMenu => "Alt izquierdo", Keys.RMenu => "Alt derecho",
        Keys.Space => "Espacio", Keys.Back => "Retroceso", Keys.Delete => "Suprimir", Keys.Insert => "Insertar",
        Keys.Home => "Inicio", Keys.End => "Fin", Keys.PageUp => "Re Pág", Keys.PageDown => "Av Pág", Keys.Return => "Enter",
        Keys.Left => "←", Keys.Right => "→", Keys.Up => "↑", Keys.Down => "↓",
        Keys.LWin => "Win izquierdo", Keys.RWin => "Win derecho",
        >= Keys.D0 and <= Keys.D9 => (code - (int)Keys.D0).ToString(), _ => ((Keys)code).ToString()
    };
}

public sealed record AutomationDocument
{
    public const int MaxSteps = 100_000;
    [System.Text.Json.Serialization.JsonRequired] public int Version { get; init; } = 1;
    [System.Text.Json.Serialization.JsonRequired] public DocumentKind Kind { get; init; }
    [System.Text.Json.Serialization.JsonRequired] public string Name { get; init; } = "Sin nombre";
    [System.Text.Json.Serialization.JsonRequired] public List<ActionStep> Steps { get; init; } = [];
    public void Validate()
    {
        if (Version != 1) throw new ArgumentException("Versión de archivo no admitida.");
        if (!Enum.IsDefined(Kind) || string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || Name.Any(char.IsControl))
            throw new ArgumentException("Nombre o tipo de documento inválido.");
        if (Steps == null || Steps.Count > MaxSteps) throw new ArgumentException("La secuencia admite hasta 100.000 pasos.");
        foreach (var step in Steps) { if (step == null) throw new ArgumentException("Paso vacío."); step.Validate(); }
        if (Steps.Sum(s => (long)s.DelayMs) > 86_400_000) throw new ArgumentException("La secuencia supera 24 horas de espera.");
    }
}
