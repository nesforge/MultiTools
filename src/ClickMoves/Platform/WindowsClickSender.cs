using System.ComponentModel;
using System.Runtime.InteropServices;
using ClickMoves.AutoClick;
using ClickMoves.Configuration;

namespace ClickMoves.Platform;

public sealed class WindowsClickSender : IClickSender
{
    public void Send(ClickButton button)
    {
        uint down = button == ClickButton.Left ? 0x0002u : 0x0008u;
        uint up = button == ClickButton.Left ? 0x0004u : 0x0010u;
        var pair = new[] { Mouse(down), Mouse(up) };
        uint sent = NativeMethods.SendInput(2, pair, Marshal.SizeOf<NativeMethods.Input>());
        if (sent == 2) return;
        // If only the press entered the input stream, attempt to release it before stopping.
        if (sent == 1) NativeMethods.SendInput(1, new[] { Mouse(up) }, Marshal.SizeOf<NativeMethods.Input>());
        throw new Win32Exception(Marshal.GetLastWin32Error(),
            "Windows no pudo enviar el clic completo. La aplicación de destino puede tener permisos superiores.");
    }
    private static NativeMethods.Input Mouse(uint flags) => new()
    {
        Type = 0, Data = new NativeMethods.InputUnion { Mouse = new NativeMethods.MouseInput { Flags = flags } }
    };
}
