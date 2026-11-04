# uno-audit: Loadpath (2026-11-04)

Audited head `1c788fc` on `feat/production-readiness` (Uno.Sdk 7.0.0-dev.115, five heads). This pass is hygiene,
pattern conformance, the semantic lint gate, performance, the build gate and a runtime re-check. Functional gaps from
`docs/COMPLETENESS.md` stay for the next pass unless a fix here touched them.

## Skills applied

| Skill | Pattern it governs here | Where it shows |
|---|---|---|
| uno-platform-agent | Single project, UnoFeatures, `Uno0001` as an error, platform symbols | `Loadpath/Loadpath.csproj`, `Directory.Build.props` (checked, unchanged) |
| uno-scaffolding (+ `uno-runtime-gotchas.md`) | Code-behind budget: events go to model methods and commands; semantic-first ladder; workaround protocol | `Presentation/EditorEngine.cs:193` `AddNodeAt`, `:201` `DropSection`; palette rows bound (`Views/CommandPalette.xaml:34`) |
| mvux | `[Command]` generation, `{Binding}` only, command inside a template through `ElementName` | `Presentation/EditorModel.cs:437` `RunPalette`; no `x:Bind` anywhere |
| uno-toolkit | `{utu:Responsive}` for every breakpoint; Toolkit controls need a design theme | `MainPage.xaml:28` `LayoutProbe`; CARD reasons (below) |
| winui-xaml | Virtualized lists, no blocking on the UI thread | `Views/OutlinePanel.xaml:75,92` `ItemsRepeater` in a `ScrollViewer` |
| dotnet-csharp | Narrow catch filters, no swallowed I/O failures, guarded `async void` | `Presentation/DocumentSession.cs:213`, `Services/SettingsService.cs:53,80`, `MainPage.xaml.cs:750` |
| skiasharp-uno | Paints, fonts and path effects allocated once; no per-frame render loop | `Workspace/WorkspaceRenderer.cs:21-41` (checked, unchanged) |
| uno-extensions-services | Hosting and DI shape for the approved next pass | Remaining, item 1 |

Not applicable: uno-navigation (single page, no routes by SPEC), uno-material (Fluent plus the app's own tokens by README
decision).

## Semantic lint gate

`xaml-semantic-lint.ps1 -Path Loadpath`. "Allowed" is the count with every `xaml-lint: allow` comment disabled.

| Rule | Gating before | Gating after | Allowed before | Allowed after |
|---|---|---|---|---|
| CARD | 0 | 0 | 8 | 8 |
| HEX | 0 | 0 | 0 | 0 |
| TOKENTHEME | 0 | 0 | 0 | 0 |
| BACKBAR | 0 | 0 | 0 | 0 |
| CODEBEHIND | 0 | 0 | 30 | 29 |
| OVERLAY | 0 | 0 | 0 | 0 |
| RESPONSIVE | 0 | 0 | 1 | 1 |
| BUILTIN | 0 | 0 | 0 | 0 |
| ICON | 0 | 0 | 0 | 0 |
| Allow comments in the code | | | 52 | 38 |
| TOKEN (info) | 172 | 172 | | |
| TEMPLATE (info) | 11 | 11 | | |

Re-check of the allow comments:
- 13 suppressed nothing (`8b8292c`): nine CARD comments above Borders styled through `SegmentGroup`, `PillBorder`,
  `DangerPill` and `StatusPill` (the lint does not see style setters), two typed-sender handlers (`OnWindowClosing`,
  `OutlinePanel.OnRowPrepared`) and two size reads that are not comparisons (`TitleStrip.FitDocumentName`, the floating
  bar clamp). Twelve were removed; `OnRowPrepared`'s reason was worth keeping and is now a plain comment. One more left
  with its handler (`CommandPalette.OnItemClick`, `cba5915`).
- CARD, 8 kept. None is a card, and `CardContentControl` has no style here: the Toolkit's controls need a design theme
  (Material, Cupertino or Simple) and the app runs Fluent plus its own tokens without merging `ToolkitResources`
  (README, Architecture). Reasons now say what each element is and name the Fluent control where one exists:
  `MainPage.xaml:59,71` status banners (InfoBar), `:118` the selection toolbar that follows the viewport
  (CommandBarFlyout cannot track a moving anchor), `:150` drag ghost, `:158` toast (Fluent has none),
  `Views/CommandPalette.xaml:7` popover chrome, `Views/TitleStrip.xaml:64` toggle plus flyout chevron (ToggleSplitButton),
  `Views/ToolRail.xaml:11` tool palette.
- CODEBEHIND, 29 kept, each with its reason: window and keyboard routing (`MainPage.xaml.cs:129,409,428,468`), the canvas
  menu built from a hit test (`:473`), the floating bar and drag ghost that follow the viewport and the pointer
  (`:624,640,647,653,658,675,683`), the toast and autosave timers (`:705,745`), the palette's key routing, hover and scrim
  (`Views/CommandPalette.xaml.cs:47,67,81,95`), the section drag start (`Views/InspectorPanel.xaml.cs:19`), Shift-aware
  outline selection and hover (`Views/OutlinePanel.xaml.cs:30,40,46`), and the title strip's flyout plumbing
  (`Views/TitleStrip.xaml.cs:31,39,46,57,60,63`). `TitleStrip.xaml.cs:39` is the preset-tile fix from `997f7be`: the
  Hide is posted to the dispatcher so the tile's command runs before the sheet unloads; it stays, with its reason.
- RESPONSIVE, 1 kept: `MainPage.xaml.cs:606` asks whether the floating bar has been measured, not a breakpoint.
- TEMPLATE (info): 10 templates in `Themes/Controls.xaml` and one in `Themes/Icons.xaml`. Most exist for the spline-eased
  state transitions and custom parts; `ToolTip` (`Controls.xaml:950`) and `MenuFlyoutSeparator` (`:933`) only set colour,
  radius and padding (Remaining, item 5).

## Findings and fixes

### Code hygiene
- Unused usings: 15 directives repeated Uno.Sdk's implicit global usings or `GlobalUsings.cs` (found with IDE0005 on the
  app's net10.0 build) - removed, `e19ef48`.
- Dead code in Core: `MoveNodesEdit.Delta`, `Material.FindByName`, `SnapResult.HasGuides`, `Bounds.Inflate`,
  `TouchTracker.IsDragging`, `EditHistory.IsInTransaction`, `Member.OtherEnd`, `StructureDocument.PeekNextNodeId` and
  `PeekNextMemberId`, `Vec2.Perp`, `Viewport.ToScreenLength` and `VisibleWorldBounds`, and `HitTester.LoadArrowPxPerKn`
  (its comment no longer matched `LoadArrowLengthPx`) - removed, `e19ef48`. Kept: `TouchTracker.ContactCount` and
  `HitResult.IsNone`, which the tests use to observe state.
- Duplicates: `IsDown` in `MainPage.xaml.cs` and `Views/OutlinePanel.xaml.cs` plus an inline copy in
  `Controls/NumberField.cs` -> `Controls/KeyboardState.cs`; `DocumentSession.SafeName` and `FileService.Sanitize` ->
  `DocumentSerializer.SafeFileName` in Core, with a test - `dcd4380`.
- Narration comments and commented-out code: none found. README's Controls list named a `UtilizationBar` that is now a
  ProgressBar style - corrected.

### Pattern conformance (code-behind budget)
- The canvas menu's "Add node here" and the section drop built `AddNodeEdit` and `SetSectionEdit` in the page. They are
  `EditorEngine.AddNodeAt` and `EditorEngine.DropSection` now, tested; dropping the section a member already has no
  longer pushes an empty undo step - `162308a`.
- Palette rows ran their entry from a `Click` handler. `RunPalette` is a generated command bound by the row, as the
  failure rows and section chips already were - `cba5915`.
- `{Binding}` on every MVUX surface, no `x:Bind`, no hex outside `Themes/Tokens.xaml`, no literal font sizes in views
  (sizes come from `Text*` tokens), every breakpoint a `{utu:Responsive}` against the one `DefaultResponsiveLayout`.
- Gotchas checked: SKCanvasElement ignores `Opacity` (paints carry alpha), Uno 7 `SKCanvasElement` needs a Background to
  hit-test (set), MVUX commands run off the UI thread (`EditorModel.Ui`), `Uno0001` is an error, a Button raises Click
  before its command (the preset fix).

### Failure handling
- The autosave and the settings write were silent catches (`MainPage.xaml.cs` `OnAutosaveTick`,
  `SettingsService.Save`). The autosave now toasts "Autosave failed: ... Save to keep your work." when the failures start
  and retries on the next edit; a settings write that fails toasts once per failing stretch; loading unreadable settings
  falls back to the defaults through a narrowed catch - `0adfab0`, tested in `DocumentSessionTests`.

### Performance
- Renderer: paints, fonts, dash effects and the support triangle are allocated once; one `SKPathBuilder` is reused; the
  label-box list is reused. Per frame: label strings and detached ribbon paths only. No `CompositionTarget.Rendering`;
  the viewport animation timer runs only while the view moves.
- Lists: the outline is an `ItemsRepeater` in a `ScrollViewer`; the palette is capped at 40 entries; the failure and
  connected-member lists are bounded by the structure.
- Startup: the canvas faces load once through a cached task off the first frame (`Workspace/CanvasTypefaces.cs:16`).
- Measured (Debug desktop, Xvfb): idle CPU 0 % of one core over 10 s with the Howe roof open; the Warren solve reads
  0.1 ms in the status bar.

### Stack currency (report only)
- .NET 10 TFMs on every head; SDK 10.0.113 here. Uno.Sdk 7.0.0-dev.115 by owner decision (newest dev 7.0.0-dev.132,
  newest stable 6.7.30). Implicit packages follow the SDK: Uno.WinUI 7.0.0-dev.2509, Uno.Toolkit.WinUI 11.0.0-dev.157,
  Uno.Extensions.Reactive.WinUI 8.0.0-dev.150, SkiaSharp 4.151.1 (stable 4.154.0 exists; the SDK pins it).
- `Directory.Packages.props`: Microsoft.NET.Test.Sdk 18.10.1 -> 18.11.0 stable; xunit.runner.visualstudio 4.0.0 ->
  4.0.1; xunit 2.9.3 is the last v2 (the v3 line is `xunit.v3` 4.0.2, a migration).
- The WinAppSDK head targets `net10.0-windows10.0.19041`; the current template uses `10.0.26100`.

## Build and tests

- Release `net10.0-desktop`, `net10.0-browserwasm` and `net10.0-android` (local SDK through `AndroidSdkDirectory`): 0
  warnings, 0 errors each, with warnings as errors. The WinAppSDK and iOS heads cannot build on Linux; CI builds them.
- Tests: 183 before, 190 after (`SafeFileName` x4, `AddNodeAt`, `DropSection`, autosave failure), all passing.

## Runtime verification (Debug desktop, Xvfb :454, `APP_NO_HOTDESIGN=1`, `LIBGL_ALWAYS_SOFTWARE=1`, `xdotool`)

- Palette row click (Warren, light, 1440 px): Ctrl+K, "show util", click the row -> trace `palette run
  id=view.utilization`, the canvas switched to utilization. Enter in the query still runs the highlighted entry.
- Narrow (390 px) and dark: Ctrl+K, "reac", click "Toggle reactions" -> trace `palette run id=view.reactions`, the
  reaction arrows went away.
- Canvas menu: right-click on empty sheet -> "Add node here" -> N14 at (4.5, -5.5), selected, detached-parts banner, one
  Ctrl+Z removed it.
- Section drop: CHS 114.3x5.0 chip dragged from M1's inspector onto M2 -> M2 took the section, became the selection,
  toast "CHS 114.3x5.0 applied to member 2".
- Shift-click on an outline row added N1 to N14 ("2 selected"); Ctrl+Z routed through the shared `KeyboardState`.
- `autosave.loadpath` and `settings.json` replaced by folders: at launch the toasts read "Settings not saved: Access to
  the path ... is denied" and then "Autosave failed: Access to the path ... is denied. Save to keep your work."; later
  edits and option changes raised no further toasts.

## Remaining

1. **Dependency injection and the MainPage split (owner-approved for the next pass), about 2 days, ~800 lines touched.**
   **Done in the A pass** (`cd5c094`, follow-ups `bacd11f`, `22d21be`): as planned below, with two differences. The
   generated view model needs the UI dispatcher, so the tests resolve `EditorModel` directly; and the Extensions theme
   service is registered per scope (it needs the window and an `IDispatcher`), so `App` opens one scope for the window and
   `ThemeSwitcher` is scoped. `MainPage.xaml.cs` is 186 lines; the overlay geometry is `Presentation/CanvasLayout` (pure,
   tested) applied by `Views/CanvasOverlays`. Tests 190 -> 261.
   - Hosting: add `Hosting;Logging;ThemeService` to `UnoFeatures`; `App.OnLaunched` builds
     `this.CreateBuilder(args).Configure(h => h.UseLogging().UseThemeSwitching().ConfigureServices(...))` and keeps
     `Host`. Register `EditorEngine`, `IDocumentFiles -> FileService` (window from the builder), `ISettingsStore ->
     SettingsService`, `IUnsavedPrompt` (the ContentDialog, replacing the `Func` the session takes today),
     `DocumentSession` and `EditorModel`, all singletons. `MainPage` resolves `EditorViewModel` on the UI thread
     (`EditorModel` captures the dispatcher in its constructor, so no navigation-created view model).
   - Out of `MainPage.xaml.cs` (764 lines, about 550 move): the debug fixtures (`SeedFixture`, `ApplyThemeFixture`,
     `ApplyFixtureHooks`, 90 lines) to `Presentation/DebugFixtures.cs` under `#if DEBUG`; window sizing and the close
     guard (90) to `App`/`Platforms/Desktop`; the theme root (20) to `IThemeService` after `Loaded` (gotcha: SetThemeAsync
     during model construction never completes); autosave debounce (25) into `DocumentSession`; settings capture and
     persist (20) into a settings service subscribed to `ViewOptions.Changed`; the toast (20) to a model `Toast` state with
     a small `ToastHost` view; the floating bar placement (60) to a pure, tested `FloatingBarPlacement` with the page only
     applying it; the sheet coverage math (45) next to it; the canvas menu (65) to `Views/CanvasMenu.cs` built from
     `AppCommand` (make `AppCommand` an `ICommand`); the section drag (50) to `Views/SectionDragController.cs`. What stays:
     construction, keyboard routing and layout-probe wiring, about 200 lines.
   - Logging replaces `EditorModel.Trace` file appends; add `UnhandledException` and `UnobservedTaskException` handlers
     that log and toast (COMPLETENESS Failure handling minor).
2. Status banners -> `InfoBar` with lightweight keys (`MainPage.xaml:59,71`): a visual change (glyph icon, chrome),
   0.5 day with both themes and four widths re-captured.
3. Reduce-noise frame -> `ToggleSplitButton` (`Views/TitleStrip.xaml:64`): 0.5 day, same re-capture.
4. Command palette as a light-dismiss `Popup` bound to `IsPaletteOpen` would drop the scrim handlers
   (`Views/CommandPalette.xaml.cs:81,95`); check focus return to the key sink. 0.5 day.
5. `ToolTip` and `MenuFlyoutSeparator` templates (`Themes/Controls.xaml:933,950`) restyle colour, radius and padding only:
   `BasedOn` the Fluent style plus lightweight keys, after confirming the keys on Uno 7. 2 hours.
6. Functional items from `docs/COMPLETENESS.md` stay there (the exaggeration control, coincident nodes, refit on resize,
   iOS/WinAppSDK runtime proof). SafeArea on phones is done (`e4a02e0`).
7. Package bumps in `Directory.Packages.props` (Test.Sdk 18.11.0, runner 4.0.1): one line each, owner call.

## Proposed decisions

- Keep the CARD exceptions as Fluent-era decisions unless the owner adopts a Toolkit design theme; if InfoBar and
  ToggleSplitButton land (items 2 and 3), CARD allows drop from 8 to 5.
- Record in README that `ToolkitResources` stays unmerged, so no Toolkit control with a template (Card, Chip, TabBar,
  NavigationBar) is used; only the Responsive helpers and `SafeArea` (a helper without a theme) are.
