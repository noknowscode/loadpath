using System.Text.Json;
using Loadpath.Core.Editing;
using Loadpath.Core.Model;

namespace Loadpath.Core.Serialization;

/// <summary>An autosaved editing session: the document, the file it belongs to (if any), and whether it had unsaved changes.</summary>
public sealed record AutosavedSession(DocumentSnapshot Snapshot, string? FilePath, bool Dirty);

/// <summary>
/// The autosave format: the document plus what the editor knew about it, so a restart brings back the session and not
/// only the drawing. A document that came from a sample, a preset or a paste belongs to no file, and one with unsaved
/// changes must come back unsaved.
/// </summary>
public static class Autosave
{
    public sealed class FileModel
    {
        /// <summary>The file the document was opened from or saved to; null when it has none.</summary>
        public string? FilePath { get; set; }
        /// <summary>True when the document had changes its file does not have.</summary>
        public bool Dirty { get; set; }
        public DocumentSerializer.FileModel? Document { get; set; }
    }

    public static string Serialize(StructureDocument doc, string? filePath, bool dirty) =>
        JsonSerializer.Serialize(new FileModel { FilePath = filePath, Dirty = dirty, Document = DocumentSerializer.ToModel(doc) },
            DocumentSerializer.Indented.AutosaveFileModel);

    /// <summary>
    /// Read an autosave. One written before the file and the dirty flag were kept is a bare document: it comes back
    /// unsaved and attached to no file, because nothing says it was saved or where.
    /// </summary>
    public static AutosavedSession Deserialize(string json)
    {
        var model = JsonSerializer.Deserialize(json, DocumentSerializer.Indented.AutosaveFileModel) ?? throw new InvalidDataException("Empty autosave.");
        return model.Document is { } document
            ? new AutosavedSession(DocumentSerializer.FromModel(document), model.FilePath, model.Dirty)
            : new AutosavedSession(DocumentSerializer.Deserialize(json), null, true);
    }
}
