using Loadpath.Presentation;
using Loadpath.Services;
using Loadpath.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Loadpath;

/// <summary>The app's services, one instance each for the window's life (the theme switcher per window scope, as the theme service is).</summary>
public static class AppServices
{
    public static IServiceCollection AddLoadpath(this IServiceCollection services, Window window, DispatcherQueue ui)
    {
        // The window and its dispatcher, for the Extensions theme service (UseThemeSwitching registers it per scope, as
        // navigation would; App opens one scope for the window). Both are created when first asked for, on the UI thread.
        services.AddSingleton<Window>(_ => window);
        services.AddSingleton<IDispatcher>(_ => new Dispatcher(window));
        services.AddSingleton<IUiThread>(new DispatcherUiThread(ui));
        services.AddSingleton<EditorEngine>();
        services.AddSingleton<ToastQueue>();
        services.AddSingleton<ISettingsStore, SettingsService>();
        services.AddSingleton<SettingsSync>();
        services.AddSingleton<IDocumentFiles>(sp => new FileService(() => window, sp.GetRequiredService<ILogger<FileService>>()));
        services.AddSingleton<IUnsavedPrompt>(new UnsavedChangesDialog(window));
        services.AddSingleton<DocumentSession>();
        services.AddSingleton<CloseGuard>();
        services.AddScoped<ThemeSwitcher>();
        // The page model's pieces: each projects one part of the editor into MVUX states.
        services.AddSingleton<StatusProjection>();
        services.AddSingleton<OptionMirror>();
        services.AddSingleton<InspectorProjection>();
        services.AddSingleton<OutlineProjection>();
        services.AddSingleton<PaletteSearch>();
        services.AddSingleton<PresetGallery>();
        // The generated view model builds the model; the page binds the view model, code that needs the model gets it here.
        // EditorModel captures nothing thread-bound, but the page resolves both on the UI thread anyway.
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton(sp => sp.GetRequiredService<EditorViewModel>().Model);
#if DEBUG
        services.AddSingleton<DebugFixtures>();
#endif
        services.AddSingleton<EditorStartup>();
        return services;
    }
}
