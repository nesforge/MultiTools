using System.Security.Cryptography;
using ClickMoves.UI;

internal static class BrandAssetChecks
{
    public static void Verify()
    {
        string folder = Path.Combine(Environment.CurrentDirectory, "assets");
        using var file = File.OpenRead(Path.Combine(folder, "minitools-app.ico"));
        using var embedded = typeof(Branding).Assembly.GetManifestResourceStream("MiniTools.Icon")!;
        AutomationChecks.Assert(Convert.ToHexString(SHA256.HashData(file)) == Convert.ToHexString(SHA256.HashData(embedded)), "Icono original de MiniTools incrustado sin alterar");
        AutomationChecks.Assert(!typeof(Branding).Assembly.GetManifestResourceNames().Any(n => n.Contains("BrioCraft")), "Solo recursos de producto en la app");
        using var icon = Branding.LoadIcon();
        using var symbol = BrandMark.Render(256);
        using var original = new Bitmap(Path.Combine(folder, "minitools-mark.png"));
        AutomationChecks.Assert(symbol.Width == original.Width && symbol.Height == original.Height && symbol.GetPixel(0, 0).A == 0, "Logo proporcional y transparente");
        for (int y = 0; y < original.Height; y++)
            for (int x = 0; x < original.Width; x++)
                AutomationChecks.Assert(symbol.GetPixel(x, y).ToArgb() == original.GetPixel(x, y).ToArgb(), "Logo recuperado idéntico al recurso MiniTools 1.3");
        byte[] data = File.ReadAllBytes(Path.Combine(folder, "minitools-app.ico"));
        int frames = BitConverter.ToUInt16(data, 4);
        AutomationChecks.Assert(frames >= 3, "Icono multirresolución");
        for (int i = 0; i < frames; i++)
        {
            int offset = 6 + 16 * i;
            AutomationChecks.Assert(data[offset] == data[offset + 1], "Proporciones del icono");
        }
        Console.WriteLine("MiniTools: icono original, logo idéntico al recurso de 1.3 y transparencia verificados.");
    }
    public static double Contrast(Color foreground, Color background)
    {
        static double Channel(byte value)
        { double n = value / 255d; return n <= 0.04045 ? n / 12.92 : Math.Pow((n + 0.055) / 1.055, 2.4); }
        static double Luminance(Color c) => .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        double first = Luminance(foreground), second = Luminance(background);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }
}
