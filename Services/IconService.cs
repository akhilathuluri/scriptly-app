using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Scriptly.Services;

public enum IconKey
{
    History,
    Settings,
    Updates,
    MoreInfo,
    Exit,
    Search,
    AskAi,
    FixGrammar,
    Summarize,
    Translate,
    Expand,
    Shorten,
    ChangeTone,
    Rewrite,
    Explain,
    BulletPoints,
    CasualTone,
    Improve,
    ExplainCode
}

public sealed class IconService
{
    private static readonly IReadOnlyDictionary<IconKey, string> Glyphs = new Dictionary<IconKey, string>
    {
        [IconKey.History] = "\uE81C",
        [IconKey.Settings] = "\uE713",
        [IconKey.Updates] = "\uE895",
        [IconKey.MoreInfo] = "\uE946",
        [IconKey.Exit] = "\uE8BB",
        [IconKey.Search] = "\uE721",
        [IconKey.AskAi] = "\uE8BD",
        [IconKey.FixGrammar] = "\uE70F",
        [IconKey.Summarize] = "\uE8A5",
        [IconKey.Translate] = "\uE774",
        [IconKey.Expand] = "\uE70D",
        [IconKey.Shorten] = "\uE70E",
        [IconKey.ChangeTone] = "\uE790",
        [IconKey.Rewrite] = "\uE72C",
        [IconKey.Explain] = "\uE897",
        [IconKey.BulletPoints] = "\uE8FD",
        [IconKey.CasualTone] = "\uE76E",
        [IconKey.Improve] = "\uE74A",
        [IconKey.ExplainCode] = "\uE943"
    };

    public string GetGlyph(IconKey key)
    {
        return Glyphs.TryGetValue(key, out var glyph) ? glyph : string.Empty;
    }

    public string GetActionGlyph(string actionId, string fallback = "")
    {
        var key = actionId.ToLowerInvariant() switch
        {
            "ask_ai" => IconKey.AskAi,
            "fix_grammar" => IconKey.FixGrammar,
            "summarize" => IconKey.Summarize,
            "translate" => IconKey.Translate,
            "expand" => IconKey.Expand,
            "shorten" => IconKey.Shorten,
            "change_tone" => IconKey.ChangeTone,
            "rewrite" => IconKey.Rewrite,
            "explain" => IconKey.Explain,
            "bullet_points" => IconKey.BulletPoints,
            "casual_tone" => IconKey.CasualTone,
            "improve" => IconKey.Improve,
            "explain_code" => IconKey.ExplainCode,
            _ => (IconKey?)null
        };

        if (key is null)
            return fallback;

        var glyph = GetGlyph(key.Value);
        return string.IsNullOrWhiteSpace(glyph) ? fallback : glyph;
    }

    public Bitmap CreateMenuImage(IconKey key, int size = 16)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        DrawMenuSymbol(g, key, size, Color.FromArgb(205, 205, 230));

        return bmp;
    }

    public Icon CreateAppTrayIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var bgBrush = new SolidBrush(Color.FromArgb(90, 74, 247));
        g.FillEllipse(bgBrush, 0, 0, 15, 15);

        using var font = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("S", font, textBrush, new RectangleF(0, 0, 16, 16), sf);

        var hIcon = bmp.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var owned = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        return owned;
    }

    private static Font CreateSymbolFont(float size)
    {
        var fluent = new Font("Segoe Fluent Icons", size, FontStyle.Regular, GraphicsUnit.Point);
        if (string.Equals(fluent.Name, "Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase))
            return fluent;

        fluent.Dispose();
        return new Font("Segoe MDL2 Assets", size, FontStyle.Regular, GraphicsUnit.Point);
    }

    private static void DrawMenuSymbol(Graphics g, IconKey key, int size, Color color)
    {
        using var pen = new Pen(color, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var fill = new SolidBrush(color);

        var inset = 2f;
        var rect = new RectangleF(inset, inset, size - (inset * 2), size - (inset * 2));

        switch (key)
        {
            case IconKey.History:
                g.DrawArc(pen, rect, 35, 290);
                g.DrawLine(pen, size * 0.5f, size * 0.5f, size * 0.5f, size * 0.27f);
                g.DrawLine(pen, size * 0.5f, size * 0.5f, size * 0.66f, size * 0.58f);
                g.FillPolygon(fill, new[]
                {
                    new PointF(size * 0.18f, size * 0.27f),
                    new PointF(size * 0.35f, size * 0.27f),
                    new PointF(size * 0.35f, size * 0.43f)
                });
                break;

            case IconKey.Settings:
                g.DrawEllipse(pen, size * 0.34f, size * 0.34f, size * 0.32f, size * 0.32f);
                for (var i = 0; i < 8; i++)
                {
                    var angle = (MathF.PI * 2f * i) / 8f;
                    var x1 = size * 0.5f + MathF.Cos(angle) * size * 0.21f;
                    var y1 = size * 0.5f + MathF.Sin(angle) * size * 0.21f;
                    var x2 = size * 0.5f + MathF.Cos(angle) * size * 0.33f;
                    var y2 = size * 0.5f + MathF.Sin(angle) * size * 0.33f;
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                break;

            case IconKey.Updates:
                g.DrawLine(pen, size * 0.5f, size * 0.2f, size * 0.5f, size * 0.68f);
                g.FillPolygon(fill, new[]
                {
                    new PointF(size * 0.33f, size * 0.52f),
                    new PointF(size * 0.67f, size * 0.52f),
                    new PointF(size * 0.5f, size * 0.8f)
                });
                break;

            case IconKey.MoreInfo:
                g.FillEllipse(fill, size * 0.45f, size * 0.24f, size * 0.1f, size * 0.1f);
                g.DrawLine(pen, size * 0.5f, size * 0.4f, size * 0.5f, size * 0.75f);
                break;

            case IconKey.Exit:
                g.DrawLine(pen, size * 0.24f, size * 0.24f, size * 0.76f, size * 0.76f);
                g.DrawLine(pen, size * 0.76f, size * 0.24f, size * 0.24f, size * 0.76f);
                break;

            default:
                g.FillEllipse(fill, size * 0.38f, size * 0.38f, size * 0.24f, size * 0.24f);
                break;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
