using System.Drawing.Drawing2D;

namespace ClickMoves.UI;

public static class Branding
{
    public const string ProductName = "MiniTools";
    public const string Developer = "BrioCraft";
    public const string WindowTitle = ProductName;
    public const string Credit = "Desarrollado por BrioCraft";
    public const string Description = "Automatiza clics y tareas con grabaciones y macros.";

    public static Icon LoadIcon()
    {
        using var stream = typeof(Branding).Assembly.GetManifestResourceStream("MiniTools.Icon")!;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}

/// <summary>Small, code-native version of the supplied cursor / MT / REC concept.</summary>
public sealed class BrandMark : Control
{
    public BrandMark()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AccessibleName = "MiniTools: cursor MT y botón de grabación";
        TabStop = false;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        float size = Math.Min(ClientSize.Width, ClientSize.Height);
        var state = e.Graphics.Save();
        e.Graphics.TranslateTransform((ClientSize.Width - size) / 2, (ClientSize.Height - size) / 2);
        Draw(e.Graphics, size);
        e.Graphics.Restore(state);
    }
    public static Bitmap Render(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        Draw(graphics, size);
        return bitmap;
    }
    private static void Draw(Graphics graphics, float size)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(size / 96, size / 96);
        using var red = new SolidBrush(Color.FromArgb(218, 49, 60));
        using var outline = new Pen(Color.FromArgb(129, 29, 39), 2);
        using var white = new SolidBrush(Color.White);
        using var blue = new SolidBrush(ToolStyle.Accent);
        using var border = new Pen(Color.White, 3) { LineJoin = LineJoin.Round };
        graphics.FillEllipse(red, 53, 4, 39, 39);
        graphics.DrawEllipse(outline, 53, 4, 39, 39);
        graphics.FillEllipse(white, 65, 16, 15, 15);
        PointF[] cursor = [new(6, 56), new(66, 27), new(53, 91), new(36, 69), new(23, 90), new(10, 78), new(24, 58)];
        graphics.FillPolygon(blue, cursor);
        graphics.DrawPolygon(border, cursor);
        // At 16/24 px retain the distinctive silhouette rather than unreadable letters.
        if (size < 32) return;
        PointF[] m = [new(23, 52), new(27, 52), new(30, 56), new(33, 52), new(37, 52), new(37, 66), new(33, 66), new(33, 58), new(30, 62), new(27, 58), new(27, 66), new(23, 66)];
        PointF[] t = [new(40, 52), new(52, 52), new(52, 56), new(48, 56), new(48, 66), new(44, 66), new(44, 56), new(40, 56)];
        graphics.FillPolygon(white, m); graphics.FillPolygon(white, t);
    }
}
