using System.ComponentModel;
using System.Runtime.InteropServices;
using ClickMoves.Automation;

namespace ClickMoves.Platform;

/// <summary>Hooks are installed only on explicit recording. Callbacks do no file/UI work.</summary>
public sealed class WindowsRecorder : IDisposable
{
    public RecordingBuffer Buffer { get; } = new();
    private readonly HookProc mouseCallback, keyboardCallback;
    private IntPtr mouseHook, keyboardHook;
    private readonly int emergencyKey;
    private readonly bool allowInjected;
    private readonly bool onlyTaggedForTests;
    public WindowsRecorder(int emergencyKey, bool allowInjected = false, bool onlyTaggedForTests = false)
    { this.emergencyKey = emergencyKey; this.allowInjected = allowInjected; this.onlyTaggedForTests = onlyTaggedForTests; mouseCallback = Mouse; keyboardCallback = Keyboard; }
    public void Start(bool includeMoves)
    {
        DisposeHooks(); Buffer.Begin(includeMoves);
        mouseHook = SetWindowsHookEx(14, mouseCallback, GetModuleHandle(null), 0);
        keyboardHook = SetWindowsHookEx(13, keyboardCallback, GetModuleHandle(null), 0);
        if (mouseHook == IntPtr.Zero || keyboardHook == IntPtr.Zero)
        { int error = Marshal.GetLastWin32Error(); Stop(); throw new Win32Exception(error, "No se pudo iniciar la grabación."); }
    }
    private bool OwnWindow(IntPtr window)
    { GetWindowThreadProcessId(window, out uint pid); return pid == Environment.ProcessId; }
    private IntPtr Mouse(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && Buffer.State == RecordingState.Recording)
        {
            var input = Marshal.PtrToStructure<MouseEvent>(data);
            if ((!onlyTaggedForTests || input.Extra == WindowsActionSender.InputTag) && (allowInjected || (input.Flags & 1) == 0) && !OwnWindow(WindowFromPoint(input.Point)))
            {
                ActionKind? kind = message.ToInt32() switch
                { 0x200 => ActionKind.MouseMove, 0x201 => ActionKind.LeftDown, 0x202 => ActionKind.LeftUp, 0x204 => ActionKind.RightDown, 0x205 => ActionKind.RightUp, _ => null };
                if (kind != null) Buffer.Observe(new() { Kind = kind.Value, X = input.Point.X, Y = input.Point.Y });
            }
        }
        return CallNextHookEx(mouseHook, code, message, data);
    }
    private IntPtr Keyboard(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && Buffer.State == RecordingState.Recording)
        {
            var input = Marshal.PtrToStructure<KeyEvent>(data);
            int msg = message.ToInt32();
            if ((!onlyTaggedForTests || input.Extra == WindowsActionSender.InputTag) && input.Key != emergencyKey && input.Key is >= 8 and <= 254 && (allowInjected || (input.Flags & 0x10) == 0) && !OwnWindow(GetForegroundWindow()))
            {
                ActionKind? kind = msg is 0x100 or 0x104 ? ActionKind.KeyDown : msg is 0x101 or 0x105 ? ActionKind.KeyUp : null;
                if (kind != null) Buffer.Observe(new() { Kind = kind.Value, KeyCode = (int)input.Key, Extended = (input.Flags & 1) != 0 });
            }
        }
        return CallNextHookEx(keyboardHook, code, message, data);
    }
    public void Pause() => Buffer.Pause();
    public void Resume() => Buffer.Resume();
    public void Stop() { DisposeHooks(); Buffer.Stop(); }
    private void DisposeHooks()
    {
        if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);
        if (keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(keyboardHook);
        mouseHook = keyboardHook = IntPtr.Zero;
    }
    public void Dispose() { Stop(); GC.KeepAlive(mouseCallback); GC.KeepAlive(keyboardCallback); }
    [StructLayout(LayoutKind.Sequential)] private struct MouseEvent { public Point Point; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyEvent { public uint Key, Scan, Flags, Time; public UIntPtr Extra; }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
}
