using System.ComponentModel;
using System.Runtime.InteropServices;
using ClickMoves.Automation;
using ClickMoves.Configuration;

namespace ClickMoves.Platform;

public sealed class WindowsActionSender : IActionSender
{
    internal static readonly UIntPtr InputTag = new(0x434d0200);
    private readonly Dictionary<int, bool> keys = [];
    private bool left, right;
    public void Send(ActionStep step)
    {
        step.Validate();
        if (step.Kind == ActionKind.Wait) return;
        if (step.X is { } x && step.Y is { } y)
        {
            if (step.Kind is ActionKind.LeftClick or ActionKind.RightClick)
            {
                bool isLeft = step.Kind == ActionKind.LeftClick;
                uint down = isLeft ? 2u : 8u, up = isLeft ? 4u : 16u;
                var inputs = new[] { MoveInput(x, y), MouseInput(down), MouseInput(up) };
                uint sent = NativeMethods.SendInput(3, inputs, Marshal.SizeOf<NativeMethods.Input>());
                if (sent != 3)
                { if (sent == 2) Mouse(up); throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no pudo enviar el clic completo."); }
                if (isLeft) left = false; else right = false;
                return;
            }
            uint flag = step.Kind switch { ActionKind.LeftDown => 2u, ActionKind.LeftUp => 4u, ActionKind.RightDown => 8u, ActionKind.RightUp => 16u, _ => 0u };
            if (flag != 0)
            {
                uint sent = NativeMethods.SendInput(2, [MoveInput(x, y), MouseInput(flag)], Marshal.SizeOf<NativeMethods.Input>());
                if (sent != 2) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no pudo enviar el botón del mouse.");
                if (step.Kind is ActionKind.LeftDown or ActionKind.LeftUp) left = step.Kind == ActionKind.LeftDown;
                else right = step.Kind == ActionKind.RightDown;
                return;
            }
            Move(x, y);
        }
        switch (step.Kind)
        {
            case ActionKind.MouseMove: return;
            case ActionKind.LeftClick: new WindowsClickSender().Send(ClickButton.Left); left = false; return;
            case ActionKind.RightClick: new WindowsClickSender().Send(ClickButton.Right); right = false; return;
            case ActionKind.LeftDown: Mouse(2); left = true; break;
            case ActionKind.LeftUp: Mouse(4); left = false; break;
            case ActionKind.RightDown: Mouse(8); right = true; break;
            case ActionKind.RightUp: Mouse(16); right = false; break;
            case ActionKind.KeyDown:
                Key(step.KeyCode!.Value, false, step.Extended); keys[step.KeyCode.Value] = step.Extended; break;
            case ActionKind.KeyUp:
                Key(step.KeyCode!.Value, true, keys.GetValueOrDefault(step.KeyCode.Value, step.Extended)); keys.Remove(step.KeyCode.Value); break;
        }
    }
    private static void Insert(NativeMethods.Input input)
    {
        if (NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>()) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no pudo enviar la acción.");
    }
    private static void Move(int x, int y) => Insert(MoveInput(x, y));
    private static NativeMethods.Input MoveInput(int x, int y)
    {
        var screen = SystemInformation.VirtualScreen;
        if (!screen.Contains(x, y)) throw new ArgumentException("Las coordenadas están fuera de las pantallas actuales.");
        return new() { Type = 0, Data = new() { Mouse = new()
        {
            Dx = (int)Math.Round((x - screen.Left) * 65535d / Math.Max(1, screen.Width - 1)),
            Dy = (int)Math.Round((y - screen.Top) * 65535d / Math.Max(1, screen.Height - 1)),
            Flags = 0xC001, ExtraInfo = InputTag
        } } };
    }
    private static NativeMethods.Input MouseInput(uint flags) => new() { Type = 0, Data = new() { Mouse = new() { Flags = flags, ExtraInfo = InputTag } } };
    private static void Mouse(uint flags) => Insert(MouseInput(flags));
    private static void Key(int code, bool up, bool extended) => Insert(new() { Type = 1, Data = new() { Keyboard = new()
    { Key = (ushort)code, Flags = (up ? 2u : 0u) | (extended ? 1u : 0u), ExtraInfo = InputTag } } });
    public void ReleaseAll()
    {
        Exception? failure = null;
        foreach (var item in keys.ToArray())
        {
            try { Key(item.Key, true, item.Value); keys.Remove(item.Key); }
            catch (Exception ex) { failure = ex; }
        }
        if (left) { try { Mouse(4); left = false; } catch (Exception ex) { failure = ex; } }
        if (right) { try { Mouse(16); right = false; } catch (Exception ex) { failure = ex; } }
        if (failure != null) throw failure;
    }
}
