namespace Loadpath.Presentation;

/// <summary>The answer to "this document has unsaved changes".</summary>
public enum UnsavedChoice { Save, Discard, Cancel }

/// <summary>Asks about unsaved changes in the named document: a dialog in the app (Views/UnsavedChangesDialog), a scripted answer in the tests.</summary>
public interface IUnsavedPrompt
{
    Task<UnsavedChoice> AskAsync(string documentName);
}
