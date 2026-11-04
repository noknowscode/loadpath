using Uno.Extensions.Reactive.Commands;
using Uno.Extensions.Reactive.Config;
using Microsoft.Extensions.Logging;

namespace Loadpath.Presentation;

/// <summary>
/// The page's MVUX model: the binding surface and the commands the views send. The engine raises events; the pieces
/// project them into states (<see cref="StatusProjection"/>, <see cref="OptionMirror"/>, <see cref="InspectorProjection"/>,
/// <see cref="OutlineProjection"/>, <see cref="PaletteSearch"/>, <see cref="PresetGallery"/>), and this record exposes
/// their states for the generated EditorViewModel, the page's DataContext. It keeps the tool rail and the inspector's
/// tabs itself. Every piece is a singleton in the app's container.
/// </summary>
[ImplicitCommands(false)]
public partial record EditorModel
{
    private readonly EditorEngine _engine;
    private readonly IUiThread _ui;
    private readonly ILogger _log;
    private readonly StatusProjection _status;
    private readonly OptionMirror _options;
    private readonly InspectorProjection _inspector;
    private readonly OutlineProjection _outline;
    private readonly PaletteSearch _palette;
    private readonly PresetGallery _presets;
    private readonly Projection _interactionProjection = new();

    public EditorModel(EditorEngine engine, IUiThread ui, ILogger<EditorModel> log, StatusProjection status, OptionMirror options,
        InspectorProjection inspector, OutlineProjection outline, PaletteSearch palette, PresetGallery presets)
    {
        _engine = engine;
        _ui = ui;
        _log = log;
        _status = status;
        _options = options;
        _inspector = inspector;
        _outline = outline;
        _palette = palette;
        _presets = presets;
        engine.AnalysisChanged += (_, _) => Fire(RefreshAllAsync());
        engine.SelectionChanged += (_, _) => Fire(RefreshSelectionAsync());
        engine.HistoryChanged += (_, _) => Fire(status.RefreshStatusAsync());
        engine.SavedStateChanged += (_, _) => Fire(status.RefreshStatusAsync());
        engine.OptionsChanged += (_, _) => Fire(options.PullAsync());
        engine.Interaction.StateChanged += (_, _) => Fire(RefreshInteractionAsync());
        engine.ViewportChanged += (_, _) => Fire(RefreshInteractionAsync());
        engine.PaletteRequested += (_, _) => Fire(TogglePalette(default));
    }

    /// <summary>
    /// Generated commands and state callbacks run off the UI thread. The engine mutates the document and raises
    /// events that reach XAML, so every mutation runs on the UI thread.
    /// </summary>
    private void Ui(Action action) => _ui.Run(action);

    /// <summary>
    /// A UI action that failed (a palette key, a pointer press) is logged and becomes a toast instead of an unhandled
    /// exception. Internal so the MVUX generator does not turn it into a command.
    /// </summary>
    internal void ReportFailure(string action, Exception error)
    {
        _log.LogError(error, "{Action} failed", action);
        Ui(() => _engine.ToastError($"{action} failed: {error.Message}"));
    }

    /// <summary>Starts a projection without awaiting it; a fault is reported, never left unobserved.</summary>
    private void Fire(ValueTask task)
    {
        if (task.IsCompletedSuccessfully) return;
        _ = task.AsTask().ContinueWith(t => ReportFailure("Refresh", t.Exception!.GetBaseException()), TaskContinuationOptions.OnlyOnFaulted);
    }

    private async ValueTask RefreshAllAsync()
    {
        await _status.RefreshStatusAsync();
        await _inspector.RefreshAsync();
        await _outline.RefreshAsync();
        await _status.RefreshFailuresAsync();
    }

    private async ValueTask RefreshSelectionAsync()
    {
        await _inspector.RefreshAsync();
        await _outline.RefreshAsync();
    }

    // ---- status, failures and layer counts (StatusProjection) ----

    public IState<EditorStatus> Status => _status.Status;
    public IState<LayerCounts> Counts => _status.Counts;
    public IListState<FailureItem> Failures => _status.Failures;
    public IState<bool> HasFailures => _status.HasFailures;
    public IState<bool> CanLighten => _status.CanLighten;
    public IState<string> FailuresHeader => _status.FailuresHeader;

    // ---- view options (OptionMirror) ----

    public IState<bool> IsForces => _options.IsForces;
    public IState<bool> IsUtilization => _options.IsUtilization;
    public IState<string> ExaggerationText => _options.ExaggerationText;
    public IState<string> SnapText => _options.SnapText;
    public IState<bool> InspectorShown => _options.InspectorShown;
    public IState<bool> MultiSelect => _options.MultiSelect;
    public IState<bool> FreePlacement => _options.FreePlacement;
    public IState<bool> IsThemeSystem => _options.IsThemeSystem;
    public IState<bool> IsThemeLight => _options.IsThemeLight;
    public IState<bool> IsThemeDark => _options.IsThemeDark;
    public IState<bool> ReduceNoise => _options.ReduceNoise;
    public IState<bool> KeepGrid => _options.KeepGrid;
    public IState<bool> KeepDimensions => _options.KeepDimensions;
    public IState<bool> KeepSupports => _options.KeepSupports;
    public IState<bool> KeepLoads => _options.KeepLoads;
    public IState<bool> KeepOverCapacity => _options.KeepOverCapacity;
    public IState<bool> KeepMemberForces => _options.KeepMemberForces;
    public IState<bool> KeepReactions => _options.KeepReactions;
    public IState<bool> KeepDeflection => _options.KeepDeflection;
    public IState<bool> KeepPartNames => _options.KeepPartNames;
    public IState<bool> KeepLegend => _options.KeepLegend;
    public IState<bool> KeepStructureList => _options.KeepStructureList;
    public IState<bool> KeepResultDetails => _options.KeepResultDetails;
    public IState<string> NoiseSummary => _options.NoiseSummary;
    public IState<string> NoiseStatus => _options.NoiseStatus;
    public IState<bool> ShowStructureList => _options.ShowStructureList;
    public IState<bool> ShowResultDetails => _options.ShowResultDetails;

    [Command] public void ShowAll() => _options.Set(o => o.ReduceNoise = false);
    [Command] public void ToggleInspector() => _options.Set(o => o.ToggleInspector());
    [Command] public void ToggleFreePlacement() => _options.Set(o => o.FreePlacement = !o.FreePlacement);
    [Command] public void ThemeSystem() => _options.Set(o => o.Theme = ThemeChoice.System);
    [Command] public void ThemeLight() => _options.Set(o => o.Theme = ThemeChoice.Light);
    [Command] public void ThemeDark() => _options.Set(o => o.Theme = ThemeChoice.Dark);
    [Command] public void ResetNoiseLayers() => _options.Set(o => o.Keep = ViewOptions.DefaultKeep);
    [Command] public void ShowForces() => _options.Set(o => o.DisplayMode = DisplayMode.Forces);
    [Command] public void ShowUtilization() => _options.Set(o => o.DisplayMode = DisplayMode.Utilization);

    // ---- inspector tabs (below 1240 px the structure outline is the inspector's second tab) ----

    public IState<bool> InspectorTabSelected => State.Value(this, () => true);
    public IState<bool> OutlineTabSelected => State.Value(this, () => false);
    [Command] public void ShowInspectorTab() => Fire(SelectTabAsync(outline: false));
    [Command] public void ShowOutlineTab() => Fire(SelectTabAsync(outline: true));

    internal async ValueTask SelectTabAsync(bool outline)
    {
        await InspectorTabSelected.SetAsync(!outline);
        await OutlineTabSelected.SetAsync(outline);
    }

    // ---- interaction (tool, hint, cursor, zoom) ----

    public IState<bool> IsSelectActive => State.Value(this, () => true);
    public IState<bool> IsNodeActive => State.Value(this, () => false);
    public IState<bool> IsMemberActive => State.Value(this, () => false);
    public IState<bool> IsLoadActive => State.Value(this, () => false);
    public IState<bool> IsSupportActive => State.Value(this, () => false);
    public IState<bool> IsPanActive => State.Value(this, () => false);
    public IState<string> Hint => State.Value(this, () => _engine.Interaction.Hint);
    public IState<string> CursorText => State.Value(this, () => "");
    public IState<string> ZoomText => State.Value(this, () => "100%");

    private async ValueTask RefreshInteractionAsync()
    {
        var t = _interactionProjection.Start();
        var i = _engine.Interaction;
        await IsSelectActive.UpdateAsync(current => t.IsLatest ? (i.ActiveTool == ToolKind.Select) : current);
        await IsNodeActive.UpdateAsync(current => t.IsLatest ? (i.ActiveTool == ToolKind.Node) : current);
        await IsMemberActive.UpdateAsync(current => t.IsLatest ? (i.ActiveTool == ToolKind.Member) : current);
        await IsLoadActive.UpdateAsync(current => t.IsLatest ? (i.ActiveTool == ToolKind.Load) : current);
        await IsSupportActive.UpdateAsync(current => t.IsLatest ? (i.ActiveTool == ToolKind.Support) : current);
        await IsPanActive.UpdateAsync(current => t.IsLatest ? (i.ActiveTool == ToolKind.Pan) : current);
        await Hint.UpdateAsync(current => t.IsLatest ? (i.Hint) : current);
        await CursorText.UpdateAsync(current => t.IsLatest ? (i.CursorWorldText) : current);
        await ZoomText.UpdateAsync(current => t.IsLatest ? (i.ZoomText) : current);
    }

    [Command] public void SelectTool() => Ui(() => _engine.Interaction.SetTool(ToolKind.Select));
    [Command] public void NodeTool() => Ui(() => _engine.Interaction.SetTool(ToolKind.Node));
    [Command] public void MemberTool() => Ui(() => _engine.Interaction.SetTool(ToolKind.Member));
    [Command] public void LoadTool() => Ui(() => _engine.Interaction.SetTool(ToolKind.Load));
    [Command] public void SupportTool() => Ui(() => _engine.Interaction.SetTool(ToolKind.Support));
    [Command] public void PanTool() => Ui(() => _engine.Interaction.SetTool(ToolKind.Pan));

    // ---- edits (the engine) ----

    public IAsyncCommand Undo => Command.Create(b => b.Given(Status).When(s => s.CanUndo).Then(async (s, ct) => Ui(() => _engine.Commands.TryExecute("edit.undo"))));
    public IAsyncCommand Redo => Command.Create(b => b.Given(Status).When(s => s.CanRedo).Then(async (s, ct) => Ui(() => _engine.Commands.TryExecute("edit.redo"))));
    [Command] public void JumpToCritical() => Ui(() => _engine.JumpToCritical());
    [Command] public void SplitMember() => Ui(() => _engine.SplitSelectedMember());
    [Command] public void DeleteSelection() => Ui(() => _engine.DeleteSelection());
    [Command] public void DuplicateSelection() => Ui(() => _engine.DuplicateSelection());
    [Command] public void CycleSupport() => Ui(() => _engine.CycleSupportOnSelection());
    [Command] public void AddLoad() => Ui(() => _engine.AddDefaultLoadOnSelection());
    [Command] public void ClearLoads() => Ui(() => _engine.ClearLoadOnSelection());
    [Command] public void SupportNone() => Ui(() => SetSupport(SupportKind.None));
    [Command] public void SupportPin() => Ui(() => SetSupport(SupportKind.Pin));
    [Command] public void SupportRoller() => Ui(() => SetSupport(SupportKind.RollerY));
    [Command] public void SupportRollerX() => Ui(() => SetSupport(SupportKind.RollerX));
    [Command] public void Lighten() => Ui(() => _engine.LightenMembers());
    [Command] public void UpsizeFailing() => Ui(() => _engine.UpsizeFailingMembers());
    [Command] public void SelectMember(string key) => Ui(() => _engine.SelectByKey(key, false));
    [Command] public void SampleWarren() => Ui(() => _engine.LoadSample("warren"));
    [Command] public void SampleCantilever() => Ui(() => _engine.LoadSample("cantilever"));
    [Command] public void SampleRoof() => Ui(() => _engine.LoadSample("roof"));
    [Command] public void OpenFile() => Ui(() => _engine.Commands.TryExecute("file.open"));
    [Command] public void SaveFile() => Ui(() => _engine.Commands.TryExecute("file.save"));
    [Command] public void ShareDesign() => Ui(() => _engine.Commands.TryExecute("file.share"));
    [Command] public void PasteDesign() => Ui(() => _engine.Commands.TryExecute("file.paste"));

    /// <summary>Apply a section to the selected members, by section id (used by the bulk list).</summary>
    [Command] public void ApplySection(string sectionId) => Ui(() => _engine.SetSectionOnSelection(Section.FindById(sectionId)));

    private void SetSupport(SupportKind kind)
    {
        var sel = _engine.Selection;
        if (sel.Single is { IsNode: true } r)
        {
            if (_engine.Document.GetNode(r.Id).Support != kind) _engine.History.Do(new Core.Editing.SetSupportEdit(r.Id, kind));
        }
        else _engine.SetSupportOnSelection(kind);
    }

    // ---- inspector (InspectorProjection) ----

    public IState<InspectorContent> Inspector => _inspector.Inspector;
    public IState<NodeVector> NodePosition => _inspector.NodePosition;
    public IState<NodeVector> NodeLoad => _inspector.NodeLoad;
    public IState<SectionPick> MemberSection => _inspector.MemberSection;
    public IListState<ConnectedItem> ConnectedMembers => _inspector.ConnectedMembers;
    public IListFeed<SectionChoice> Sections => _inspector.Sections;

    // ---- outline (OutlineProjection) ----

    public IListState<OutlineItem> OutlineNodes => _outline.OutlineNodes;
    public IListState<OutlineItem> OutlineMembers => _outline.OutlineMembers;
    public IState<string> NodesHeader => _outline.NodesHeader;
    public IState<string> MembersHeader => _outline.MembersHeader;
    public IState<bool> OutlineIsEmpty => _outline.OutlineIsEmpty;
    public IState<bool> NodesExpanded => _outline.NodesExpanded;
    public IState<bool> MembersExpanded => _outline.MembersExpanded;
    [Command] public void ToggleNodes() => Fire(NodesExpanded.UpdateAsync(v => !v));
    [Command] public void ToggleMembers() => Fire(MembersExpanded.UpdateAsync(v => !v));

    // ---- presets sheet (PresetGallery) ----

    public IState<double> PresetSpan => _presets.PresetSpan;
    public IState<double> PresetPanels => _presets.PresetPanels;
    public IState<double> PresetDepth => _presets.PresetDepth;
    public IListState<PresetItem> Presets => _presets.Presets;

    /// <summary>Load a preset with the sheet's values the tile hands over (see <see cref="PresetGallery.LoadAsync"/>).</summary>
    [Command] public async ValueTask LoadPreset(PresetChoice choice, CancellationToken ct) => await _presets.LoadAsync(choice, ct);

    // ---- command palette (PaletteSearch) ----

    public IState<bool> IsPaletteOpen => _palette.IsPaletteOpen;
    public IState<string> PaletteQuery => _palette.PaletteQuery;
    public IListState<PaletteItem> PaletteItems => _palette.PaletteItems;

    [Command] public async ValueTask TogglePalette(CancellationToken ct) => await _palette.ToggleAsync(ct);
    [Command] public async ValueTask ClosePalette(CancellationToken ct) => await _palette.CloseAsync(ct);
    /// <summary>Run a palette entry: a row's command (its id) or Enter in the query (the highlighted entry).</summary>
    [Command] public async ValueTask RunPalette(string? id, CancellationToken ct) => await _palette.RunAsync(id, ct);
    public async ValueTask MovePaletteHighlight(int delta, CancellationToken ct) => await _palette.MoveHighlightAsync(delta, ct);
    public async ValueTask HighlightPaletteItem(string id, CancellationToken ct) => await _palette.HighlightAsync(id, ct);
}
