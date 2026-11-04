using Loadpath.Core.Editing;
using Loadpath.Core.Geometry;
using Loadpath.Core.Model;
using Loadpath.Core.Samples;
using Loadpath.Presentation;
using Xunit;

namespace Loadpath.Tests;

/// <summary>
/// An inspector edit lands on the element it was typed for. The fields bind to NodeVector and SectionPick records that
/// carry the element's id, and EditorModel commits them through these engine calls with that id, never the selection,
/// because MVUX runs the commit after the edit, when the user may already have clicked something else.
/// </summary>
public class InspectorFieldTests
{
    private static EditorEngine Warren()
    {
        var engine = new EditorEngine();
        engine.LoadSnapshot(SampleStructures.Build("warren"), "Open", null);
        return engine;
    }

    [Fact]
    public void A_position_typed_for_one_node_lands_on_it_after_another_node_was_clicked()
    {
        var engine = Warren();
        engine.Selection.Set(ElementRef.Node(2));
        var typed = new NodeVector(2, 2.5, 0);

        engine.Selection.Set(ElementRef.Node(3));
        engine.SetNodePosition(typed.NodeId, new Vec2(typed.X, typed.Y));

        Assert.Equal(new Vec2(2.5, 0), engine.Document.GetNode(2).Position);
        Assert.Equal(new Vec2(4, 0), engine.Document.GetNode(3).Position);
    }

    [Fact]
    public void A_load_typed_for_one_node_lands_on_it_after_another_node_was_clicked()
    {
        var engine = Warren();
        engine.Selection.Set(ElementRef.Node(2));
        var typed = new NodeVector(2, 5, -15);

        engine.Selection.Set(ElementRef.Node(3));
        engine.SetNodeLoad(typed.NodeId, new Vec2(typed.X, typed.Y));

        Assert.Equal(new Vec2(5, -15), engine.Document.GetNode(2).Load);
        Assert.Equal(new Vec2(0, -15), engine.Document.GetNode(3).Load);
    }

    [Fact]
    public void A_section_picked_for_one_member_lands_on_it_after_another_member_was_clicked()
    {
        var engine = Warren();
        engine.Selection.Set(ElementRef.Member(1));
        var picked = new SectionPick(1, Section.Library.ToList().IndexOf(Section.Chs114));

        engine.Selection.Set(ElementRef.Member(2));
        engine.SetMemberSection(picked.MemberId, Section.Library[picked.Index]);

        Assert.Equal(Section.Chs114, engine.Document.GetMember(1).Section);
        Assert.Equal(Section.Chs76, engine.Document.GetMember(2).Section);
    }

    [Fact]
    public void A_value_the_inspector_pushed_itself_commits_no_edit()
    {
        var engine = Warren();
        var node = engine.Document.GetNode(2);

        engine.SetNodePosition(node.Id, node.Position);
        engine.SetNodeLoad(node.Id, node.Load);
        engine.SetMemberSection(1, engine.Document.GetMember(1).Section);

        Assert.False(engine.History.CanUndo);
    }

    [Fact]
    public void A_value_for_an_element_deleted_since_is_dropped()
    {
        var engine = Warren();
        engine.History.Do(new RemoveElementsEdit([ElementRef.Node(2)]));

        engine.SetNodePosition(2, new Vec2(9, 9));

        Assert.Null(engine.Document.FindNode(2));
        Assert.Equal("Delete node", engine.History.UndoLabel);
    }
}
