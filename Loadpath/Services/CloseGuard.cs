using Loadpath.Presentation;
using Microsoft.Extensions.Logging;

namespace Loadpath.Services;

/// <summary>
/// Asks before a window with unsaved changes closes. Desktop and WinAppSDK hold the close (AppWindow.Closing) and ask
/// Save, Discard or Cancel; a browser tab cannot be held that way, so WebAssembly arms the browser's own "leave this
/// page?" prompt while there are unsaved changes. Android and iOS have no close to intercept; the autosave keeps the work.
/// </summary>
public sealed class CloseGuard(EditorEngine engine, DocumentSession session, ILogger<CloseGuard> log)
{
    private bool _confirmed;

    /// <summary>True when closing now would lose work, so the close is held and <see cref="ConfirmAsync"/> asks.</summary>
    public bool HoldsClose => engine.IsDirty && !_confirmed;

    /// <summary>Ask about the unsaved work. True when the window may close now (saved or discarded); closing again is not held.</summary>
    public async Task<bool> ConfirmAsync()
    {
        if (!await session.ConfirmDiscardAsync(closing: true)) return false;
        _confirmed = true;
        return true;
    }

    public void Attach(Window window)
    {
#if __DESKTOP__ || WINDOWS
        window.AppWindow.Closing += (sender, args) =>
        {
            if (!HoldsClose) return;
            args.Cancel = true;
            _ = CloseWhenConfirmedAsync(window);
        };
#elif __WASM__
        var armed = false;
        engine.HistoryChanged += (_, _) =>
        {
            if (engine.IsDirty == armed) return;
            armed = engine.IsDirty;
            BrowserUnloadGuard.SetUnsavedChanges(armed);
        };
#endif
    }

    private async Task CloseWhenConfirmedAsync(Window window)
    {
        try
        {
            if (await ConfirmAsync()) window.Close();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Close failed");
            engine.ToastError($"Close failed: {ex.Message}");
        }
    }
}
