using Windows.Foundation;

namespace Loadpath.Controls;

/// <summary>
/// A stack that starts a new column (or row) when the next child would run past the space it has, instead of
/// clipping. The floating tool rail is a column of seven tools: on a landscape phone (844 x 390) the canvas is shorter
/// than the rail, so the last tools wrap into a second column rather than running under the status bar. With unlimited
/// space (the phone's scrolling tool strip) it is a plain stack. Children fill the column's width (the row's height).
/// </summary>
public sealed partial class WrapStack : Panel
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(WrapStack), new PropertyMetadata(Orientation.Vertical, OnLayoutPropertyChanged));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(WrapStack), new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WrapStack)d).InvalidateMeasure();

    /// <summary>Where a child goes, in the panel's coordinates.</summary>
    public readonly record struct Slot(double X, double Y, double Width, double Height);

    /// <summary>
    /// Stack <paramref name="sizes"/> along the main axis, wrapping when a line would pass <paramref name="limit"/>.
    /// Returns each child's slot and the panel's size. A child longer than the limit still gets a line of its own.
    /// </summary>
    /// <param name="vertical">Columns (true) or rows (false).</param>
    public static (Slot[] Slots, Vec2 Size) Layout(IReadOnlyList<Vec2> sizes, double limit, double spacing, bool vertical)
    {
        // Work in (main, cross) and swap at the end.
        double Main(Vec2 s) => vertical ? s.Y : s.X;
        double Cross(Vec2 s) => vertical ? s.X : s.Y;

        var lines = new List<(int First, int Count, double Main, double Cross)>();
        var first = 0; double main = 0, cross = 0;
        for (var i = 0; i < sizes.Count; i++)
        {
            var add = (i > first ? spacing : 0) + Main(sizes[i]);
            if (i > first && main + add > limit)
            {
                lines.Add((first, i - first, main, cross));
                first = i; main = 0; cross = 0;
                add = Main(sizes[i]);
            }
            main += add;
            cross = Math.Max(cross, Cross(sizes[i]));
        }
        if (sizes.Count > first) lines.Add((first, sizes.Count - first, main, cross));

        var slots = new Slot[sizes.Count];
        double lineStart = 0, longest = 0;
        foreach (var line in lines)
        {
            double at = 0;
            for (var i = line.First; i < line.First + line.Count; i++)
            {
                var length = Main(sizes[i]);
                slots[i] = vertical ? new Slot(lineStart, at, line.Cross, length) : new Slot(at, lineStart, length, line.Cross);
                at += length + spacing;
            }
            longest = Math.Max(longest, line.Main);
            lineStart += line.Cross + spacing;
        }
        var across = lines.Count == 0 ? 0 : lineStart - spacing;
        return (slots, vertical ? new Vec2(across, longest) : new Vec2(longest, across));
    }

    private Slot[] _slots = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        var vertical = Orientation == Orientation.Vertical;
        var shown = new List<int>(Children.Count);
        var sizes = new List<Vec2>(Children.Count);
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(availableSize);
            // A collapsed child takes no place in a line.
            if (child.Visibility == Visibility.Collapsed) continue;
            shown.Add(i);
            sizes.Add(new Vec2(child.DesiredSize.Width, child.DesiredSize.Height));
        }
        var (slots, size) = Layout(sizes, vertical ? availableSize.Height : availableSize.Width, Spacing, vertical);
        _slots = new Slot[Children.Count];
        for (var k = 0; k < shown.Count; k++) _slots[shown[k]] = slots[k];
        return new Size(size.X, size.Y);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < Children.Count && i < _slots.Length; i++)
        {
            var s = _slots[i];
            Children[i].Arrange(new Rect(s.X, s.Y, s.Width, s.Height));
        }
        return finalSize;
    }
}
