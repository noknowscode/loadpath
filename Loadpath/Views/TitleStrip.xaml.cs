using Microsoft.UI.Xaml.Controls.Primitives;

namespace Loadpath.Views;

public sealed partial class TitleStrip : UserControl
{
    public TitleStrip()
    {
        InitializeComponent();
        SizeChanged += (_, _) => FitDocumentName();
        LeftCluster.SizeChanged += (_, _) => FitDocumentName();
        RightCluster.SizeChanged += (_, _) => FitDocumentName();
    }

    /// <summary>
    /// The document name gets the width the strip has left once both command clusters are in, up to its token maximum,
    /// and trims with an ellipsis below that. A fit, not a breakpoint: which commands show is the responsive layout's job.
    /// </summary>
    // xaml-lint: allow responsive - sizes the name to the room left over, no layout decision
    private void FitDocumentName()
    {
        if (DocumentName.Visibility != Visibility.Visible) return;
        var cap = Application.Current.Resources.TryGetValue("DocumentNameMaxWidth", out var m) && m is double d ? d : double.PositiveInfinity;
        var gap = Application.Current.Resources.TryGetValue("Space16", out var g) && g is double gd ? gd : 0;
        var strip = ((Grid)Content).Padding;
        var others = LeftCluster.DesiredSize.Width - DocumentNameText.DesiredSize.Width;
        var room = ActualWidth - strip.Left - strip.Right - RightCluster.DesiredSize.Width - gap - others;
        var fitted = Math.Clamp(room, 0, cap);
        if (Math.Abs(DocumentNameText.MaxWidth - fitted) > 0.5) DocumentNameText.MaxWidth = fitted;
    }

    // xaml-lint: allow codebehind - Flyout has no bindable IsOpen; Done closes it (light dismiss also does)
    private void OnNoiseDone(object sender, RoutedEventArgs e) => NoiseFlyout.Hide();

    // xaml-lint: allow codebehind - a chosen preset closes its flyout; the bound command does the work
    private void OnPresetClick(object sender, RoutedEventArgs e) => PresetsFlyout.Hide();

    /// <summary>
    /// A sheet never grows past the window: on a phone the 380 px presets sheet and the 318 px layer sheet shrink to the
    /// window less a gutter each side, and scroll when the window is shorter than the sheet.
    /// </summary>
    // xaml-lint: allow codebehind - caps a flyout sheet to the window it opens in (no bindable window size)
    private void OnSheetOpening(object? sender, object e)
    {
        if (sender is not Flyout { Content: FrameworkElement sheet } || XamlRoot is not { } root) return;
        var gutter = Application.Current.Resources.TryGetValue("SheetWindowGutter", out var g) && g is double d ? d : 0;
        sheet.MaxWidth = Math.Max(0, root.Size.Width - 2 * gutter);
        sheet.MaxHeight = Math.Max(0, root.Size.Height - ActualHeight - 2 * gutter);
    }

    private Flyout? _sheetAfterMenu;

    // xaml-lint: allow codebehind - below Wide the presets and layer sheets open from the overflow menu, once it has closed
    private void OnOverflowPresets(object sender, RoutedEventArgs e) => _sheetAfterMenu = PresetsFlyout;

    // xaml-lint: allow codebehind - see OnOverflowPresets
    private void OnOverflowNoise(object sender, RoutedEventArgs e) => _sheetAfterMenu = NoiseFlyout;

    // xaml-lint: allow codebehind - a flyout cannot open while the menu that asked for it is still closing
    private void OnOverflowClosed(object? sender, object e)
    {
        if (_sheetAfterMenu is not { } sheet) return;
        _sheetAfterMenu = null;
        DispatcherQueue.TryEnqueue(() => sheet.ShowAt(OverflowButton, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight }));
    }
}
