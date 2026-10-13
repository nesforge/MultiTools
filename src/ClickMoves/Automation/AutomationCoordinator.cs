namespace ClickMoves.Automation;

/// <summary>UI-thread coordinator reserves even countdowns and shortcut capture.</summary>
public sealed class AutomationCoordinator
{
    public string? Owner { get; private set; }
    private Action? stop;
    public bool TryBegin(string owner, Action stopAction)
    {
        if (Owner != null) return false;
        Owner = owner; stop = stopAction; return true;
    }
    public void End(string owner) { if (Owner == owner) { Owner = null; stop = null; } }
    public void StopAll()
    {
        var action = stop;
        stop = null;
        try { action?.Invoke(); }
        finally { Owner = null; }
    }
}
