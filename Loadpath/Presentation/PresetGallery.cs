using System.Collections.Immutable;
using Loadpath.Core.Samples;
using Microsoft.Extensions.Logging;

namespace Loadpath.Presentation;

/// <summary>
/// The presets sheet as MVUX states: span, panels and depth (clamped to what the generators build), and a tile per
/// preset drawn at those values. Changing a field rebuilds the tiles; a tile loads its preset.
/// </summary>
public sealed class PresetGallery(EditorEngine engine, IUiThread ui, ILogger<PresetGallery> log)
{
    private static readonly PresetParameters StartParameters = new(12, 6, 2);

    public IState<double> PresetSpan => State.Value(this, () => StartParameters.Span).ForEach(async (v, ct) => await RebuildAsync(ct));
    public IState<double> PresetPanels => State.Value(this, () => (double)StartParameters.Panels).ForEach(async (v, ct) => await RebuildAsync(ct));
    public IState<double> PresetDepth => State.Value(this, () => StartParameters.Depth).ForEach(async (v, ct) => await RebuildAsync(ct));
    public IListState<PresetItem> Presets => ListState.Value(this, () => BuildItems(StartParameters));

    private async ValueTask<PresetParameters> ReadParametersAsync(CancellationToken ct)
    {
        var raw = new PresetParameters(await PresetSpan, (int)Math.Round(await PresetPanels), await PresetDepth);
        var p = raw.Clamped();
        // Write clamped values back so the fields show what will be built. Equal values do not re-fire ForEach.
        if (p.Span != raw.Span) await PresetSpan.SetAsync(p.Span, ct);
        if (p.Panels != await PresetPanels) await PresetPanels.SetAsync(p.Panels, ct);
        if (p.Depth != raw.Depth) await PresetDepth.SetAsync(p.Depth, ct);
        return p;
    }

    private async ValueTask RebuildAsync(CancellationToken ct)
    {
        var p = await ReadParametersAsync(ct);
        await Presets.UpdateAsync(_ => BuildItems(p), ct);
    }

    /// <summary>
    /// Load a preset with the parameters the tile hands over: the sheet's fields as they were when the tile was clicked,
    /// a value still being typed included (the tile commits it first; the states may not have caught up yet). The
    /// fields then show the clamped values that were built.
    /// </summary>
    public async ValueTask LoadAsync(PresetChoice choice, CancellationToken ct)
    {
        var p = new PresetParameters(choice.Span, (int)Math.Round(choice.Panels), choice.Depth).Clamped();
        log.LogDebug("preset {Id} span={Span} panels={Panels} depth={Depth}", choice.Id, p.Span, p.Panels, p.Depth);
        ui.Run(() => engine.LoadPreset(choice.Id, p));
        await PresetSpan.SetAsync(p.Span, ct);
        await PresetPanels.SetAsync(p.Panels, ct);
        await PresetDepth.SetAsync(p.Depth, ct);
    }

    private static IImmutableList<PresetItem> BuildItems(PresetParameters p) =>
        TrussPresets.Catalog.Select(preset =>
        {
            var snapshot = TrussPresets.Build(preset.Id, p);
            var k = snapshot.Members.Count;
            var size = preset.UsesPanels ? $"{p.Span:0.#} m · {snapshot.Nodes.Count} N · {k} M" : $"{p.Span:0.#} m · rise {p.Depth:0.#} m";
            return new PresetItem(preset.Id, preset.Title.ToUpperInvariant(), preset.Description, PresetSketch.Of(snapshot), size, $"Load {preset.Title} preset",
                new PresetChoice(preset.Id, p.Span, p.Panels, p.Depth));
        }).ToImmutableList();
}
