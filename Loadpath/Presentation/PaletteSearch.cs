using System.Collections.Immutable;
using Loadpath.Commands;
using Microsoft.Extensions.Logging;

namespace Loadpath.Presentation;

/// <summary>
/// The Ctrl+K command palette as MVUX states: open or closed, the query, and the matching commands with the keyboard
/// highlight. Matches are scored by title, then category, then letters in order; the list is capped at 40 entries.
/// </summary>
public sealed class PaletteSearch(EditorEngine engine, IUiThread ui, ILogger<PaletteSearch> log)
{
    private int _highlight;
    private List<AppCommand> _matches = new();

    public IState<bool> IsPaletteOpen => State.Value(this, () => false);
    public IState<string> PaletteQuery => State.Value(this, () => "").ForEach(async (q, ct) => await RebuildAsync(q ?? "", 0, ct));
    public IListState<PaletteItem> PaletteItems => ListState<PaletteItem>.Empty(this);

    public async ValueTask ToggleAsync(CancellationToken ct)
    {
        var open = !await IsPaletteOpen;
        if (open)
        {
            await PaletteQuery.SetAsync("", ct);
            await RebuildAsync("", 0, ct);
        }
        await IsPaletteOpen.SetAsync(open, ct);
    }

    public async ValueTask CloseAsync(CancellationToken ct) => await IsPaletteOpen.SetAsync(false, ct);

    public async ValueTask MoveHighlightAsync(int delta, CancellationToken ct)
    {
        if (_matches.Count == 0) return;
        _highlight = (_highlight + delta + _matches.Count) % _matches.Count;
        await PublishAsync(ct);
    }

    public async ValueTask HighlightAsync(string id, CancellationToken ct)
    {
        var i = _matches.FindIndex(c => c.Id == id);
        if (i < 0 || i == _highlight) return;
        _highlight = i;
        await PublishAsync(ct);
    }

    /// <summary>Run an entry: a row's command (its id) or Enter in the query (the highlighted entry). The palette closes.</summary>
    public async ValueTask RunAsync(string? id, CancellationToken ct)
    {
        log.LogDebug("palette run id={Id} highlight={Highlight} matches={Matches}", id, _highlight, _matches.Count);
        var command = id is null
            ? (_highlight >= 0 && _highlight < _matches.Count ? _matches[_highlight] : null)
            : _matches.FirstOrDefault(c => c.Id == id);
        await IsPaletteOpen.SetAsync(false, ct);
        if (command is null) return;
        ui.Run(() => { if (!command.TryExecute()) engine.Toast($"{command.Title} is not available right now"); });
    }

    private async ValueTask RebuildAsync(string query, int highlight, CancellationToken ct)
    {
        var q = query.Trim();
        _matches = engine.Commands.All
            .Select(c => (c, score: Score(c.Title, c.Category, q)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score).ThenBy(x => x.c.Category).ThenBy(x => x.c.Title)
            .Take(40)
            .Select(x => x.c)
            .ToList();
        _highlight = highlight;
        await PublishAsync(ct);
    }

    private async ValueTask PublishAsync(CancellationToken ct)
    {
        var items = _matches.Select((c, i) => new PaletteItem(
            c.Id, c.Title, c.Category, c.ShortcutDisplay.Replace("|", " · "), c.Icon ?? "chevron-right", c.Icon is not null,
            !c.CanExecute, i == _highlight ? "OnSurfaceHoverBrush" : "TransparentBrush")).ToImmutableList();
        await PaletteItems.UpdateAsync(_ => items, ct);
    }

    private static int Score(string title, string category, string q)
    {
        if (q.Length == 0) return 1;
        var t = title.ToLowerInvariant();
        var ql = q.ToLowerInvariant();
        if (t.StartsWith(ql)) return 100;
        if (t.Contains(ql)) return 60;
        if (category.ToLowerInvariant().Contains(ql)) return 30;
        var i = 0;
        foreach (var ch in t) { if (i < ql.Length && ch == ql[i]) i++; }
        return i == ql.Length ? 10 : 0;
    }
}
