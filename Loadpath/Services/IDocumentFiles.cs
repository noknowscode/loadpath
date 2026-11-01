using Loadpath.Core.Editing;

namespace Loadpath.Services;

/// <summary>Where documents are opened from and saved to: the platform pickers in the app (FileService), a fake in the tests.</summary>
public interface IDocumentFiles
{
    /// <summary>Pick a document and read it. Null when cancelled or when there is nothing to open.</summary>
    Task<(DocumentSnapshot Snapshot, string Path)?> OpenAsync();

    /// <summary>
    /// Save to the document's file, or ask for one. Returns the key the editor keeps as its FilePath, or null when
    /// cancelled.
    /// </summary>
    Task<string?> SaveAsync(StructureDocument doc, string? existingPath, bool forceAsk);
}
