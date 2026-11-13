using Loadpath.Presentation;
using Loadpath.Services;
using Xunit;

namespace Loadpath.Tests;

/// <summary>The saved view options: applied at start, written on change from then on, failures said once.</summary>
public class SettingsSyncTests
{
    private readonly EditorEngine _engine = new();
    private readonly MemorySettingsStore _store = new("unused");
    private readonly List<ToastMessage> _toasts = new();

    public SettingsSyncTests() => _engine.ToastRequested += (_, t) => _toasts.Add(t);

    [Fact]
    public void Nothing_is_written_before_the_saved_settings_are_applied()
    {
        var sync = new SettingsSync(_engine, _store);

        _engine.Options.ShowGrid = false;

        Assert.Equal(0, _store.SaveCount);
        GC.KeepAlive(sync);
    }

    [Fact]
    public void Applied_settings_reach_the_options_and_later_changes_are_saved()
    {
        var sync = new SettingsSync(_engine, _store);
        sync.Apply(new SavedSettings { DisplayMode = "Utilization", Theme = "Dark", Exaggeration = 9, NoiseKeep = (int)NoiseLayer.Grid });

        Assert.Equal(DisplayMode.Utilization, _engine.Options.DisplayMode);
        Assert.Equal(ThemeChoice.Dark, _engine.Options.Theme);
        Assert.Equal(3, _engine.Options.Exaggeration);
        Assert.Equal(NoiseLayer.Grid, _engine.Options.Keep);
        Assert.Equal(0, _store.SaveCount);

        _engine.Options.ShowReactions = false;

        Assert.False(_store.Saved!.ShowReactions);
        Assert.Equal("Utilization", _store.Saved.DisplayMode);
    }

    [Fact]
    public void A_session_only_adjustment_is_not_saved_until_something_else_changes()
    {
        var sync = new SettingsSync(_engine, _store);

        sync.Apply(new SavedSettings { Theme = "System" }, o => o.Theme = ThemeChoice.Light);

        Assert.Equal(ThemeChoice.Light, _engine.Options.Theme);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public void Unknown_values_fall_back_to_the_defaults()
    {
        var sync = new SettingsSync(_engine, _store);
        sync.Apply(new SavedSettings { DisplayMode = "Sideways", Theme = "Purple", GridStep = 0 });

        Assert.Equal(DisplayMode.Forces, _engine.Options.DisplayMode);
        Assert.Equal(ThemeChoice.System, _engine.Options.Theme);
        Assert.Equal(0.5, _engine.Options.GridStep);
    }

    [Fact]
    public void A_failing_write_is_said_once_per_failing_stretch()
    {
        var sync = new SettingsSync(_engine, _store);
        sync.Apply(new SavedSettings());
        _store.Failing = "Access to the path '/home/ann/.local/share/Loadpath/settings.json' is denied.";

        _engine.Options.ShowGrid = false;
        _engine.Options.ShowGrid = true;
        _store.Failing = null;
        _engine.Options.ShowGrid = false;
        _store.Failing = "Disk full.";
        _engine.Options.ShowGrid = true;

        Assert.Equal(new[]
        {
            new ToastMessage("Settings not saved: Access to the path 'settings.json' is denied", true),
            new ToastMessage("Settings not saved: Disk full", true),
        }, _toasts);
    }

    [Fact]
    public void Capture_and_apply_round_trip_every_saved_option()
    {
        var o = _engine.Options;
        o.DisplayMode = DisplayMode.Utilization; o.ShowDeflection = false; o.Exaggeration = 1.7; o.ShowLabels = false;
        o.ShowReactions = false; o.ShowGrid = false; o.SnapEnabled = false; o.GridStep = 0.25; o.InspectorVisible = false;
        o.ReduceNoise = true; o.Keep = NoiseLayer.Loads | NoiseLayer.Legend; o.Theme = ThemeChoice.Dark;
        var saved = new SavedSettings();

        SettingsSync.Capture(o, saved);
        var other = new ViewOptions();
        SettingsSync.Apply(saved, other);

        Assert.Equal((o.DisplayMode, o.ShowDeflection, o.Exaggeration, o.ShowLabels, o.ShowReactions, o.ShowGrid, o.SnapEnabled, o.GridStep, o.InspectorVisible, o.ReduceNoise, o.Keep, o.Theme),
            (other.DisplayMode, other.ShowDeflection, other.Exaggeration, other.ShowLabels, other.ShowReactions, other.ShowGrid, other.SnapEnabled, other.GridStep, other.InspectorVisible, other.ReduceNoise, other.Keep, other.Theme));
    }
}
