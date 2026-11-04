using Loadpath.Presentation;
using Loadpath.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loadpath.Tests;

/// <summary>The app's container (AppServices): every service resolves, once, around one engine.</summary>
public sealed class CompositionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "loadpath-di-" + Guid.NewGuid().ToString("N"));

    public CompositionTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // The window and its dispatcher only matter once a picker, a dialog or a timer runs; the host's UI thread and
        // the settings folder are replaced by the tests' own.
        services.AddLoadpath(window: null!, ui: null!);
        services.AddSingleton<IUiThread>(new TestUiThread());
        services.AddSingleton<ISettingsStore>(new MemorySettingsStore(Path.Combine(_folder, "autosave.loadpath")));
        // The generated view model needs the UI dispatcher it binds on, so the tests take the model it wraps directly,
        // with the same constructor arguments the container gives the view model.
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<EditorModel>(sp));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public async Task Every_service_the_page_resolves_is_built_around_one_engine()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<EditorEngine>();

        Assert.NotNull(provider.GetRequiredService<EditorStartup>());
        Assert.NotNull(provider.GetRequiredService<CloseGuard>());
        Assert.Same(provider.GetRequiredService<DocumentSession>(), provider.GetRequiredService<DocumentSession>());
        Assert.Same(engine, provider.GetRequiredService<EditorEngine>());
        // The session registered the File commands on that engine.
        Assert.NotNull(engine.Commands.Find("file.save"));
    }

    [Fact]
    public async Task Toasts_from_the_engine_reach_the_one_queue()
    {
        await using var provider = Build();
        var queue = provider.GetRequiredService<ToastQueue>();

        provider.GetRequiredService<EditorEngine>().Toast("Nothing selected");

        Assert.Equal("Nothing selected", queue.Current?.Text);
    }

    [Fact]
    public async Task Settings_changes_are_saved_through_the_container_once_started()
    {
        await using var provider = Build();
        var store = (MemorySettingsStore)provider.GetRequiredService<ISettingsStore>();
        provider.GetRequiredService<EditorStartup>().BeforeViews();

        provider.GetRequiredService<EditorEngine>().Options.ShowGrid = false;

        Assert.False(store.Saved!.ShowGrid);
    }

    [Fact]
    public async Task The_page_model_resolves_and_projects_the_engine()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<EditorEngine>();
        var model = provider.GetRequiredService<EditorModel>();
        Assert.Same(model, provider.GetRequiredService<EditorModel>());

        engine.LoadSample("warren");

        var clock = System.Diagnostics.Stopwatch.StartNew();
        while ((await model.Status)!.IsEmpty && clock.ElapsedMilliseconds < 5000) await Task.Delay(10);
        Assert.Equal("Warren truss", (await model.Status)!.DocumentName);
    }
}
