using System.Runtime.InteropServices.JavaScript;

namespace Loadpath;

/// <summary>
/// Arms the browser's "leave this page?" prompt while the document has unsaved changes (WasmScripts/UnsavedChanges.js).
/// AppWindow.Closing cannot be cancelled in a browser.
/// </summary>
internal static partial class BrowserUnloadGuard
{
    [JSImport("globalThis.Loadpath.setUnsavedChanges")]
    internal static partial void SetUnsavedChanges(bool unsaved);
}
