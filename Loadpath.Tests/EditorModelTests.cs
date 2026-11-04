using System.Collections.Immutable;
using System.Diagnostics;
using Loadpath.Core.Editing;
using Loadpath.Core.Model;
using Loadpath.Core.Samples;
using Loadpath.Core.Geometry;
using Loadpath.Presentation;
using Uno.Extensions.Reactive;
using Xunit;

namespace Loadpath.Tests;

/// <summary>
/// The page model (MVUX): what the views bind to follows the engine, and what the views send reaches it. The engine
/// raises events and the model projects them into states asynchronously, so each check waits for the state it reads.
/// </summary>
public class EditorModelTests
{
    private readonly EditorEngine _engine = new();
    private readonly EditorModel _model;
    private readonly List<ToastMessage> _toasts = new();

    public EditorModelTests()
    {
        _model = Models.Editor(_engine, new TestUiThread());
        _engine.ToastRequested += (_, t) => _toasts.Add(t);
    }

    /// <summary>The feed's value once it satisfies <paramref name="ready"/>, or a timeout with the last value seen.</summary>
    private static async Task<T> When<T>(IFeed<T> feed, Func<T, bool> ready) where T : notnull
    {
        var clock = Stopwatch.StartNew();
        T? last = default;
        while (clock.ElapsedMilliseconds < 5000)
        {
            last = await feed;
            if (last is not null && ready(last)) return last;
            await Task.Delay(10);
        }
        throw new TimeoutException($"Still {last} after 5 s");
    }

    private static async Task<IImmutableList<T>> When<T>(IListFeed<T> feed, Func<IImmutableList<T>, bool> ready) where T : notnull
    {
        var clock = Stopwatch.StartNew();
        IImmutableList<T> last = ImmutableList<T>.Empty;
        while (clock.ElapsedMilliseconds < 5000)
        {
            last = await feed;
            if (ready(last)) return last;
            await Task.Delay(10);
        }
        throw new TimeoutException($"Still {last.Count} items after 5 s");
    }

    private static async Task Until(Func<bool> ready)
    {
        var clock = Stopwatch.StartNew();
        while (!ready())
        {
            if (clock.ElapsedMilliseconds > 5000) throw new TimeoutException();
            await Task.Delay(10);
        }
    }

    private void Warren() => _engine.LoadSnapshot(SampleStructures.Build("warren"), "Open", null);

    [Fact]
    public async Task The_status_starts_empty_and_follows_a_loaded_structure()
    {
        Assert.True((await _model.Status)!.IsEmpty);

        Warren();

        var status = await When(_model.Status, s => s.PillText == "WITHIN CAPACITY" && !s.IsDirty);
        Assert.Equal("Warren truss", status.DocumentName);
        Assert.False(status.IsDirty);
        Assert.False(status.IsEmpty);
        Assert.Equal("OkBrush", status.StatusToneKey);
    }

    [Fact]
    public async Task An_edit_makes_the_status_dirty_and_offers_its_undo()
    {
        Warren();
        // Loading passes through a dirty state (the replacement) before it is marked saved: wait for the clean one.
        await When(_model.Status, s => !s.IsEmpty && !s.IsDirty);

        _engine.History.Do(new SetSupportEdit(4, SupportKind.Pin));

        var status = await When(_model.Status, s => s.IsDirty && s.CanUndo);
        Assert.Equal("Undo " + _engine.History.UndoLabel!.ToLowerInvariant(), status.UndoTooltip);
    }

    [Fact]
    public async Task A_roller_alone_raises_the_mechanism_banner()
    {
        Warren();
        var pin = _engine.Document.Nodes.Single(n => n.Support == SupportKind.Pin);
        _engine.History.Do(new SetSupportEdit(pin.Id, SupportKind.None));

        var status = await When(_model.Status, s => s.IsMechanism);
        Assert.Contains("can move freely", status.MechanismText);
        Assert.Equal("DangerBrush", status.StatusToneKey);
    }

    [Fact]
    public async Task Selecting_a_node_or_a_member_switches_the_inspector()
    {
        Warren();
        await When(_model.Inspector, i => i.IsSummary && i.Title == "Warren truss");

        _engine.Selection.Set(ElementRef.Node(2));
        var node = await When(_model.Inspector, i => i.IsNode);
        Assert.Equal("Node N2", node.Title);
        Assert.Equal(new NodeVector(2, _engine.Document.GetNode(2).Position.X, _engine.Document.GetNode(2).Position.Y), await _model.NodePosition);

        _engine.Selection.Set(ElementRef.Member(1));
        var member = await When(_model.Inspector, i => i.IsMember);
        Assert.Equal("Member M1", member.Title);
        Assert.Equal("M1", member.SelectionLabel);
    }

    [Fact]
    public async Task A_position_typed_in_the_inspector_moves_its_node()
    {
        Warren();
        _engine.Selection.Set(ElementRef.Node(2));
        await When(_model.Inspector, i => i.IsNode);

        await _model.NodePosition.UpdateAsync(_ => new NodeVector(2, 2.5, 0.5), CancellationToken.None);

        await Until(() => _engine.Document.GetNode(2).Position == new Vec2(2.5, 0.5));
        Assert.True(_engine.History.CanUndo);
    }

    [Fact]
    public void SupportPin_pins_the_selected_node_as_one_undo_step()
    {
        Warren();
        Assert.Equal(SupportKind.None, _engine.Document.GetNode(4).Support);
        _engine.Selection.Set(ElementRef.Node(4));

        _model.SupportPin();

        Assert.Equal(SupportKind.Pin, _engine.Document.GetNode(4).Support);
        Assert.Equal("Set pin", _engine.History.UndoLabel);
        _engine.History.Undo();
        Assert.Equal(SupportKind.None, _engine.Document.GetNode(4).Support);
        Assert.False(_engine.History.CanUndo);
    }

    [Fact]
    public void SupportPin_on_a_node_that_is_already_pinned_adds_no_undo_step()
    {
        Warren();
        var pin = _engine.Document.Nodes.Single(n => n.Support == SupportKind.Pin);
        _engine.Selection.Set(pin.Ref);

        _model.SupportPin();

        Assert.False(_engine.History.CanUndo);
    }

    [Fact]
    public void SupportPin_pins_every_selected_node_at_once_and_asks_for_a_node_without_one()
    {
        Warren();
        _engine.Selection.Replace(new[] { ElementRef.Node(3), ElementRef.Node(4) });

        _model.SupportPin();

        Assert.Equal(SupportKind.Pin, _engine.Document.GetNode(3).Support);
        Assert.Equal(SupportKind.Pin, _engine.Document.GetNode(4).Support);
        _engine.History.Undo();
        Assert.Equal(SupportKind.None, _engine.Document.GetNode(3).Support);
        Assert.Equal(SupportKind.None, _engine.Document.GetNode(4).Support);

        _engine.Selection.Set(ElementRef.Member(1));
        _model.SupportPin();
        Assert.Contains(new ToastMessage("Select a node first", false), _toasts);
    }

    [Fact]
    public async Task A_section_picked_in_the_inspector_lands_on_its_member()
    {
        Warren();
        _engine.Selection.Set(ElementRef.Member(1));
        await When(_model.Inspector, i => i.IsMember);
        var index = Section.Library.ToList().FindIndex(s => s != _engine.Document.GetMember(1).Section);
        // The selection moves on before the pick is committed: the pick names its member.
        _engine.Selection.Set(ElementRef.Member(2));

        await _model.MemberSection.UpdateAsync(_ => new SectionPick(1, index), CancellationToken.None);

        await Until(() => _engine.Document.GetMember(1).Section == Section.Library[index]);
        Assert.Equal(1, _engine.History.UndoDepth);
    }

    [Fact]
    public async Task A_section_pick_outside_the_library_changes_nothing()
    {
        Warren();
        var before = _engine.Document.GetMember(1).Section;

        await _model.MemberSection.UpdateAsync(_ => new SectionPick(1, Section.Library.Count), CancellationToken.None);
        await _model.MemberSection.UpdateAsync(_ => new SectionPick(1, -1), CancellationToken.None);
        await Task.Delay(100);

        Assert.Equal(before, _engine.Document.GetMember(1).Section);
        Assert.False(_engine.History.CanUndo);
    }

    [Fact]
    public async Task A_load_typed_in_the_inspector_lands_on_its_node()
    {
        Warren();
        _engine.Selection.Set(ElementRef.Node(4));
        await When(_model.Inspector, i => i.IsNode && i.Title == "Node N4");

        await _model.NodeLoad.UpdateAsync(_ => new NodeVector(4, 2, -20), CancellationToken.None);

        await Until(() => _engine.Document.GetNode(4).Load == new Vec2(2, -20));
        Assert.Equal("Set load", _engine.History.UndoLabel);
    }

    [Fact]
    public async Task Undo_and_redo_step_through_the_history_and_the_status_gates_them()
    {
        Warren();
        // The buttons are enabled by Status.CanUndo and Status.CanRedo.
        await When(_model.Status, s => !s.IsEmpty && !s.CanUndo && !s.CanRedo);
        _engine.History.Do(new SetSupportEdit(4, SupportKind.Pin));
        await When(_model.Status, s => s.CanUndo && s.IsDirty);

        _model.UndoStep();

        Assert.Equal(SupportKind.None, _engine.Document.GetNode(4).Support);
        await When(_model.Status, s => !s.CanUndo && s.CanRedo && !s.IsDirty && s.RedoTooltip == "Redo set pin");

        _model.RedoStep();

        Assert.Equal(SupportKind.Pin, _engine.Document.GetNode(4).Support);
        await When(_model.Status, s => s.CanUndo && !s.CanRedo && s.IsDirty);
    }

    [Fact]
    public async Task Overloaded_members_are_listed_as_failures_with_their_mode()
    {
        _engine.LoadSnapshot(TrussPresets.Build("pratt", new PresetParameters(24, 6, 1.6)), "Load preset", null);

        var failures = await When(_model.Failures, f => f.Count > 0);
        // The list, the flag, the header and Lighten are separate states, published one after another.
        await When(_model.HasFailures, v => v);
        Assert.All(failures, f => Assert.Contains(f.Mode, new[] { "BUCKLES", "YIELDS" }));
        await When(_model.FailuresHeader, h => h == (failures.Count == 1 ? "1 MEMBER" : $"{failures.Count} MEMBERS"));
        await When(_model.CanLighten, v => !v);
    }

    [Fact]
    public async Task The_outline_lists_nodes_and_members_and_marks_the_selection()
    {
        Warren();
        _engine.Selection.Set(ElementRef.Member(3));

        var members = await When(_model.OutlineMembers, m => m.Any(r => r.RowBrushKey == "SelectionBrush"));
        Assert.Equal(_engine.Document.Members.Count, members.Count);
        Assert.Equal("m3", Assert.Single(members, r => r.RowBrushKey == "SelectionBrush").Key);
        await When(_model.NodesHeader, h => h == _engine.Document.Nodes.Count.ToString());
        await When(_model.OutlineIsEmpty, v => !v);
    }

    [Fact]
    public async Task Display_commands_reach_the_engine_and_option_changes_come_back()
    {
        _model.ShowUtilization();
        Assert.Equal(DisplayMode.Utilization, _engine.Options.DisplayMode);
        await When(_model.IsUtilization, v => v);
        await When(_model.IsForces, v => !v);

        _engine.Options.ReduceNoise = true;
        Assert.StartsWith("NOISE REDUCED · ", await When(_model.NoiseStatus, t => t.Length > 0 && _engine.Options.ReduceNoise));
        Assert.True(await When(_model.ReduceNoise, v => v));

        _model.ThemeDark();
        Assert.Equal(ThemeChoice.Dark, _engine.Options.Theme);
        await When(_model.IsThemeDark, v => v);
    }

    [Fact]
    public async Task A_noise_layer_toggled_in_the_view_is_kept_by_the_engine()
    {
        Assert.False(await _model.KeepGrid);

        await _model.KeepGrid.SetAsync(true, CancellationToken.None);

        await Until(() => (_engine.Options.Keep & NoiseLayer.Grid) != 0);
        _model.ResetNoiseLayers();
        Assert.Equal(ViewOptions.DefaultKeep, _engine.Options.Keep);
    }

    [Fact]
    public async Task The_palette_opens_with_every_command_and_a_query_narrows_it()
    {
        await _model.TogglePalette(CancellationToken.None);
        Assert.True(await _model.IsPaletteOpen);
        var all = await When(_model.PaletteItems, items => items.Count > 0);
        Assert.Equal(40, all.Count);

        await _model.PaletteQuery.SetAsync("show util", CancellationToken.None);

        var narrowed = await When(_model.PaletteItems, items => items.Count > 0 && items[0].Id == "view.utilization");
        Assert.Equal("SurfaceHoverBrush", narrowed[0].RowBrushKey);
    }

    [Fact]
    public async Task Enter_runs_the_highlighted_entry_and_closes_the_palette()
    {
        await _model.TogglePalette(CancellationToken.None);
        await _model.PaletteQuery.SetAsync("toggle grid", CancellationToken.None);
        await When(_model.PaletteItems, items => items.Count > 0 && items[0].Id == "view.grid");
        Assert.True(_engine.Options.ShowGrid);

        await _model.RunPalette(null, CancellationToken.None);

        Assert.False(_engine.Options.ShowGrid);
        Assert.False(await _model.IsPaletteOpen);
    }

    [Fact]
    public async Task The_highlight_wraps_and_a_row_runs_its_own_entry()
    {
        await _model.TogglePalette(CancellationToken.None);
        await _model.PaletteQuery.SetAsync("zoom", CancellationToken.None);
        var items = await When(_model.PaletteItems, i => i.Count >= 2 && i.All(x => x.Title.Contains("Zoom", StringComparison.OrdinalIgnoreCase)));

        await _model.MovePaletteHighlight(-1, CancellationToken.None);
        var wrapped = await When(_model.PaletteItems, i => i[^1].RowBrushKey == "SurfaceHoverBrush");
        Assert.Equal("TransparentBrush", wrapped[0].RowBrushKey);

        await _model.RunPalette("view.zoomIn", CancellationToken.None);
        Assert.False(await _model.IsPaletteOpen);
        Assert.Equal(items.Count, wrapped.Count);
    }

    [Fact]
    public async Task An_unavailable_entry_says_so_instead_of_running()
    {
        await _model.TogglePalette(CancellationToken.None);
        await _model.PaletteQuery.SetAsync("split member", CancellationToken.None);
        var items = await When(_model.PaletteItems, i => i.Count > 0 && i[0].Id == "structure.split");
        Assert.True(items[0].IsDisabled);

        await _model.RunPalette("structure.split", CancellationToken.None);

        Assert.Contains(new ToastMessage("Split member at midpoint is not available right now", false), _toasts);
    }

    [Fact]
    public async Task Preset_fields_rebuild_the_tiles_and_a_tile_loads_with_them()
    {
        Assert.Contains("12 m", (await _model.Presets)!.First(p => p.Id == "pratt").Size);

        await _model.PresetSpan.SetAsync(24, CancellationToken.None);
        await _model.PresetPanels.SetAsync(8, CancellationToken.None);
        await _model.PresetDepth.SetAsync(3, CancellationToken.None);
        var tiles = await When(_model.Presets, p => p.First(t => t.Id == "pratt").Size.StartsWith("24 m"));
        Assert.Equal("24 m · 16 N · 29 M", tiles.First(t => t.Id == "pratt").Size);

        await _model.LoadPreset(tiles.First(t => t.Id == "pratt").Choice, CancellationToken.None);

        Assert.Equal("Pratt truss", _engine.Document.Name);
        Assert.Equal(16, _engine.Document.Nodes.Count);
        Assert.True(_engine.IsDirty);
    }

    [Fact]
    public async Task A_tile_loads_with_the_values_it_is_handed_clamped_and_the_fields_show_them()
    {
        // What the sheet hands over when a value was typed without Enter: the fields' values, not yet in the states.
        await _model.LoadPreset(new PresetChoice("warren", 12, 1000, 2), CancellationToken.None);

        Assert.Equal(PresetParameters.MaxPanels, await When(_model.PresetPanels, v => v == PresetParameters.MaxPanels));
        Assert.Equal(TrussPresets.Build("warren", new PresetParameters(12, PresetParameters.MaxPanels, 2)).Nodes.Count, _engine.Document.Nodes.Count);
        var tiles = await When(_model.Presets, p => p.First(t => t.Id == "warren").Choice.Panels == PresetParameters.MaxPanels);
        Assert.Equal(new PresetChoice("pratt", 12, PresetParameters.MaxPanels, 2), tiles.First(t => t.Id == "pratt").Choice);
    }

    [Fact]
    public async Task Tabs_switch_between_the_inspector_and_the_outline()
    {
        await _model.SelectTabAsync(outline: true);
        await When(_model.OutlineTabSelected, v => v);
        await When(_model.InspectorTabSelected, v => !v);

        _model.ShowInspectorTab();
        await When(_model.InspectorTabSelected, v => v);
    }

    [Fact]
    public async Task Tool_commands_switch_the_tool_and_the_rail_follows()
    {
        _model.MemberTool();

        Assert.Equal(ToolKind.Member, _engine.Interaction.ActiveTool);
        await When(_model.IsMemberActive, v => v);
        await When(_model.IsSelectActive, v => !v);
    }

    [Fact]
    public void A_failure_reported_by_a_view_becomes_an_error_toast()
    {
        _model.ReportFailure("Command palette", new IOException("Access to the path '/x/y/settings.json' is denied."));

        Assert.Equal(new ToastMessage("Command palette failed: Access to the path 'settings.json' is denied.", true), Assert.Single(_toasts));
    }
}
