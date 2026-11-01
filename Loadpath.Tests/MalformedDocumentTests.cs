using System.IO.Compression;
using System.Text;
using Loadpath.Core.Model;
using Loadpath.Core.Serialization;
using Xunit;

namespace Loadpath.Tests;

/// <summary>A broken file, share code or autosave is refused whole, before it can touch the open document.</summary>
public class MalformedDocumentTests
{
    private static string Json(string nodes, string members) =>
        $$"""{ "version": 1, "name": "Broken", "nodes": [ {{nodes}} ], "members": [ {{members}} ] }""";

    private const string TwoNodes = """{ "id": 1, "x": 0, "y": 0, "support": "Pin" }, { "id": 2, "x": 4, "y": 0, "support": "RollerY", "fy": -10 }""";

    /// <summary>A share code for any text, built the way ShareCode.Encode builds one.</summary>
    private static string Code(string json)
    {
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(Encoding.UTF8.GetBytes(json));
        return ShareCode.Prefix + Convert.ToBase64String(buffer.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static TheoryData<string, string> Broken() => new()
    {
        { Json(TwoNodes + """, { "id": 2, "x": 8, "y": 0 }""", ""), "Node 2 appears twice." },
        { Json(TwoNodes, """{ "id": 1, "start": 1, "end": 2 }, { "id": 1, "start": 2, "end": 1 }"""), "Member 1 appears twice." },
        { Json(TwoNodes, """{ "id": 1, "start": 1, "end": 3 }"""), "Member 1 references a missing node." },
    };

    [Theory]
    [MemberData(nameof(Broken))]
    public void A_malformed_document_is_refused_with_a_reason(string json, string reason)
    {
        var error = Assert.Throws<InvalidDataException>(() => DocumentSerializer.Deserialize(json));
        Assert.Equal(reason, error.Message);
    }

    [Theory]
    [MemberData(nameof(Broken))]
    public void A_malformed_share_code_or_pasted_json_names_what_is_wrong(string json, string reason)
    {
        Assert.False(ShareCode.TryDecode(Code(json), out var fromCode, out var codeError));
        Assert.Null(fromCode);
        Assert.Equal(reason, codeError);

        Assert.False(ShareCode.TryDecode(json, out var fromJson, out var jsonError));
        Assert.Null(fromJson);
        Assert.Equal(reason, jsonError);
    }

    [Theory]
    [MemberData(nameof(Broken))]
    public void A_malformed_autosave_is_refused_too(string json, string reason)
    {
        var autosave = $$"""{ "filePath": null, "dirty": true, "document": {{json}} }""";
        Assert.Equal(reason, Assert.Throws<InvalidDataException>(() => Autosave.Deserialize(autosave)).Message);
    }

    [Theory]
    [InlineData("""{ "id": 1, "x": NaN, "y": 0 }""")]
    [InlineData("""{ "id": 1, "x": 1e999, "y": 0 }""")]
    [InlineData("""{ "id": 1, "x": 0, "y": 0, "fx": "Infinity" }""")]
    public void A_position_or_load_that_is_not_a_finite_number_is_refused(string node)
    {
        Assert.False(ShareCode.TryDecode(Json(node, ""), out var snapshot, out var error));
        Assert.Null(snapshot);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void A_support_written_as_an_unknown_number_reads_as_no_support()
    {
        var snapshot = DocumentSerializer.Deserialize(Json("""{ "id": 1, "x": 0, "y": 0, "support": "7" }""", ""));
        Assert.Equal(SupportKind.None, snapshot.Nodes[0].Support);
    }
}
