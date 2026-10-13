using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ClickMoves.Platform;

/// <summary>Temporary hook observes even shortcuts reserved by another application.
/// Swallow capture input and track physical releases, including auto-repeat.</summary>
public sealed class HotkeyCapture : IDisposable
{
    private readonly HashSet<Keys> pressed = [];
    private readonly HashSet<Keys> alreadyDown = [];
    private readonly HookProc callback;
    private IntPtr hook;
    public event Action<Keys>? KeyPressed;
    public bool AllReleased => pressed.Count == 0;
    public HotkeyCapture()
    {
        // Generic modifier states duplicate their left/right physical states.
        for (int key = 8; key <= 254; key++)
            if (key is not (16 or 17 or 18) && (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0)
            { pressed.Add((Keys)key); alreadyDown.Add((Keys)key); }
        callback = OnKeyboard;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            int msg = message.ToInt32();
            var key = (Keys)Marshal.ReadInt32(data);
            if (msg is 0x0100 or 0x0104)
            {
                if (pressed.Add(key))
                {
                    Keys keys = key;
                    if (Has(Keys.ControlKey, Keys.LControlKey, Keys.RControlKey)) keys |= Keys.Control;
                    if (Has(Keys.Menu, Keys.LMenu, Keys.RMenu)) keys |= Keys.Alt;
                    if (Has(Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey)) keys |= Keys.Shift;
                    // Win combinations are not supported by the configuration contract.
                    KeyPressed?.Invoke(pressed.Contains(Keys.LWin) || pressed.Contains(Keys.RWin) ? Keys.LWin : keys);
                }
                return new IntPtr(1);
            }
            if (msg is 0x0101 or 0x0105)
            {
                pressed.Remove(key);
                // Release any key Windows already saw before capture began.
                return alreadyDown.Remove(key) ? CallNextHookEx(hook, code, message, data) : new IntPtr(1);
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }
    private bool Has(Keys generic, Keys left, Keys right) => pressed.Contains(generic) || pressed.Contains(left) || pressed.Contains(right);
    public void Dispose()
    {
        if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
        GC.KeepAlive(callback);
    }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hook, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}
