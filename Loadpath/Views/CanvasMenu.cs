using Loadpath.Commands;
using Loadpath.Presentation;
using Loadpath.Workspace;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Loadpath.Views;

/// <summary>
/// The canvas context menu (right click, or a long press). What is under the pointer picks the entries; each entry
/// is a registered <see cref="AppCommand"/> bound as the item's command, so its title, shortcut and availability are
/// the palette's and the keyboard's.
/// </summary>
public static partial class CanvasMenu
{
    /// <summary>One entry: a command id with an optional shorter title, the section submenu, the add-node item, or a separator.</summary>
    public sealed partial record Entry(string Id, string? Title = null)
    {
        public const string SeparatorId = "-";
        public const string SectionsId = "sections";
        public const string AddNodeId = "addNode";
        public static readonly Entry Separator = new(SeparatorId);
    }

    /// <summary>The entries for what the pointer is on: a node (or its load), a member, or the empty sheet.</summary>
    public static IReadOnlyList<Entry> EntriesFor(HitKind hit) => hit switch
    {
        HitKind.Node or HitKind.LoadHandle => new Entry[]
        {
            new("structure.pin", "Pin support"), new("structure.roller", "Roller support"), new("structure.rollerX", "Vertical roller"),
            new("structure.noSupport", "No support"), Entry.Separator, new("structure.clearLoad"), new("edit.duplicate"), new("edit.delete"),
        },
        HitKind.Member => new Entry[]
        {
            new(Entry.SectionsId, "Set section"), new("structure.split"), Entry.Separator, new("edit.duplicate"), new("edit.delete"),
        },
        _ => new Entry[]
        {
            new(Entry.AddNodeId, "Add node here"), new("edit.selectAll"), Entry.Separator,
            new("view.fit"), new("view.grid"), new("view.snap"), Entry.Separator, new("view.palette"),
        },
    };

    /// <summary>Open the menu for a request from the canvas.</summary>
    public static void Show(EditorEngine engine, EditorModel model, WorkspaceView canvas, ContextMenuRequest request)
    {
        var menu = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        // A long press gets fingertip rows.
        var touchStyle = request.Touch && Application.Current.Resources.TryGetValue("TouchMenuItem", out var ts) ? ts as Style : null;
        MenuFlyoutItem Item(string text, AppCommand command, string? shortcut)
        {
            var item = new MenuFlyoutItem { Text = text, Command = command, KeyboardAcceleratorTextOverride = shortcut ?? "" };
            if (touchStyle is not null) item.Style = touchStyle;
            return item;
        }

        foreach (var entry in EntriesFor(request.Hit.Kind))
        {
            switch (entry.Id)
            {
                case Entry.SeparatorId:
                    menu.Items.Add(new MenuFlyoutSeparator());
                    break;
                case Entry.SectionsId:
                    var sub = new MenuFlyoutSubItem { Text = entry.Title };
                    foreach (var s in Section.Library) sub.Items.Add(Item(s.Name, engine.Commands[$"section.{s.Id}"], null));
                    menu.Items.Add(sub);
                    break;
                case Entry.AddNodeId:
                    // At the snapped point under the pointer, as the node tool would place it.
                    var world = engine.Interaction.SnapPoint(engine.Viewport.ToWorld(new Vec2(request.Position.X, request.Position.Y)), false).Point;
                    var title = entry.Title ?? "Add node here";
                    menu.Items.Add(Item(title, new AppCommand("canvas.addNode", title, "Edit", null, () => model.AddNodeAt(world)), "N"));
                    break;
                default:
                    if (engine.Commands.Find(entry.Id) is { } c) menu.Items.Add(Item(entry.Title ?? c.Title, c, c.ShortcutDisplay.Split('|')[0]));
                    break;
            }
        }
        menu.ShowAt(canvas, request.Position);
    }
}
