using Loadpath.Services;

namespace Loadpath.Presentation;

/// <summary>
/// The order the editor starts in. The settings come first, so the first frame is drawn in the saved theme and layers;
/// then the document: the autosave, or in a Debug build a fixture (DebugFixtures) instead.
/// </summary>
public sealed class EditorStartup
{
    private readonly SettingsSync _settings;
    private readonly ISettingsStore _store;
    private readonly DocumentSession _session;
#if DEBUG
    private readonly DebugFixtures _fixtures;

    public EditorStartup(SettingsSync settings, ISettingsStore store, DocumentSession session, DebugFixtures fixtures)
    {
        _settings = settings;
        _store = store;
        _session = session;
        _fixtures = fixtures;
    }
#else
    public EditorStartup(SettingsSync settings, ISettingsStore store, DocumentSession session)
    {
        _settings = settings;
        _store = store;
        _session = session;
    }
#endif

    /// <summary>
    /// Before the views are built. The local folder is there at launch on every head but WebAssembly, which mounts its
    /// browser storage later; there the settings wait for <see cref="StartAsync"/>.
    /// </summary>
    public void BeforeViews()
    {
#if !__WASM__
        ApplySettings(Reset ? new SavedSettings() : _store.Load());
#endif
    }

    /// <summary>Once the page is loaded: the settings on WebAssembly, then the autosave or a fixture.</summary>
    public async Task StartAsync()
    {
#if __WASM__
        ApplySettings(Reset ? new SavedSettings() : await _store.LoadAsync());
#endif
#if DEBUG
        if (!_fixtures.Seed() && !Reset) await _session.RestoreAsync();
        _fixtures.ApplyHooks();
#else
        await _session.RestoreAsync();
#endif
    }

#if DEBUG
    private static bool Reset => DebugFixtures.Reset;

    private void ApplySettings(SavedSettings saved) => _settings.Apply(saved, DebugFixtures.ApplyTheme);
#else
    private static bool Reset => false;

    private void ApplySettings(SavedSettings saved) => _settings.Apply(saved);
#endif
}
