# Completeness audit: Loadpath (2026-11-04, Uno.Sdk 7.0.0-dev.115, heads: desktop, wasm, android, ios, windows)

Audited commit: `39ce1a0` on `feat/production-readiness`. Report mode: nothing was fixed.
Fix pass (2026-11-04): the one Blocker (preset tiles) fixed at `997f7be` and re-proved on desktop and WebAssembly; see
Findings / Fixed. Everything else below is as audited.
Runtime proof: Debug `net10.0-desktop` under Xvfb (1600 x 1000 screen, 1440 x 960 window), driven with `xdotool` and the
Debug fixture hooks, post-state read from the autosave JSON, the saved `.loadpath` file, `settings.json`, `LOADPATH_TRACE`
and the `VERIFY` stderr lines. WebAssembly: Release publish served locally and driven in headless Chromium (Playwright, 1440 x 900). Android, iOS and
WinAppSDK were not run here; CI built all five heads at this commit (run 41351276840, all jobs green).
Re-checked from the 2026-11-01 review: the autosave now restores its file and its unsaved state (R), a sample is an undoable
replacement (R), WebAssembly raises the leave-page prompt (R); Esc cancelling a section drag is read in code only (C).

Verdict: **Complete with gaps** (was Incomplete at audit; the preset-tile Blocker is fixed)
Safe to show: draw and solve, drag with live re-solve, undo/redo, save/open/autosave restore, section change, unsaved-changes
prompt, share code round trip, failures with J / U / Shift+U, palette, phone layout, PRESETS flyout with span / panels /
depth (commit a typed value with Enter before picking a tile). / Keep off screen: the deflection exaggeration slider (there is none).

## Inventory

Screens: 20 intended, 20 present, 0 unreachable, 0 missing (source: SPEC.md Stage 2 surfaces table, Stage 5, README; derived for the sheets and overflow menu)
- Shell (title strip, tool rail, workspace, status bar); inspectors: summary, node, member, mixed; structure outline;
  floating selection bar; context menus (node, member, canvas); command palette; toast; empty-state invitation;
  mechanism banner; detached-parts banner; unsaved-changes dialog; presets flyout; reduce-noise layers flyout;
  overflow menu (below Wide); inspector side sheet / bottom sheet with Inspector/Structure tabs; section drag ghost.
Routes: single page by design (SPEC "Navigation model: None"); 0 registered routes, 0 dead references. Navigation is in the
structure: zoom, pan, fit, select from outline, jump to critical (J).
Interactions: 90 interactive XAML elements + 80 `AppCommand`s (every one has a palette entry); 0 dead by construction
(every Button/Toggle/ComboBox/NumberField resolves to a command, handler or flyout); 0 broken at runtime (the preset tile
template, 6 tiles, was broken at audit and is fixed at `997f7be`); 0 simulated.
Critical workflows (derived from SPEC Interaction Brief flows 1-3, Stage 5 and the README; confirm):
1. Draw and solve: place nodes, connect, support, load, read forces, drag a node, undo/redo
2. Save, open, autosave restore after a kill
3. Change a member's section and read utilization
4. Unsaved-changes guard on New and on window close
5. Load a parametric preset from the PRESETS flyout
6. Share code round trip (copy, New, paste)
7. Failures: pills and list, step with J, Upsize (U), Lighten (Shift+U)

## Completeness by area

| # | Area | Status | Evidence | Blockers | Majors | Minors | Note |
|---|---|---|---|---|---|---|---|
| 1 | Screens | Complete | R | 0 | 0 | 0 | all 20 surfaces opened (detached-parts banner and "No loads" chip on WebAssembly) |
| 2 | Navigation | Complete | R | 0 | 0 | 0 | single page by design; fit, outline select, J proven |
| 3 | Interactions | Partial | R | 0 | 0 | 2 | preset tiles load with the sheet's parameters (fixed); a tile click right after typing a value without Enter only commits it; exaggeration has no control |
| 4 | Logic | Partial | R | 0 | 0 | 1 | forces match method of joints; upsize/lighten as stated; tools can stack a node on an existing one |
| 5 | Data | Partial | R | 0 | 0 | 1 | desktop file + autosave and WebAssembly browser-storage restore proven; undoing a sample/preset/paste detaches the file |
| 6 | States | Complete | R | 0 | 0 | 0 | empty, mechanism, failures, refused paste, malformed file; loading N/A (synchronous) |
| 7 | Forms | Complete | R | 0 | 0 | 0 | number fields commit on Enter; preset fields rebuild thumbnails |
| 8 | Authentication | N/A | C | 0 | 0 | 0 | single-user local tool, no accounts (confirm) |
| 9 | Responsive/adaptive UI | Partial | R | 0 | 0 | 2 | rail clips at 390 px height; view not refit after width change |
| 10 | Platform behavior | Partial | C | 0 | 1 | 1 | no safe-area handling for phones; picker path X on Linux |
| 11 | Failure handling | Partial | R | 0 | 0 | 2 | autosave/settings failures silent; no global exception log |
| 12 | Accessibility | Partial | C | 0 | 0 | 1 | all controls named; canvas has no per-element peers (outline is the alternative); tab order and contrast X |
| 13 | Performance | Complete | R | 0 | 0 | 0 | idle 0.1 % CPU; solve 0.1-0.4 ms; Debug first layout 2.5 s |
| 14 | Security/privacy | Complete | C | 0 | 0 | 0 | no secrets, no permissions, every LOADPATH_ hook `#if DEBUG` |
| 15 | Visual quality | Partial | R | 0 | 0 | 3 | fallback face on cold start; mode toggle desync under a fixture; stale node inspector once on WebAssembly |
| 16 | Testing | Partial | R | 0 | 4 | 1 | 183/183 in CI; android, ios, windows never run; wasm 4 of 7 workflows; no UI automation |

## Critical workflows by head

| Workflow | desktop | wasm | android | ios | windows |
|---|---|---|---|---|---|
| 1. Draw and solve | R (autosave: 3 nodes, 3 members, Pin + RollerY, Fy -10; M1 +5.0, M2/M3 -7.1 = method of joints; drag N3 y -1 → 1 re-solves to +2.5/-5.6; one Ctrl+Z / Ctrl+Y) | R (Release in Chromium: same clicks gave N1 Pin, N2 Roller, N3 −10 kN; M1 +5.0, M2/M3 −7.1) | X (no device) | X (no Mac) | X (no Windows) |
| 2. Save, open, autosave restore | R (Ctrl+S wrote `~/Documents/Loadpath/Untitled.loadpath`; Ctrl+O read it back; kill -9 + relaunch restored N3 x 2.5 as UNSAVED with the file attached; Ctrl+S wrote x 2.5 to that file) | Partial (browser relaunch restored the triangle as UNSAVED from browser storage; Save/Open download pickers not driven) | X | X | X |
| 3. Section change → utilization | R (chip 76.1x4.0: M2 21 % → 9 %; ComboBox CHS 114.3x5.0: autosave `chs114`) | X (not driven) | X | X | X |
| 4. Unsaved-changes guard | R (Ctrl+N dialog: Cancel kept 3 nodes, Discard gave 0; WM_DELETE_WINDOW → dialog → Discard exited and deleted the autosave) | R (Ctrl+N raised the in-app dialog, Discard cleared it; closing the tab with edits raised `beforeunload`) | X | X | X |
| 5. Parametric preset (flyout) | R after fix `997f7be` (from Warren 13 N / 23 M: Pratt at 24 m / 8 / 3 m → autosave "Pratt truss" 16 N / 29 M, span 24, height 3, dirty; Ctrl+Z → Warren 13 / 23, clean. K truss at 18 / 4 / 1.5 → 13 N / 24 M, span 18, height 1.5; Ctrl+Z back. Howe at 20 / 10 / 2.5 → 20 N / 37 M, kill -9 + relaunch restored it UNSAVED. Normal width, via ⋯ → Presets…: Warren at 16 / 4 → 9 N / 15 M; Ctrl+Z. Audit: **Broken**) | R after fix (Release in Chromium, fresh profile, empty document: span 24 / panels 8 / depth 3 committed with Enter rebuilt the tiles to 24 m · 16 N · 29 M; Pratt tile → "Pratt truss" UNSAVED, 16 N / 29 M, 3.000 m panels on 24.000 m, flyout closed; Ctrl+Z → Untitled 0 / 0; Ctrl+Y → Pratt again. K truss tile at defaults → 19 N / 36 M, and a reload 8 s later restored it UNSAVED from browser storage. No console errors. Audit: **Broken**) | X | X | X |
| 6. Share code round trip | R (Ctrl+Shift+C toast "215 characters", no fallback file; New → 0 nodes; Ctrl+Shift+V restored identical nodes/members; corrupt `LP1.` code refused, document kept) | X | X | X | X |
| 7. Failures and fixes | R (`LOADPATH_PRESET=pratt:24,6,1.6`: 6 BUCKLES pills and rows; J toast "M8 buckles at 294% · 2 of 6"; U: 6 members → `chs114`, max 294 % → 68 %, "+157.3 kg"; Shift+U: 14 members, −131.8 kg, 88 %) | X | X | X | X |

## Findings

### Fixed
- [Interactions] Preset tiles in the PRESETS flyout loaded nothing (audit: R, the click closed the flyout and the autosave kept the previous document, 2 launches; the palette's presets loaded only at the default parameters) - fixed at `997f7be` (`Views/TitleStrip.xaml.cs` `OnPresetClick`). Cause, proven with trace lines in the tile's Click and in `EditorModel.LoadPreset`: the `ElementName=Strip` command binding resolved (an `AsyncCommand`, `CanExecute("pratt")` true at Click). A button raises Click before it executes its command, and `PresetsFlyout.Hide()` in Click unloaded the sheet at once: right after it the tile had `IsLoaded` false, `DataContext` null and `CommandParameter` null, so the generated command's `CanExecute(null)` was false and `LoadPreset` never ran. Fix: the hide is posted to the dispatcher, so the command runs first. `LoadPreset` now writes a Debug trace line with the parameters it read. Proof R: workflow 5 row above (desktop, both layouts) and WebAssembly (Chromium, Pratt at 24 / 8 / 3 and K truss); trace `preset pratt span=24 panels=8 depth=3`, `preset k span=18 panels=4 depth=1.5`, `preset howe span=20 panels=10 depth=2.5`, `preset warren span=16 panels=4 depth=2`. No unit test: the defect lived in the view's event order; the model and engine paths were already covered (`ShareAndPresetTests`).

### Blockers
None open.

### Majors (functional)
- [Platform behavior] No safe-area handling on the phone heads - no `SafeArea`, `VisibleBounds` or edge-to-edge setting anywhere in `Loadpath/` (Toolkit is referenced) - evidence C (absence), effect I - on iOS notch devices and Android 15+ (edge-to-edge enforced) the title strip and the bottom tool strip can sit under the status bar and the gesture bar. Owner: uno-toolkit (`utu:SafeArea` on the page root).

### Majors (head)
- [Testing] Android never run: every workflow X; CI builds it - run workflows 1-7 on an emulator.
- [Testing] iOS never run: every workflow X; CI builds the simulator head.
- [Testing] WinAppSDK never run: every workflow X; CI builds x64. Picker owner window (`InitializeWithWindow`) only provable there.
- [Testing] WebAssembly partly run: workflows 3, 6 and 7 X there (section change, share code through the browser clipboard, failures).

### Minors
- [Interactions] A tile click right after typing a sheet value without Enter only commits the value: the press blurs the field, the commit rebuilds the six tiles under the pointer (new sketch and size) and the press does not become a Click; the sheet stays open with the new thumbnails and the next click loads with the typed value - `Views/TitleStrip.xaml` tile `ItemsRepeater`, `Presentation/EditorModel.cs` `RebuildPresetsAsync` - evidence R (desktop after the fix: span typed 30 without Enter, Fink tile click → thumbnails 30 m, document unchanged, no `preset` trace; second click → `preset fink span=30`, Fink 7 N / 11 M. Typing the value it already had, then one click, loads). Found in the fix pass.
- [Interactions] The deflection exaggeration has no control - README.md:32 promises "deflected ghost with exaggeration slider", SPEC flow 3 says "adjust exaggeration in status bar"; only two palette commands step it by 0.1 (`Presentation/EditorEngine.cs:383-384`) and the readout sits in the layers flyout - evidence R (palette twice → legend "×1.2", `settings.json` `"Exaggeration": 1.2`). HANDOFF lists the removal as an open question. Partial.
- [Data] Undoing a sample, preset or paste returns to the save point but drops the file - `Presentation/EditorEngine.cs:138` sets `FilePath = null`, the undo does not restore it - evidence R (after Ctrl+Z autosave `dirty False, file None`); the next Save asks for a location again.
- [Logic] The node and member tools can put a node exactly on top of an existing one: node snapping only looks 10 px around the pointer (`Core/Geometry/SnapEngine.cs:32-39`), then grid snap can land on an occupied point and `MemberTool` adds a new node there (`Workspace/Tools/MemberTool.cs:64-70`) - evidence R (WebAssembly: a click 20 px off N1 made N4 at the same (0, −2), likewise N5/N6; the result is 7 nodes, 4 unsupported). Duplicate members are refused, coincident nodes are not.
- [Responsive] At 844 x 390 (landscape phone, Normal layout) the floating tool rail runs into the status bar and the free-placement toggle is cut off - `Views/ToolRail.xaml` - evidence R (screenshot); still reachable from the palette.
- [Responsive] Changing the window width keeps the old view: at 1024 px the Warren truss runs off the left edge until Ctrl+0 - evidence R.
- [Failure handling] Autosave and settings write failures are swallowed - `MainPage.xaml.cs:770` `OnAutosaveTick` catch, `Services/SettingsService.cs:72`; a corrupt autosave is discarded without a word (`Presentation/DocumentSession.cs:197-201`) - evidence C.
- [Failure handling] No global unhandled-exception handler or log (`App.xaml.cs`); handlers are guarded one by one (`GuardAsync`, `RunAsync`) - evidence C.
- [Platform behavior] Linux without an xdg-desktop-portal: Open loads the newest `.loadpath` in `~/Documents/Loadpath` with no choice (documented fallback, `Services/FileService.cs:74-80`) - evidence R; the picker path itself X here.
- [Visual quality] Cold start draws the mono labels (title, toggles) in the fallback face for the first seconds (seen at 0.5 s, and still at 9 s on one loaded run) - evidence R (screenshots); settles without interaction.
- [Visual quality] With `LOADPATH_MODE=forces` over a saved Utilization mode the toolbar shows UTILIZATION while the canvas draws forces, until the mode changes - `Presentation/EditorModel.cs` `PullOptionsAsync` - evidence R (Debug fixture path; pressing 1 / 2 by hand stays in sync). Worth a check on WebAssembly, which applies settings after the views exist.
- [Visual quality] On WebAssembly, right after the load tool loaded the selected N3, the node inspector still read "Free node", δ "—" and Y "-0.000 m" while FY showed −10.0; reselecting N3 showed the right values - evidence R (one observation; desktop showed them at once). Seen again in the fix pass (one Chromium run, screenshots 2.5-3 s after the key): after Ctrl+Z from the Pratt preset to the empty document the summary kept the old OVER CAPACITY list (M15 / M16 BUCKLES 109 %); after Ctrl+Y the outline's NODES header read 0 above 16 rows and the summary showed LIGHTEN MEMBERS beside 109 %; the undo / redo buttons looked disabled in both. A reload 3 s after that Ctrl+Y restored the empty document (not investigated; a reload 8 s after a tile load restored the preset).
- [Accessibility] The structure on the canvas has one automation name ("Structure workspace") and no per-node/member peers; the outline list is the accessible route - evidence C.
- [Testing] No UI automation (`.tests/` scenarios or runtime tests); states are reached through Debug fixtures, which is fine for a local tool - evidence C. HANDOFF.md is stale (2026-10-01, 26 tests, `main`).

## Not checked (X)
- Real touch, pinch and two-finger pan - `xdotool` has one pointer; `LOADPATH_TOUCH=1` proved tap (18 px off N4 selects it) and long press (node menu with 44 px rows) through the touch path - check on Android.
- Mouse-wheel zoom - X11 button 4/5 arrive as presses on this host (README) - check on Windows or macOS.
- Platform file pickers (Open / Save as) - no portal in the container - check on Windows and a Linux desktop session.
- Keyboard tab order, Escape on every dialog, contrast ratios, text scaling - not driven - tab through with a screen reader on Windows.
- Release cold start - Debug only here (first layout 2.5 s under a shared CPU) - time a Release publish.
- Failure-row click in the summary inspector - read in code (`InspectorPanel.xaml:127`), not driven; J and the outline select the same members.

## Noted for other skills
- uno-audit: `MainPage.xaml.cs` is 789 lines (menus, drag-drop, toast, autosave, sizing, fixtures); the context menu and the section drop push edits from code-behind (`AddNodeEdit`, `SetSectionEdit`).
- gold-standard-pass: reaction labels overlap the legend when a structure sits low on the sheet; at phone width the floating bar covers the selected member.

## Unresolved Questions
- Authentication N/A: single-user local tool with files, no accounts (SPEC scope). Confirm.
- The seven critical workflows are derived (SPEC flows 1-3, Stage 5, README). Confirm presets as critical (it set the verdict at audit; fixed since).
- Deflection exaggeration: restore a slider (README, SPEC) or change the docs to "palette only" (HANDOFF open question)?
- Android, iOS and WinAppSDK have no runtime proof at all. In scope for this sample, or CI build only?
- Safe areas: run the phone heads on a notch device / Android 15 before calling the layout done.
- WebAssembly: workflows 3, 6 and 7, and the Save/Open download path, were not driven in Chromium. Run them before calling the browser head done.
