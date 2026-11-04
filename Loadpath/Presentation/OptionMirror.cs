namespace Loadpath.Presentation;

/// <summary>
/// The view options as MVUX states: display mode, snapping, the inspector, the touch modifiers, the theme and the
/// reduce-noise layers. The engine's <see cref="ViewOptions"/> is what the renderer and the tools read; a state the view
/// changes is pushed there, and every option change is pulled back into the states.
/// </summary>
public sealed class OptionMirror(EditorEngine engine, IUiThread ui)
{
    private readonly Projection _projection = new();
    private bool _syncing;

    private ViewOptions Options => engine.Options;

    public IState<bool> IsForces => State.Value(this, () => Options.DisplayMode == DisplayMode.Forces);
    public IState<bool> IsUtilization => State.Value(this, () => Options.DisplayMode == DisplayMode.Utilization);
    public IState<string> ExaggerationText => State.Value(this, () => ExaggerationLabel());
    public IState<string> SnapText => State.Value(this, () => SnapLabel());
    /// <summary>Whether the inspector shows in the current layout (docked column or sheet).</summary>
    public IState<bool> InspectorShown => State.Value(this, () => Options.InspectorShown);
    public IState<bool> MultiSelect => State.Value(this, () => Options.MultiSelect).ForEach(async (v, ct) => Push(o => o.MultiSelect = v));
    public IState<bool> FreePlacement => State.Value(this, () => Options.FreePlacement);
    public IState<bool> IsThemeSystem => State.Value(this, () => Options.Theme == ThemeChoice.System);
    public IState<bool> IsThemeLight => State.Value(this, () => Options.Theme == ThemeChoice.Light);
    public IState<bool> IsThemeDark => State.Value(this, () => Options.Theme == ThemeChoice.Dark);

    // ---- reduce noise: the toggle, which layers stay, and what the panels show ----
    // One declaration per layer: MVUX keys each state by its declaring member, so a shared factory would merge them.

    public IState<bool> ReduceNoise => State.Value(this, () => Options.ReduceNoise).ForEach(async (v, ct) => Push(o => o.ReduceNoise = v));
    public IState<bool> KeepGrid => State.Value(this, () => Keeps(NoiseLayer.Grid)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.Grid, v)));
    public IState<bool> KeepDimensions => State.Value(this, () => Keeps(NoiseLayer.Dimensions)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.Dimensions, v)));
    public IState<bool> KeepSupports => State.Value(this, () => Keeps(NoiseLayer.Supports)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.Supports, v)));
    public IState<bool> KeepLoads => State.Value(this, () => Keeps(NoiseLayer.Loads)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.Loads, v)));
    public IState<bool> KeepOverCapacity => State.Value(this, () => Keeps(NoiseLayer.OverCapacity)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.OverCapacity, v)));
    public IState<bool> KeepMemberForces => State.Value(this, () => Keeps(NoiseLayer.MemberForces)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.MemberForces, v)));
    public IState<bool> KeepReactions => State.Value(this, () => Keeps(NoiseLayer.Reactions)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.Reactions, v)));
    public IState<bool> KeepDeflection => State.Value(this, () => Keeps(NoiseLayer.Deflection)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.Deflection, v)));
    public IState<bool> KeepPartNames => State.Value(this, () => Keeps(NoiseLayer.PartNames)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.PartNames, v)));
    public IState<bool> KeepLegend => State.Value(this, () => Keeps(NoiseLayer.Legend)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.Legend, v)));
    public IState<bool> KeepStructureList => State.Value(this, () => Keeps(NoiseLayer.StructureList)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.StructureList, v)));
    public IState<bool> KeepResultDetails => State.Value(this, () => Keeps(NoiseLayer.ResultDetails)).ForEach(async (v, ct) => Push(o => o.SetKeep(NoiseLayer.ResultDetails, v)));
    public IState<string> NoiseSummary => State.Value(this, () => NoiseSummaryText());
    public IState<string> NoiseStatus => State.Value(this, () => NoiseStatusText());
    public IState<bool> ShowStructureList => State.Value(this, () => Options.IsVisible(NoiseLayer.StructureList));
    public IState<bool> ShowResultDetails => State.Value(this, () => Options.IsVisible(NoiseLayer.ResultDetails));

    /// <summary>Apply a change to the engine's options on the UI thread (MVUX runs commands and callbacks off it).</summary>
    public void Set(Action<ViewOptions> apply) => ui.Run(() => apply(Options));

    private bool Keeps(NoiseLayer layer) => (Options.Keep & layer) != 0;

    private void Push(Action<ViewOptions> apply)
    {
        if (_syncing) return;
        Set(apply);
    }

    private string SnapLabel() => Options.SnapEnabled ? $"{Options.GridStep:0.##} m" : "OFF";
    private string ExaggerationLabel() => $"×{Options.Exaggeration:0.0}";

    private string NoiseSummaryText()
    {
        var hidden = ViewOptions.LayerCount - System.Numerics.BitOperations.PopCount((uint)Options.Keep);
        return $"{(Options.ReduceNoise ? "ON" : "OFF")} · {hidden} {(hidden == 1 ? "LAYER" : "LAYERS")} HIDDEN";
    }

    private string NoiseStatusText()
    {
        var n = Options.HiddenLayerCount;
        return $"NOISE REDUCED · {n} {(n == 1 ? "LAYER" : "LAYERS")} HIDDEN";
    }

    /// <summary>An option changed in the engine: bring every state up to date.</summary>
    public async ValueTask PullAsync()
    {
        var t = _projection.Start();
        _syncing = true;
        try
        {
            var o = Options;
            await IsForces.UpdateAsync(current => t.IsLatest ? (o.DisplayMode == DisplayMode.Forces) : current);
            await IsUtilization.UpdateAsync(current => t.IsLatest ? (o.DisplayMode == DisplayMode.Utilization) : current);
            await ExaggerationText.UpdateAsync(current => t.IsLatest ? (ExaggerationLabel()) : current);
            await SnapText.UpdateAsync(current => t.IsLatest ? (SnapLabel()) : current);
            await InspectorShown.UpdateAsync(current => t.IsLatest ? (o.InspectorShown) : current);
            await MultiSelect.UpdateAsync(current => t.IsLatest ? (o.MultiSelect) : current);
            await FreePlacement.UpdateAsync(current => t.IsLatest ? (o.FreePlacement) : current);
            await IsThemeSystem.UpdateAsync(current => t.IsLatest ? (o.Theme == ThemeChoice.System) : current);
            await IsThemeLight.UpdateAsync(current => t.IsLatest ? (o.Theme == ThemeChoice.Light) : current);
            await IsThemeDark.UpdateAsync(current => t.IsLatest ? (o.Theme == ThemeChoice.Dark) : current);
            await ReduceNoise.UpdateAsync(current => t.IsLatest ? (o.ReduceNoise) : current);
            await KeepGrid.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.Grid) : current);
            await KeepDimensions.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.Dimensions) : current);
            await KeepSupports.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.Supports) : current);
            await KeepLoads.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.Loads) : current);
            await KeepOverCapacity.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.OverCapacity) : current);
            await KeepMemberForces.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.MemberForces) : current);
            await KeepReactions.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.Reactions) : current);
            await KeepDeflection.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.Deflection) : current);
            await KeepPartNames.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.PartNames) : current);
            await KeepLegend.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.Legend) : current);
            await KeepStructureList.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.StructureList) : current);
            await KeepResultDetails.UpdateAsync(current => t.IsLatest ? Keeps(NoiseLayer.ResultDetails) : current);
            await NoiseSummary.UpdateAsync(current => t.IsLatest ? (NoiseSummaryText()) : current);
            await NoiseStatus.UpdateAsync(current => t.IsLatest ? (NoiseStatusText()) : current);
            await ShowStructureList.UpdateAsync(current => t.IsLatest ? (o.IsVisible(NoiseLayer.StructureList)) : current);
            await ShowResultDetails.UpdateAsync(current => t.IsLatest ? (o.IsVisible(NoiseLayer.ResultDetails)) : current);
        }
        finally { _syncing = false; }
    }
}
