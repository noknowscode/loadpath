using Loadpath.Presentation;
using Uno.Extensions.Reactive.Core;
using Xunit;

namespace Loadpath.Tests;

/// <summary>
/// The generated EditorViewModel binds a state two-way only when the state belongs to the model's SourceContext: it asks
/// the context for the state, and a state of another context comes back wrapped and read-only, so what the user types
/// never reaches it. The states the model's pieces declare must therefore be the model's.
/// </summary>
public class BindableStatesTests
{
    [Fact]
    public void Every_state_the_view_can_write_belongs_to_the_model()
    {
        var model = Models.Editor(new EditorEngine(), new TestUiThread());
        var context = SourceContext.GetOrCreate(model);
        var stateFor = typeof(SourceContext).GetMethods()
            .Single(m => m.Name == nameof(SourceContext.GetOrCreateState) && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
        var states = typeof(EditorModel).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(IState<>))
            .ToList();

        Assert.Contains(states, p => p.Name == nameof(EditorModel.PaletteQuery));
        Assert.Contains(states, p => p.Name == nameof(EditorModel.NodePosition));
        Assert.Contains(states, p => p.Name == nameof(EditorModel.KeepGrid));
        Assert.Contains(states, p => p.Name == nameof(EditorModel.PresetSpan));
        Assert.All(states, p =>
        {
            var state = p.GetValue(model);
            var bound = stateFor.MakeGenericMethod(p.PropertyType.GetGenericArguments()[0]).Invoke(context, new[] { state });
            Assert.True(ReferenceEquals(state, bound), $"{p.Name} belongs to another context: the view would bind it read-only");
        });
    }
}
