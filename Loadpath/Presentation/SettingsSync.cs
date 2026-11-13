using Loadpath.Services;

namespace Loadpath.Presentation;

/// <summary>
/// The saved half of the view options: applied from the settings file at start, written back on every option change
/// from then on. Before the settings are applied nothing is written, or the defaults would overwrite them.
/// </summary>
public sealed class SettingsSync
{
    private readonly EditorEngine _engine;
    private readonly ISettingsStore _store;
    private SavedSettings _saved = new();
    private bool _applied;
    private bool _saveFailing;

    public SettingsSync(EditorEngine engine, ISettingsStore store)
    {
        _engine = engine;
        _store = store;
        engine.OptionsChanged += (_, _) => Persist();
    }

    /// <summary>Apply saved settings to the view options. From here on, option changes are saved.</summary>
    /// <param name="adjust">Changes for this session only, made before saving starts (a Debug fixture's theme).</param>
    public void Apply(SavedSettings saved, Action<ViewOptions>? adjust = null)
    {
        _saved = saved;
        Apply(saved, _engine.Options);
        adjust?.Invoke(_engine.Options);
        _applied = true;
    }

    private void Persist()
    {
        if (!_applied) return;
        Capture(_engine.Options, _saved);
        var error = _store.Save(_saved);
        // Reported when the failures start: toggling a layer ten times does not raise ten toasts.
        if (error is not null && !_saveFailing) _engine.ToastError($"Settings not saved: {error.TrimEnd('.')}");
        _saveFailing = error is not null;
    }

    public static void Apply(SavedSettings m, ViewOptions o)
    {
        o.DisplayMode = Enum.TryParse<DisplayMode>(m.DisplayMode, out var mode) ? mode : DisplayMode.Forces;
        o.ShowDeflection = m.ShowDeflection;
        o.Exaggeration = Math.Clamp(m.Exaggeration, 0.2, 3);
        o.ShowLabels = m.ShowLabels;
        o.ShowReactions = m.ShowReactions;
        o.ShowGrid = m.ShowGrid;
        o.SnapEnabled = m.SnapEnabled;
        o.GridStep = m.GridStep > 0 ? m.GridStep : 0.5;
        o.InspectorVisible = m.InspectorVisible;
        o.ReduceNoise = m.ReduceNoise;
        o.Keep = (NoiseLayer)m.NoiseKeep;
        o.Theme = Enum.TryParse<ThemeChoice>(m.Theme, out var theme) ? theme : ThemeChoice.System;
    }

    public static void Capture(ViewOptions o, SavedSettings m)
    {
        m.DisplayMode = o.DisplayMode.ToString();
        m.ShowDeflection = o.ShowDeflection;
        m.Exaggeration = o.Exaggeration;
        m.ShowLabels = o.ShowLabels;
        m.ShowReactions = o.ShowReactions;
        m.ShowGrid = o.ShowGrid;
        m.SnapEnabled = o.SnapEnabled;
        m.GridStep = o.GridStep;
        m.InspectorVisible = o.InspectorVisible;
        m.ReduceNoise = o.ReduceNoise;
        m.NoiseKeep = (int)o.Keep;
        m.Theme = o.Theme.ToString();
    }
}
