using System.Globalization;
using Loadpath.Core.Editing;

namespace Loadpath.Presentation;

/// <summary>A preset tile's thumbnail: the generated geometry fitted into the tile's box, y up, as pixel segments.</summary>
internal static class PresetSketch
{
    // The tile draws it in a SketchWidth x SketchHeight box (Themes/Layout.xaml).
    private const double Width = 156, Height = 52, Pad = 3;

    /// <summary>The members as "x1,y1,x2,y2;…" in the thumbnail's pixels, centred; empty for a structure with no nodes.</summary>
    public static string Of(DocumentSnapshot s)
    {
        if (s.Nodes.Count == 0) return "";
        var pos = s.Nodes.ToDictionary(n => n.Id, n => n.Position);
        double minX = pos.Values.Min(v => v.X), maxX = pos.Values.Max(v => v.X), minY = pos.Values.Min(v => v.Y), maxY = pos.Values.Max(v => v.Y);
        var scale = Math.Min((Width - 2 * Pad) / Math.Max(maxX - minX, 1e-6), (Height - 2 * Pad) / Math.Max(maxY - minY, 1e-6));
        var ox = (Width - (maxX - minX) * scale) / 2;
        var oy = (Height + (maxY - minY) * scale) / 2;
        string P(Vec2 v) => string.Create(CultureInfo.InvariantCulture, $"{ox + (v.X - minX) * scale:0.#},{oy - (v.Y - minY) * scale:0.#}");
        return string.Join(";", s.Members.Select(m => $"{P(pos[m.Start])},{P(pos[m.End])}"));
    }
}
