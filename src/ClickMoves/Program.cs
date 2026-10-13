using ClickMoves.UI;

namespace ClickMoves;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Prevent independent instances from running two engines for the same user/session.
        using var instance = new Mutex(true, @"Local\ClickMoves.AutoClick.v1", out bool first);
        ApplicationConfiguration.Initialize();
        if (!first)
        {
            MessageBox.Show($"{Branding.ProductName} ya está abierto. Busca su ventana en la barra de tareas.",
                "Aplicación abierta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Application.Run(new MainForm());
    }
}
