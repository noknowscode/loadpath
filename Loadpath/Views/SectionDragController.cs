using Loadpath.Presentation;
using Loadpath.Workspace;
using Microsoft.UI.Xaml.Input;

namespace Loadpath.Views;

/// <summary>
/// Dragging a section chip from the inspector onto a member on the canvas. The drag crosses two views (the chip's
/// press in the inspector, the hit test on the canvas), so it is tracked on the page root with handled events too
/// (the canvas marks its pointer events handled); there is no XAML drag source for this. A ghost with the section's
/// name follows the pointer and the member under it is highlighted; release applies it (through the model), Esc cancels.
/// </summary>
public sealed class SectionDragController
{
    private readonly EditorEngine _engine;
    private readonly EditorModel _model;
    private readonly FrameworkElement _host;
    private readonly WorkspaceView _canvas;
    private readonly FrameworkElement _ghost;
    private readonly TextBlock _ghostText;
    private Section? _section;
    private int? _target;

    /// <param name="root">Where the pointer is tracked: the page root.</param>
    /// <param name="host">The canvas host the ghost is positioned in.</param>
    public SectionDragController(EditorEngine engine, EditorModel model, UIElement root, FrameworkElement host, WorkspaceView canvas, FrameworkElement ghost, TextBlock ghostText)
    {
        _engine = engine;
        _model = model;
        _host = host;
        _canvas = canvas;
        _ghost = ghost;
        _ghostText = ghostText;
        root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnMoved), handledEventsToo: true);
        root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnReleased), handledEventsToo: true);
        root.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnReleased), handledEventsToo: true);
    }

    public bool IsDragging => _section is not null;

    public void Start(Section section)
    {
        _section = section;
        _ghostText.Text = section.Name;
        _ghost.Margin = new Thickness(_host.ActualWidth - 120, 12, 0, 0);
        // xaml-lint: allow codebehind - the ghost follows the pointer for the life of the drag
        _ghost.Visibility = Visibility.Visible;
        _engine.Interaction.Hint = "Drop on a member to apply the section · Esc cancels";
    }

    // xaml-lint: allow codebehind - drag tracking across the inspector and the canvas, see the class summary
    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_section is null) return;
        var p = e.GetCurrentPoint(_host).Position;
        _ghost.Margin = new Thickness(p.X + 12, p.Y + 12, 0, 0);
        var onCanvas = e.GetCurrentPoint(_canvas).Position;
        var hit = _engine.Interaction.Hit(new Vec2(onCanvas.X, onCanvas.Y), includeLoadHandles: false);
        var target = hit.Kind == HitKind.Member ? hit.Id : (int?)null;
        if (target == _target) return;
        _target = target;
        _engine.Interaction.Overlay.HighlightMember = target;
        _engine.RequestRender();
    }

    // xaml-lint: allow codebehind - drag tracking, see OnMoved
    private void OnReleased(object sender, PointerRoutedEventArgs e) => End(drop: true);

    /// <param name="drop">The pointer was released: apply the section to the member under it. Esc only cancels.</param>
    public void End(bool drop)
    {
        if (_section is not { } section) return;
        _section = null;
        // xaml-lint: allow codebehind - the ghost ends with the drag
        _ghost.Visibility = Visibility.Collapsed;
        _engine.Interaction.Overlay.HighlightMember = null;
        if (drop && _target is { } id) _model.DropSection(id, section);
        _target = null;
        _engine.Interaction.Hint = "";
        _engine.RequestRender();
    }
}
