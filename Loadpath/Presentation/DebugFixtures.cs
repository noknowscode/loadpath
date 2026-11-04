#if DEBUG
using System.Globalization;
using Loadpath.Core.Editing;
using Loadpath.Core.Samples;
using Loadpath.Core.Serialization;

namespace Loadpath.Presentation;

/// <summary>
/// Debug-only start states, read from <c>LOADPATH_*</c> environment variables so headless captures are deterministic
/// without a pointer. A Release build compiles none of this and reads none of them (README, Validation, lists them all).
/// </summary>
public sealed class DebugFixtures(EditorEngine engine, EditorModel model)
{
    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

    /// <summary><c>LOADPATH_RESET=1</c>: start from default settings and no autosave.</summary>
    public static bool Reset => Env("LOADPATH_RESET") == "1";

    /// <summary><c>LOADPATH_THEME=light|dark|system</c>: start in a theme without touching the saved choice first.</summary>
    public static void ApplyTheme(ViewOptions options)
    {
        if (Env("LOADPATH_THEME") is { } theme && Enum.TryParse<ThemeChoice>(theme, true, out var t))
        {
            options.Theme = t;
            Console.Error.WriteLine($"VERIFY theme={t}");
        }
    }

    /// <summary>
    /// <c>LOADPATH_SAMPLE=warren|cantilever|roof</c>, <c>LOADPATH_PRESET=pratt</c> or <c>pratt:40,6,2</c> (span, panels,
    /// depth), <c>LOADPATH_FILE=path</c>: start from a fixture instead of the autosave. A fixture starts clean, as if
    /// opened from a file: no undo step, nothing unsaved. True when one was loaded.
    /// </summary>
    public bool Seed()
    {
        if (Env("LOADPATH_SAMPLE") is { } sample)
        {
            engine.LoadSnapshot(SampleStructures.Build(sample), "Load sample", null);
            return true;
        }
        if (Env("LOADPATH_PRESET") is { } preset)
        {
            var parts = preset.Split(':');
            var p = TrussPresets.Find(parts[0]).Defaults;
            if (parts.Length > 1 && parts[1].Split(',') is { Length: 3 } v)
                p = new(double.Parse(v[0], CultureInfo.InvariantCulture), int.Parse(v[1], CultureInfo.InvariantCulture), double.Parse(v[2], CultureInfo.InvariantCulture));
            engine.LoadSnapshot(TrussPresets.Build(parts[0], p), "Load preset", null);
            return true;
        }
        if (Env("LOADPATH_FILE") is { } file && File.Exists(file))
        {
            engine.LoadSnapshot(DocumentSerializer.Deserialize(File.ReadAllText(file)), "Open", null);
            return true;
        }
        return false;
    }

    /// <summary>Selection, display modes, sheet, tab, touch modifiers, tool, palette, a toast and edits, applied after the document is in.</summary>
    public void ApplyHooks()
    {
        if (Env("LOADPATH_SELECT") is { } select)
        {
            var refs = select.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s[0] is 'n' or 'N' ? ElementRef.Node(int.Parse(s[1..], CultureInfo.InvariantCulture)) : ElementRef.Member(int.Parse(s[1..], CultureInfo.InvariantCulture)));
            engine.Selection.Replace(refs);
        }
        foreach (var m in (Env("LOADPATH_MODE") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var o = engine.Options;
            switch (m.ToLowerInvariant())
            {
                case "utilization": o.DisplayMode = DisplayMode.Utilization; break;
                case "forces": o.DisplayMode = DisplayMode.Forces; break;
                case "deflection": o.ShowDeflection = true; break;
                case "reactions": o.ShowReactions = true; break;
                case "nolabels": o.ShowLabels = false; break;
                case "labels": o.ShowLabels = true; break;
                case "noise": o.ReduceNoise = true; break;
            }
        }
        if (Env("LOADPATH_SHEET") == "1") engine.Options.InspectorSheetOpen = true;
        if (Env("LOADPATH_TAB") == "outline") _ = model.SelectTabAsync(outline: true);
        if (Env("LOADPATH_MULTI") == "1") engine.Options.MultiSelect = true;
        if (Env("LOADPATH_FREE") == "1") engine.Options.FreePlacement = true;
        if (Env("LOADPATH_TOOL") is { } tool && Enum.TryParse<ToolKind>(tool, true, out var kind)) engine.Interaction.SetTool(kind);
        if (Env("LOADPATH_PALETTE") == "1") _ = model.TogglePalette(default);
        if (Env("LOADPATH_TOAST") is { } toast) engine.Toast(toast);
        var edit = Env("LOADPATH_EDIT");
        if (edit == "unsupported")
        {
            foreach (var n in engine.Document.Nodes.Where(n => n.HasSupport).ToList()) engine.History.Do(new SetSupportEdit(n.Id, SupportKind.None));
        }
        if (edit == "overload")
        {
            foreach (var n in engine.Document.Nodes.Where(n => n.HasLoad).ToList()) engine.History.Do(new SetLoadEdit(n.Id, n.Load * 6));
        }
    }
}
#endif
