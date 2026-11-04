namespace Loadpath.Presentation;

/// <summary>
/// The view options as MVUX states: display mode, snapping, the inspector, the touch modifiers, the theme and the
/// reduce-noise layers. The engine's <see cref="ViewOptions"/> is what the renderer and the tools read; a state the view
/// changes is pushed there, and every option change is pulled back into the states.
/// </summary>
/// <remarks>
/// The two directions meet in the states the view writes (the touch multi-select, reduce noise and the layer toggles).
/// MVUX calls a state's ForEach on a pool thread some time after the update that raised it, so a flag held across the
/// pull's awaits cannot tell the pull's own echoes from the view's changes, and a toggle made while a pull runs would be
/// dropped. Each of those states has a <see cref="Link"/>: a pull writes the state only when the engine's value moved
/// since the last pull, so it never overwrites a toggle on its way to the engine; a change goes to the engine only when
/// it is the state's latest value and not the value the last pull wrote, so an echo, or a value a later change replaced,
/// never reaches it.
/// </remarks>
public sealed class OptionMirror(EditorEngine engine, IUiThread ui)
{
    private readonly Projection _projection = new();
    private readonly Link _multiSelect = new(o => o.MultiSelect, (o, v) => o.MultiSelect = v);
    private readonly Link _reduceNoise = new(o => o.ReduceNoise, (o, v) => o.ReduceNoise = v);
    private readonly Link _keepGrid = Layer(NoiseLayer.Grid), _keepDimensions = Layer(NoiseLayer.Dimensions),
        _keepSupports = Layer(NoiseLayer.Supports), _keepLoads = Layer(NoiseLayer.Loads), _keepOverCapacity = Layer(NoiseLayer.OverCapacity),
        _keepMemberForces = Layer(NoiseLayer.MemberForces), _keepReactions = Layer(NoiseLayer.Reactions), _keepDeflection = Layer(NoiseLayer.Deflection),
        _keepPartNames = Layer(NoiseLayer.PartNames), _keepLegend = Layer(NoiseLayer.Legend), _keepStructureList = Layer(NoiseLayer.StructureList),
        _keepResultDetails = Layer(NoiseLayer.ResultDetails);

    private ViewOptions Options => engine.Options;

    public IState<bool> IsForces => State.Value(this, () => Options.DisplayMode == DisplayMode.Forces);
    public IState<bool> IsUtilization => State.Value(this, () => Options.DisplayMode == DisplayMode.Utilization);
    public IState<string> ExaggerationText => State.Value(this, () => ExaggerationLabel());
    public IState<string> SnapText => State.Value(this, () => SnapLabel());
    /// <summary>Whether the inspector shows in the current layout (docked column or sheet).</summary>
    public IState<bool> InspectorShown => State.Value(this, () => Options.InspectorShown);
    public IState<bool> MultiSelect => State.Value(this, () => _multiSelect.Seed(Options)).ForEach(async (v, ct) => await PushAsync(MultiSelect, _multiSelect, v));
    public IState<bool> FreePlacement => State.Value(this, () => Options.FreePlacement);
    public IState<bool> IsThemeSystem => State.Value(this, () => Options.Theme == ThemeChoice.System);
    public IState<bool> IsThemeLight => State.Value(this, () => Options.Theme == ThemeChoice.Light);
    public IState<bool> IsThemeDark => State.Value(this, () => Options.Theme == ThemeChoice.Dark);

    // ---- reduce noise: the toggle, which layers stay, and what the panels show ----
    // One declaration per layer: MVUX keys each state by its declaring member, so a shared factory would merge them.

    public IState<bool> ReduceNoise => State.Value(this, () => _reduceNoise.Seed(Options)).ForEach(async (v, ct) => await PushAsync(ReduceNoise, _reduceNoise, v));
    public IState<bool> KeepGrid => State.Value(this, () => _keepGrid.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepGrid, _keepGrid, v));
    public IState<bool> KeepDimensions => State.Value(this, () => _keepDimensions.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepDimensions, _keepDimensions, v));
    public IState<bool> KeepSupports => State.Value(this, () => _keepSupports.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepSupports, _keepSupports, v));
    public IState<bool> KeepLoads => State.Value(this, () => _keepLoads.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepLoads, _keepLoads, v));
    public IState<bool> KeepOverCapacity => State.Value(this, () => _keepOverCapacity.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepOverCapacity, _keepOverCapacity, v));
    public IState<bool> KeepMemberForces => State.Value(this, () => _keepMemberForces.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepMemberForces, _keepMemberForces, v));
    public IState<bool> KeepReactions => State.Value(this, () => _keepReactions.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepReactions, _keepReactions, v));
    public IState<bool> KeepDeflection => State.Value(this, () => _keepDeflection.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepDeflection, _keepDeflection, v));
    public IState<bool> KeepPartNames => State.Value(this, () => _keepPartNames.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepPartNames, _keepPartNames, v));
    public IState<bool> KeepLegend => State.Value(this, () => _keepLegend.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepLegend, _keepLegend, v));
    public IState<bool> KeepStructureList => State.Value(this, () => _keepStructureList.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepStructureList, _keepStructureList, v));
    public IState<bool> KeepResultDetails => State.Value(this, () => _keepResultDetails.Seed(Options)).ForEach(async (v, ct) => await PushAsync(KeepResultDetails, _keepResultDetails, v));
    public IState<string> NoiseSummary => State.Value(this, () => NoiseSummaryText());
    public IState<string> NoiseStatus => State.Value(this, () => NoiseStatusText());
    public IState<bool> ShowStructureList => State.Value(this, () => Options.IsVisible(NoiseLayer.StructureList));
    public IState<bool> ShowResultDetails => State.Value(this, () => Options.IsVisible(NoiseLayer.ResultDetails));

    /// <summary>Apply a change to the engine's options on the UI thread (MVUX runs commands and callbacks off it).</summary>
    public void Set(Action<ViewOptions> apply) => ui.Run(() => apply(Options));

    private static Link Layer(NoiseLayer layer) => new(o => (o.Keep & layer) != 0, (o, keep) => o.SetKeep(layer, keep));

    /// <summary>A change the view made to a linked state: to the engine, unless it is an echo or already replaced.</summary>
    private async ValueTask PushAsync(IState<bool> state, Link link, bool value)
    {
        if (!link.IsChange(value) || await state != value) return;
        Set(o => link.Write(o, value));
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

    /// <summary>An option changed in the engine: bring the states up to date.</summary>
    public async ValueTask PullAsync()
    {
        var t = _projection.Start();
        var o = Options;
        await IsForces.UpdateAsync(current => t.IsLatest ? (o.DisplayMode == DisplayMode.Forces) : current);
        await IsUtilization.UpdateAsync(current => t.IsLatest ? (o.DisplayMode == DisplayMode.Utilization) : current);
        await ExaggerationText.UpdateAsync(current => t.IsLatest ? (ExaggerationLabel()) : current);
        await SnapText.UpdateAsync(current => t.IsLatest ? (SnapLabel()) : current);
        await InspectorShown.UpdateAsync(current => t.IsLatest ? (o.InspectorShown) : current);
        await PullAsync(MultiSelect, _multiSelect);
        await FreePlacement.UpdateAsync(current => t.IsLatest ? (o.FreePlacement) : current);
        await IsThemeSystem.UpdateAsync(current => t.IsLatest ? (o.Theme == ThemeChoice.System) : current);
        await IsThemeLight.UpdateAsync(current => t.IsLatest ? (o.Theme == ThemeChoice.Light) : current);
        await IsThemeDark.UpdateAsync(current => t.IsLatest ? (o.Theme == ThemeChoice.Dark) : current);
        await PullAsync(ReduceNoise, _reduceNoise);
        await PullAsync(KeepGrid, _keepGrid);
        await PullAsync(KeepDimensions, _keepDimensions);
        await PullAsync(KeepSupports, _keepSupports);
        await PullAsync(KeepLoads, _keepLoads);
        await PullAsync(KeepOverCapacity, _keepOverCapacity);
        await PullAsync(KeepMemberForces, _keepMemberForces);
        await PullAsync(KeepReactions, _keepReactions);
        await PullAsync(KeepDeflection, _keepDeflection);
        await PullAsync(KeepPartNames, _keepPartNames);
        await PullAsync(KeepLegend, _keepLegend);
        await PullAsync(KeepStructureList, _keepStructureList);
        await PullAsync(KeepResultDetails, _keepResultDetails);
        await NoiseSummary.UpdateAsync(current => t.IsLatest ? (NoiseSummaryText()) : current);
        await NoiseStatus.UpdateAsync(current => t.IsLatest ? (NoiseStatusText()) : current);
        await ShowStructureList.UpdateAsync(current => t.IsLatest ? (o.IsVisible(NoiseLayer.StructureList)) : current);
        await ShowResultDetails.UpdateAsync(current => t.IsLatest ? (o.IsVisible(NoiseLayer.ResultDetails)) : current);
    }

    // The engine's value is read when MVUX applies the update, so a pull that started earlier still writes the latest.
    private async ValueTask PullAsync(IState<bool> state, Link link) => await state.UpdateAsync(current => link.Take(Options, current));

    /// <summary>A state the view writes and the engine's option it mirrors, with the value the last pull wrote into the state.</summary>
    private sealed class Link(Func<ViewOptions, bool> read, Action<ViewOptions, bool> write)
    {
        private readonly object _gate = new();
        private bool _pulled;
        private bool _known;

        public void Write(ViewOptions o, bool value) => write(o, value);

        /// <summary>The state's first value: the engine's, which counts as pulled.</summary>
        public bool Seed(ViewOptions o)
        {
            var value = read(o);
            lock (_gate)
            {
                if (!_known) (_pulled, _known) = (value, true);
            }
            return value;
        }

        /// <summary>What a pull writes: the engine's value when it moved since the last pull, else the state's own.</summary>
        public bool Take(ViewOptions o, bool current)
        {
            var value = read(o);
            lock (_gate)
            {
                if (_known && value == _pulled) return current;
                (_pulled, _known) = (value, true);
                return value;
            }
        }

        /// <summary>False for the value the last pull wrote: its echo, or a change back to it before the engine moved.</summary>
        public bool IsChange(bool value)
        {
            lock (_gate) return !_known || value != _pulled;
        }
    }
}
