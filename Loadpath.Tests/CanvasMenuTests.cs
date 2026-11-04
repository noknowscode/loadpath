using Loadpath.Commands;
using Loadpath.Core.Model;
using Loadpath.Core.Geometry;
using Loadpath.Presentation;
using Loadpath.Views;
using System.Windows.Input;
using Xunit;

namespace Loadpath.Tests;

/// <summary>The canvas menu lists registered commands, and binds them as commands.</summary>
public class CanvasMenuTests
{
    [Theory]
    [InlineData(HitKind.Node)]
    [InlineData(HitKind.LoadHandle)]
    [InlineData(HitKind.Member)]
    [InlineData(HitKind.None)]
    public void Every_entry_is_a_registered_command_or_a_built_in_item(HitKind hit)
    {
        var engine = new EditorEngine();
        var builtIn = new[] { CanvasMenu.Entry.SeparatorId, CanvasMenu.Entry.SectionsId, CanvasMenu.Entry.AddNodeId };

        foreach (var entry in CanvasMenu.EntriesFor(hit))
        {
            Assert.True(builtIn.Contains(entry.Id) || engine.Commands.Find(entry.Id) is not null, $"{entry.Id} is not registered");
        }
    }

    [Fact]
    public void A_member_menu_offers_every_library_section_as_a_command()
    {
        var engine = new EditorEngine();

        Assert.Contains(CanvasMenu.EntriesFor(HitKind.Member), e => e.Id == CanvasMenu.Entry.SectionsId);
        Assert.All(Section.Library, s => Assert.NotNull(engine.Commands.Find($"section.{s.Id}")));
    }

    [Fact]
    public void A_command_bound_to_a_menu_item_runs_only_when_it_can()
    {
        var runs = 0;
        var available = false;
        ICommand command = new AppCommand("test", "Test", "Edit", null, () => runs++, () => available);

        Assert.False(command.CanExecute(null));
        command.Execute(null);
        available = true;
        command.Execute(null);

        Assert.Equal(1, runs);
        Assert.True(command.CanExecute(null));
    }
}
