using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ClickMoves.Platform;

/// <summary>Only recognizes the emergency key, even while a macro holds modifiers.</summary>
public sealed class EmergencyMonitor : IDisposable
{
    private readonly HookProc callback;
    private readonly int key;
    private readonly Action notify;
    private IntPtr hook;
    private bool held;
    public EmergencyMonitor(int key, Action notify)
    {
        this.key = key; this.notify = notify; callback = Keyboard;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    private IntPtr Keyboard(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && Marshal.ReadInt32(data) == key)
        {
            var value = Marshal.PtrToStructure<KeyEvent>(data);
            if (value.Extra != WindowsActionSender.InputTag)
            {
                int msg = message.ToInt32();
                if (msg is 0x100 or 0x104) { if (!held) { held = true; notify(); } }
                else if (msg is 0x101 or 0x105) held = false;
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }
    public void Dispose() { if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook); hook = IntPtr.Zero; GC.KeepAlive(callback); }
    [StructLayout(LayoutKind.Sequential)] private struct KeyEvent { public uint Key, Scan, Flags, Time; public UIntPtr Extra; }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}
