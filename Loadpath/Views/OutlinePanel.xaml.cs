using Loadpath.Controls;
using Loadpath.Presentation;
using Microsoft.UI.Xaml.Input;

namespace Loadpath.Views;

public sealed partial class OutlinePanel : UserControl
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(EditorModel), typeof(OutlinePanel), new PropertyMetadata(null));

    public OutlinePanel() => InitializeComponent();

    /// <summary>The page model: a row's press and hover go to it with the element key the row carries in Tag.</summary>
    public EditorModel? Model
    {
        get => (EditorModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Element keys that have already had their entrance, so a row re-prepared for a value change stays still.</summary>
    private readonly HashSet<string> _entered = new();

    // A new row fades and rises in once: ItemsRepeater has no per-item add transition on Uno.
    private void OnRowPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is FrameworkElement { Tag: string key } row && _entered.Add(key)) Motion.Enter(row);
    }

    // xaml-lint: allow codebehind - Shift/Ctrl-aware selection reads the key state on the UI thread at the click; generated commands run off it
    private void OnRowClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string key } && Model is { } model)
            model.SelectFromOutline(key, KeyboardState.IsDown(Windows.System.VirtualKey.Shift) || KeyboardState.IsDown(Windows.System.VirtualKey.Control));
    }

    // xaml-lint: allow codebehind - row hover highlights the element on the canvas
    private void OnRowEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string key }) Model?.HoverFromOutline(key);
    }

    // xaml-lint: allow codebehind - clears the canvas hover highlight
    private void OnRowExited(object sender, PointerRoutedEventArgs e) => Model?.HoverFromOutline(null);
}
