using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Loadpath.Views;

public sealed partial class InspectorPanel : UserControl
{
    public InspectorPanel()
    {
        InitializeComponent();
        // Section chips are buttons (keyboard and screen reader reach them) and a button marks its press handled,
        // so the drag start listens for handled presses too.
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPanelPointerPressed), handledEventsToo: true);
    }

    /// <summary>Raised when the user presses a section chip; the page runs the pointer drag onto the canvas.</summary>
    public event EventHandler<Section>? SectionDragStarted;

    // xaml-lint: allow codebehind - starts the section drag that MainPage tracks onto the canvas
    private void OnPanelPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // A finger on a chip scrolls the inspector or taps the chip (which applies it); only mouse and pen drag.
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch) return;
        for (var o = e.OriginalSource as DependencyObject; o is not null && !ReferenceEquals(o, this); o = VisualTreeHelper.GetParent(o))
        {
            if (o is Button { Tag: string id } && Section.Library.FirstOrDefault(x => x.Id == id) is { } section)
            {
                SectionDragStarted?.Invoke(this, section);
                return;
            }
        }
    }
}
