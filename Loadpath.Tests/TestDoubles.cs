using Loadpath.Presentation;
using Loadpath.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loadpath.Tests;

/// <summary>A UI thread that runs everything at once, with timers the test fires by hand.</summary>
public sealed class TestUiThread : IUiThread
{
    private readonly List<Timer> _timers = new();

    public void Run(Action action) => action();

    public IUiTimer CreateTimer(TimeSpan interval, Action tick)
    {
        var timer = new Timer(interval, tick);
        _timers.Add(timer);
        return timer;
    }

    public IReadOnlyList<Timer> Timers => _timers;

    /// <summary>Tick every running timer, as if its interval had passed with nothing else happening.</summary>
    public void Elapse()
    {
        foreach (var t in _timers.Where(t => t.IsRunning).ToList()) t.Tick();
    }

    public sealed class Timer(TimeSpan interval, Action tick) : IUiTimer
    {
        public TimeSpan Interval { get; } = interval;
        public bool IsRunning { get; private set; }
        public int Restarts { get; private set; }

        public void Restart()
        {
            IsRunning = true;
            Restarts++;
        }

        public void Stop() => IsRunning = false;

        public void Tick()
        {
            IsRunning = false;
            tick();
        }
    }
}

/// <summary>Settings in memory; the autosave in a real file the test owns. <see cref="Failing"/> makes saving fail.</summary>
public sealed class MemorySettingsStore(string autosavePath) : ISettingsStore
{
    public SavedSettings? Saved { get; private set; }
    public int SaveCount { get; private set; }
    public string? Failing { get; set; }
    public SavedSettings OnDisk { get; set; } = new();

    public string AutosavePath => autosavePath;

    public SavedSettings Load() => OnDisk;

    public Task<SavedSettings> LoadAsync() => Task.FromResult(OnDisk);

    public string? Save(SavedSettings settings)
    {
        SaveCount++;
        if (Failing is not null) return Failing;
        Saved = new SavedSettings
        {
            DisplayMode = settings.DisplayMode, ShowDeflection = settings.ShowDeflection, Exaggeration = settings.Exaggeration,
            ShowLabels = settings.ShowLabels, ShowReactions = settings.ShowReactions, ShowGrid = settings.ShowGrid,
            SnapEnabled = settings.SnapEnabled, GridStep = settings.GridStep, InspectorVisible = settings.InspectorVisible,
            ReduceNoise = settings.ReduceNoise, NoiseKeep = settings.NoiseKeep, Theme = settings.Theme,
        };
        return null;
    }
}

/// <summary>Answers the unsaved-changes question with a scripted choice and records who asked.</summary>
public sealed class ScriptedPrompt(UnsavedChoice answer) : IUnsavedPrompt
{
    public List<string> Asked { get; } = new();
    public UnsavedChoice Answer { get; set; } = answer;

    public Task<UnsavedChoice> AskAsync(string documentName)
    {
        Asked.Add(documentName);
        return Task.FromResult(Answer);
    }
}

/// <summary>The page model with its pieces, as the container builds it (AppServices), around the test's engine and UI thread.</summary>
public static class Models
{
    public static EditorModel Editor(EditorEngine engine, IUiThread ui) => new(engine, ui, Logs.For<EditorModel>(),
        new StatusProjection(engine), new OptionMirror(engine, ui), new InspectorProjection(engine, ui), new OutlineProjection(engine, ui),
        new PaletteSearch(engine, ui, Logs.For<PaletteSearch>()), new PresetGallery(engine, ui, Logs.For<PresetGallery>()));
}

public static class Logs
{
    public static Microsoft.Extensions.Logging.ILogger<T> For<T>() => NullLogger<T>.Instance;
}
