namespace Loadpath.Presentation;

/// <summary>How results read in the panels: signed forces, utilization tones (resource keys), load arrows and support words.</summary>
internal static class ResultText
{
    public static string UtilTone(double util) => util >= 1 ? "ErrorBrush" : util >= 0.75 ? "WarnBrush" : "OnSurfaceBrush";

    /// <summary>Tension and compression are the structure's ink (Primary); their sign reads from the word and the line style.</summary>
    public static string ForceTone(bool tension, bool compression, bool danger) =>
        danger ? "ErrorBrush" : tension || compression ? "PrimaryBrush" : "OnSurfaceVariantBrush";

    public static string SignedKn(double v) => (v >= 0 ? "+" : "−") + $"{Math.Abs(v):0.0}";

    /// <summary>A signed value in a fixed-width column ("+  12.5", "−   3.0"), for reaction tables.</summary>
    public static string Column(double v) => (Math.Round(v, 1) >= 0 ? "+" : "−") + $"{Math.Abs(v),6:0.0}";

    /// <summary>Nearest of eight arrows for a load direction (screen convention: +y is up).</summary>
    public static string Arrow(Vec2 v)
    {
        if (v.LengthSquared < 1e-12) return "";
        var deg = Math.Atan2(v.Y, v.X) * 180 / Math.PI;
        var i = (int)Math.Round(((deg % 360) + 360) % 360 / 45) % 8;
        return "→↗↑↖←↙↓↘"[i].ToString();
    }

    public static string SupportWord(SupportKind k) => k switch
    {
        SupportKind.Pin => "PIN",
        SupportKind.RollerY => "ROLLER",
        SupportKind.RollerX => "ROLLER Y",
        _ => "",
    };

    public static string Plural(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
}
