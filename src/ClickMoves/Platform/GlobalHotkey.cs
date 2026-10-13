using System.ComponentModel;
using System.Runtime.InteropServices;
using ClickMoves.Configuration;

namespace ClickMoves.Platform;

public sealed class GlobalHotkey : IDisposable
{
    public const int Message = 0x0312;
    private readonly IntPtr window;
    private int activeId;
    private int lastId;
    private HotkeySpec? current;
    public bool Suspended { get; set; }
    public HotkeySpec? Current => current;
    public GlobalHotkey(IntPtr window, int idBase = 0) { this.window = window; lastId = idBase; }
    public bool Owns(IntPtr id) => activeId != 0 && id.ToInt32() == activeId;
    public bool Matches(IntPtr id) => !Suspended && Owns(id);
    public void Set(string value)
    {
        var spec = HotkeySpec.Parse(value);
        if (spec == current) return;
        // Register the replacement first: if it fails, the previous shortcut remains available.
        int next = ++lastId;
        if (!NativeMethods.RegisterHotKey(window, next, spec.Modifiers | 0x4000, (uint)spec.Key))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "El atajo está ocupado o Windows no permite registrarlo. Elige otro.");
        if (activeId != 0) NativeMethods.UnregisterHotKey(window, activeId);
        activeId = next;
        current = spec;
    }
    public void Dispose()
    {
        if (activeId != 0) NativeMethods.UnregisterHotKey(window, activeId);
        activeId = 0;
        current = null;
    }
}
