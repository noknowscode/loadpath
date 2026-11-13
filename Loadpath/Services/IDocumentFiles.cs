using Loadpath.Core.Editing;

namespace Loadpath.Services;

/// <summary>A document read from a file, with the key the editor keeps as its FilePath.</summary>
/// <param name="FromFallbackFolder">This host has no file picker, so the newest document in the fallback folder was opened; the caller says so.</param>
public sealed record OpenedDocument(DocumentSnapshot Snapshot, string Path, bool FromFallbackFolder);

/// <summary>Where a document was saved: the key the editor keeps as its FilePath.</summary>
/// <param name="ToFallbackFolder">No picker was used: this host has none, or it failed (<paramref name="PickerError"/>), so the file went to the fallback folder; the caller says where.</param>
/// <param name="PickerError">Why the picker failed, when it did.</param>
public sealed record SavedDocument(string Path, bool ToFallbackFolder, string? PickerError);

/// <summary>Where documents are opened from and saved to: the platform pickers in the app (FileService), a fake in the tests.</summary>
public interface IDocumentFiles
{
    /// <summary>
    /// Pick a document and read it. Null when cancelled. A picker that fails throws, and so does a host without a picker
    /// whose fallback folder holds no document.
    /// </summary>
    Task<OpenedDocument?> OpenAsync();

    /// <summary>Save to the document's file, or ask for one. Null when cancelled.</summary>
    Task<SavedDocument?> SaveAsync(StructureDocument doc, string? existingPath, bool forceAsk);

    /// <summary>Where share codes go on a host without a clipboard. Null when there is no such folder.</summary>
    string? FallbackFolder { get; }

    /// <summary>The newest file matching the pattern in the fallback folder, as a full path.</summary>
    string? LatestInFallbackFolder(string pattern);
}
