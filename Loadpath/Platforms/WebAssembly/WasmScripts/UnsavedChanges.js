// The browser's own "leave this page?" prompt, armed while the document has unsaved changes. A page cannot keep its
// tab open through AppWindow.Closing, and no dialog of the app's own can run while the page unloads.
// MainPage keeps the flag current through BrowserUnloadGuard.SetUnsavedChanges (JSImport).
globalThis.Loadpath = globalThis.Loadpath || {};
globalThis.Loadpath.unsavedChanges = false;
globalThis.Loadpath.setUnsavedChanges = function (unsaved) {
    globalThis.Loadpath.unsavedChanges = unsaved;
};
window.addEventListener("beforeunload", function (e) {
    if (!globalThis.Loadpath.unsavedChanges) return;
    e.preventDefault();
    // Browsers that predate preventDefault() here show the prompt only when returnValue is set.
    e.returnValue = "";
});
