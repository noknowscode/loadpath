# Completeness audit: Loadpath (2026-11-04, Uno.Sdk 7.0.0-dev.115, heads: desktop, wasm, android, ios, windows)

Audited commit: `39ce1a0` on `feat/production-readiness`. Report mode: nothing was fixed.
Fix pass (2026-11-04): the one Blocker (preset tiles) fixed at `997f7be` and re-proved on desktop and WebAssembly; see
Findings / Fixed.
A pass (2026-11-04, after the DI refactor): the functional Major (safe areas) and seven Minors fixed and re-proved; see
Findings / Fixed. The Android emulator smoke job in CI now launches the Release APK on every push (launch only).
A pass 2 (2026-11-04, closed scope from the code review): the three logic Minors (coincident nodes, the file an undone
sample drops, no refit after a width change) fixed, the stale exaggeration copy aligned, and a regression of the
EditorModel split found at runtime and fixed (the view could not write the projections' states); see Findings / Fixed.
Desktop (Xvfb :613) drove workflows 1-7 again at `e7ea608`; WebAssembly drove the palette, the presets sheet, J, U,
Ctrl+Z and a narrower window (`docs/evidence/wasm-palette-presets-refit.png`).
Runtime proof: Debug `net10.0-desktop` under Xvfb (1600 x 1000 screen, 1440 x 960 window), driven with `xdotool` and the
Debug fixture hooks, post-state read from the autosave JSON, the saved `.loadpath` file, `settings.json`, `LOADPATH_TRACE`
and the `VERIFY` stderr lines. WebAssembly: Release publish served locally and driven in headless Chromium (Playwright, 1440 x 900). Android, iOS and
WinAppSDK were not run here; CI built all five heads at this commit (run 41351276840, all jobs green).
Re-checked from the 2026-11-01 review: the autosave now restores its file and its unsaved state (R), a sample is an undoable
replacement (R), WebAssembly raises the leave-page prompt (R); Esc cancelling a section drag is read in code only (C).

Verdict: **Complete with gaps, 0 functional Majors** (was Incomplete at audit; the preset-tile Blocker and the safe-area
Major are fixed; the remaining Majors are head Majors: iOS and WinAppSDK never run, Android launch-only). Counts after
A pass 2: 0 Blockers, 3 Majors (all head), 7 Minors (was 10).
Safe to show: draw and solve, drag with live re-solve, undo/redo, save/open/autosave restore, section change, unsaved-changes
prompt, share code round trip, failures with J / U / Shift+U, palette, phone layout (portrait and landscape), PRESETS
flyout with span / panels / depth. / Keep off screen: the deflection exaggeration slider (there is none; the palette steps it).

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
| 3 | Interactions | Partial | R | 0 | 0 | 1 | preset tiles load with the sheet's parameters, a value typed without Enter included (fixed, desktop and WebAssembly); exaggeration has no control |
| 4 | Logic | Complete | R | 0 | 0 | 0 | forces match method of joints; upsize/lighten as stated; a snapped point on a node selects it (fixed) |
| 5 | Data | Complete | R | 0 | 0 | 0 | desktop file + autosave and WebAssembly browser-storage restore proven; undoing a sample/preset/paste gives the file back (fixed) |
| 6 | States | Complete | R | 0 | 0 | 0 | empty, mechanism, failures, refused paste, malformed file; loading N/A (synchronous) |
| 7 | Forms | Complete | R | 0 | 0 | 0 | number fields commit on Enter; preset fields rebuild thumbnails |
| 8 | Authentication | N/A | C | 0 | 0 | 0 | single-user local tool, no accounts (confirm) |
| 9 | Responsive/adaptive UI | Complete | R | 0 | 0 | 0 | rail wraps at 390 px height and the floating bar stays off the drawing on phones (fixed); a narrower window refits a view it would cut off (fixed) |
| 10 | Platform behavior | Partial | R | 0 | 0 | 1 | utu:SafeArea keeps the shell inside the visible bounds (fixed; Android emulator dump: title strip 49-98 → 177-226 px); picker path X on Linux |
| 11 | Failure handling | Complete | R | 0 | 0 | 0 | autosave and settings failures toast in turn with file names (fixed); unhandled and unobserved exceptions are logged and toasted (fixed) |
| 12 | Accessibility | Partial | R | 0 | 0 | 1 | all controls named, 25.1 names per 1,000 XAML lines; Tab reaches every control and outline row (59 stops on Warren, runtime); input edges 3:1 (fixed); canvas has no per-element peers (outline is the alternative) |
| 13 | Performance | Complete | R | 0 | 0 | 0 | idle 0.1 % CPU; solve 0.1-0.4 ms; Debug first layout 2.5 s |
| 14 | Security/privacy | Complete | C | 0 | 0 | 0 | no secrets, no permissions, every LOADPATH_ hook `#if DEBUG` |
| 15 | Visual quality | Partial | R | 0 | 0 | 3 | fallback face on cold start; mode toggle desync under a fixture; stale node inspector once on WebAssembly |
| 16 | Testing | Partial | R | 0 | 3 | 1 | 325 tests incl. the page model, its bindable states, the canvas's touch input, frame and fitting, and the container; android launch-smoked in CI, ios and windows never run; wasm 5 of 7 workflows; no UI automation |

## Critical workflows by head

| Workflow | desktop | wasm | android | ios | windows |
|---|---|---|---|---|---|
| 1. Draw and solve | R (A pass 2: empty document, N at (1,0) (3,0) (2,1), M chain, Pin and Roller X from the inspector, L on N3: M1 +5.0, M2/M3 -7.1; drag N3 to (2,2): +2.5 / -5.6; Ctrl+Z / Ctrl+Y. Before: autosave: 3 nodes, 3 members, Pin + RollerY, Fy -10; M1 +5.0, M2/M3 -7.1 = method of joints; drag N3 y -1 → 1 re-solves to +2.5/-5.6; one Ctrl+Z / Ctrl+Y) | R (Release in Chromium: same clicks gave N1 Pin, N2 Roller, N3 −10 kN; M1 +5.0, M2/M3 −7.1) | X (no device) | X (no Mac) | X (no Windows) |
| 2. Save, open, autosave restore | R (A pass 2: Ctrl+S and Ctrl+O through the fallback, each toast saying so; kill -9 + relaunch restored the pasted Pratt UNSAVED. A pass: Howe roof, Delete on N2, autosave written dirty with 11 nodes 1.2 s later; kill -9 + relaunch restored it UNSAVED. Before: Ctrl+S wrote `~/Documents/Loadpath/Untitled.loadpath`; Ctrl+O read it back; kill -9 + relaunch restored N3 x 2.5 as UNSAVED with the file attached; Ctrl+S wrote x 2.5 to that file) | Partial (browser relaunch restored the triangle as UNSAVED from browser storage; Save/Open download pickers not driven) | X | X | X |
| 3. Section change → utilization | R (A pass 2: combo box CHS 114.3x5.0 on M9: 294 % → 68 %; the 114.3x5.0 chip dragged onto M8: "applied to member 8", 4 over capacity. Before: chip 76.1x4.0: M2 21 % → 9 %; ComboBox CHS 114.3x5.0: autosave `chs114`) | X (not driven) | X | X | X |
| 4. Unsaved-changes guard | R (A pass 2: Ctrl+N on the Pratt: Cancel kept 12 nodes, Discard gave Untitled. Ctrl+N dialog: Cancel kept 3 nodes, Discard gave 0; WM_DELETE_WINDOW → dialog → Discard exited and deleted the autosave. A pass, through CloseGuard: WM_DELETE_WINDOW → dialog; Cancel kept the window and the autosave; again → Discard closed the window and deleted the autosave) | R (Ctrl+N raised the in-app dialog, Discard cleared it; closing the tab with edits raised `beforeunload`) | X | X | X |
| 5. Parametric preset (flyout) | R (A pass 2: 24 / 6 with Tab and 1.6 not entered, one click on Pratt: 6 over capacity, 294 %, trace `preset pratt span=24 panels=6 depth=1.6`. A pass: span 24 / panels 6 committed with Tab, depth 1.6 typed without Enter, one click on Pratt → 6 over capacity, 294 %, trace `preset pratt span=24 panels=6 depth=1.6`; span 30 typed then one click on Fink → 30 m) R after fix `997f7be` (from Warren 13 N / 23 M: Pratt at 24 m / 8 / 3 m → autosave "Pratt truss" 16 N / 29 M, span 24, height 3, dirty; Ctrl+Z → Warren 13 / 23, clean. K truss at 18 / 4 / 1.5 → 13 N / 24 M, span 18, height 1.5; Ctrl+Z back. Howe at 20 / 10 / 2.5 → 20 N / 37 M, kill -9 + relaunch restored it UNSAVED. Normal width, via ⋯ → Presets…: Warren at 16 / 4 → 9 N / 15 M; Ctrl+Z. Audit: **Broken**) | R (A pass 2 at `e7ea608`: the palette filtered "sample" to the three samples; 24 / 6 / 1.6 → Pratt in one click, 6 over capacity, 294 %; then the window narrowed to 1150 px reframed it, `docs/evidence/wasm-palette-presets-refit.png`. Earlier: Release in Chromium, fresh profile, empty document: span 24 / panels 8 / depth 3 committed with Enter rebuilt the tiles to 24 m · 16 N · 29 M; Pratt tile → "Pratt truss" UNSAVED, 16 N / 29 M, 3.000 m panels on 24.000 m, flyout closed; Ctrl+Z → Untitled 0 / 0; Ctrl+Y → Pratt again. K truss tile at defaults → 19 N / 36 M, and a reload 8 s later restored it UNSAVED from browser storage. No console errors. Audit: **Broken**. A pass, after the DI refactor: Warren sample, then 24 / 6 with Tab and 1.6 typed without Enter, one click on Pratt → nodes at y 1.6, 6 over capacity, 294 % (`docs/evidence/wasm-pratt-one-click.png`)) | X | X | X |
| 6. Share code round trip | R (A pass 2: Ctrl+Shift+C "438 characters", Ctrl+N → Discard, Ctrl+Shift+V restored the Pratt (12 N / 21 M). Before: Ctrl+Shift+C toast "215 characters", no fallback file; New → 0 nodes; Ctrl+Shift+V restored identical nodes/members; corrupt `LP1.` code refused, document kept) | X | X | X | X |
| 7. Failures and fixes | R (A pass 2: J "M9 buckles at 294% · 1 of 6"; U 6 members +157.3 kg → 68 %; Shift+U 14 members −131.8 kg → 88 %; Ctrl+Z twice → 294 %. Before: `LOADPATH_PRESET=pratt:24,6,1.6`: 6 BUCKLES pills and rows; J toast "M8 buckles at 294% · 2 of 6"; U: 6 members → `chs114`, max 294 % → 68 %, "+157.3 kg"; Shift+U: 14 members, −131.8 kg, 88 %) | R (A pass 2: J, U → 68 %, Ctrl+Z → 294 %. A pass, Release in Chromium: J selected M9 at 294 %; U → M9 CHS 114.3x5.0, WITHIN CAPACITY, max 68 %; Ctrl+Z → 294 % again; `docs/evidence/wasm-presets-failures-upsize.png`) | X (launch smoke only) | X | X |

## Findings

### Fixed
- [Interactions, regression found in A pass 2] After the EditorModel split (`ee96479`) the view could read but not write
  the projections' states: the palette did not filter as the user typed, a value typed into a node, load or section
  field changed nothing, and the reduce-noise switch and its layer boxes did not reach the engine. Cause: each projection
  owned a SourceContext of its own, and the generated view model wraps another context's state read-only. Fixed at
  `e7ea608` (the model gives its pieces its context). Proof R on desktop: "sample" in the palette lists the three
  samples; 2.5 typed into N3's X moves N3; REDUCE NOISE hides nine layers and the Grid box brings the grid back; the
  section combo box takes M9 from 294 % to 68 %; and in Chromium the palette filters. Tested:
  `BindableStatesTests` (46 of 57 states failed before the fix).
- [Responsive] A narrower window kept a view the user had moved, cutting the structure off - fixed at `f091e39`: a
  resize refits a moved view when it would push part of a structure that was all in view off the canvas; a fitted view
  follows every resize as before. Proof R: panned at 1440 px, widened back (view kept, 67 %), narrowed to 1150 px
  (refit to 56 %, both supports in view; `docs/evidence/desktop-refit-after-width-change.png`); in Chromium the Pratt
  preset at 1440 x 900 reframed at 1150 x 900. Tested: `ViewFittingTests`.
- [Logic] The node and member tools could stack a node on an existing one - fixed at `a4f1f63`: a point that guides and
  the grid put on a node is that node. Proof R: with the node tool, a click 16 px off N1 (outside the node tolerance,
  snapped to its grid point) selected N1 and the node count stayed 3. Tested: `ToolPlacementTests`.
- [Data] Undoing a sample, preset or paste dropped the file - fixed at `9ec6346`. Proof R: Ctrl+O (fallback) opened
  `Untitled.loadpath`, the palette loaded the Warren sample (autosave: no file, dirty), Ctrl+Z gave the file back
  (autosave: the file, clean). Tested: `EditorEngineTests`, `DocumentSessionTests`.
- [Failure handling] Picker failures were swallowed and Open fell back to the folder silently - fixed at `3dd0928`:
  failures are logged and toasted, and the fallback says what it did. Proof R: "No file picker here: saved to
  Loadpath/Untitled.loadpath" and "No file picker here: opened the newest file, Loadpath/Untitled.loadpath".
- [Interactions, docs] SPEC flow 3 promised an exaggeration control in the status bar; it now steps it from the palette,
  as README.md:32 says (`f7eb929`).
- [Platform behavior, functional Major] No safe-area handling on the phone heads - fixed at `e4a02e0`: `utu:SafeArea` (margin mode) on the title strip (Left, Top, Right), the work area (Left, Right), the status strip (Left, Right, Bottom) and the palette. Proof R: the CI emulator job's window dump (run 41855773486, pixel_6, API 34) has the title strip's LOADPATH at y 177-226 (was 49-98, under the status bar), its buttons at 144-260 (was 16-132), ZOOM 100% at 2264-2306 (was 2327-2369, at the gesture handle) and the content marker at 787-861 (was 755-829); the marker text is unchanged and the job passed. Desktop and WebAssembly have no unsafe area (no change in the width matrix).
- [Interactions] A tile click right after typing a sheet value without Enter needed a second click - fixed at `dac50ec` and `22d21be`: tiles no longer take focus on a press, the click commits the field being typed in, and the tile's command receives a `PresetChoice` with the fields' values (the bound states had not caught up on WebAssembly). Proof R on desktop (Pratt 24 / 6 / 1.6 with 1.6 not entered: one click, 294 %) and in Chromium (same values, nodes at y 1.6). Tested: `EditorModelTests.A_tile_loads_with_the_values_it_is_handed_clamped_and_the_fields_show_them`.
- [Responsive] At 844 x 390 the floating tool rail ran into the status strip and cut off free placement - fixed at `6356c62`: the rail's panel (`Controls/WrapStack`) starts a second column when the canvas is shorter than the rail; the rail keeps 24 px off the status strip. Proof R: `docs/evidence/desktop-landscape-rail.png` (light and dark); `WrapStackTests`.
- [Responsive, from gold-standard notes] At phone width the floating bar covered the truss - fixed at `1b366a7`: `CanvasLayout.FloatingBar` measures the bar and keeps it off the drawn members, load arrows and labels and supports (above the selection, else above the drawing, else below it). Proof R: `docs/evidence/desktop-phone-floating-bar.png` (Warren N3 and Howe N2 at 390 px); `CanvasLayoutTests`.
- [Accessibility] Field borders were 1.9:1 in both themes - fixed at `a831978`: a `ControlBorder` token (light 3.6 / 3.3 / 3.1:1 on Canvas / SurfaceRaised / Selection, dark 3.8 / 3.5 / 3.1:1) on the number and search fields, the combo box, the check box and the switch track.
- [Accessibility] Tab entered each item list once (one outline row, chip or tile), and the number fields' text boxes had no name - fixed at `8a6bd7e`. Proof R (`LOADPATH_TRACE=1` focus log): 59 named stops on Warren with nothing selected (rail, inspector actions, title strip, every outline row); with N3 selected the floating bar, "Node X", "Node Y", the four support toggles, "Load X", "Load Y" and "Delete selection" are reached.
- [Failure handling] The two failure toasts shared one slot and showed full paths - fixed at `cd5c094` (`ToastQueue`, `ToastText`): a failure is never replaced before it has had its time, and paths read as file names. Proof R: settings.json and autosave.loadpath replaced by folders, then `2` and Delete: "Settings not saved: Access to the path 'settings.json' is denied" for 4.5 s, then "Autosave failed: Access to the path 'autosave.loadpath' is denied. Save to keep your work." (`docs/evidence/desktop-failure-toasts.png`); full paths in the log.
- [Interactions] Preset tiles in the PRESETS flyout loaded nothing (audit: R, the click closed the flyout and the autosave kept the previous document, 2 launches; the palette's presets loaded only at the default parameters) - fixed at `997f7be` (`Views/TitleStrip.xaml.cs` `OnPresetClick`). Cause, proven with trace lines in the tile's Click and in `EditorModel.LoadPreset`: the `ElementName=Strip` command binding resolved (an `AsyncCommand`, `CanExecute("pratt")` true at Click). A button raises Click before it executes its command, and `PresetsFlyout.Hide()` in Click unloaded the sheet at once: right after it the tile had `IsLoaded` false, `DataContext` null and `CommandParameter` null, so the generated command's `CanExecute(null)` was false and `LoadPreset` never ran. Fix: the hide is posted to the dispatcher, so the command runs first. `LoadPreset` now writes a Debug trace line with the parameters it read. Proof R: workflow 5 row above (desktop, both layouts) and WebAssembly (Chromium, Pratt at 24 / 8 / 3 and K truss); trace `preset pratt span=24 panels=8 depth=3`, `preset k span=18 panels=4 depth=1.5`, `preset howe span=20 panels=10 depth=2.5`, `preset warren span=16 panels=4 depth=2`. No unit test: the defect lived in the view's event order (Click, then the command), which the offline tests cannot host; the preset builds at any span / panels / depth are covered by `ShareAndPresetTests` (`Every_preset_is_stable_and_loaded`, `Parameters_clamp`).
- [Failure handling] No global unhandled-exception handler or log - fixed at `cd5c094`: `App` logs `UnhandledException` and `UnobservedTaskException` and shows a toast; every app class logs through `ILogger`. Autosave and settings write failures were reported since `0adfab0`.

### Blockers
None open.

### Majors (functional)
None open (safe areas fixed; see Fixed).

### Majors (head)
- [Testing] Android launch-only: the CI emulator job installs the Release APK, launches it, finds the first screen and no crash on every push (run 41855773486); workflows 1-7 are not driven there.
- [Testing] iOS never run: every workflow X; CI builds the simulator head.
- [Testing] WinAppSDK never run: every workflow X; CI builds x64. Picker owner window (`InitializeWithWindow`) only provable there.
- [Testing] WebAssembly partly run: workflows 3 and 6 X there (section change, share code through the browser clipboard); workflow 7 driven in the A pass.

### Minors
- [Interactions] The deflection exaggeration has no control of its own: two palette commands step it by 0.1 between ×0.2 and ×3 (`Presentation/EditorEngine.cs:465-466`, `Presentation/ViewOptions.cs:58`) and the legend and the layers sheet show it - evidence R (palette twice → legend "×1.2", `settings.json` `"Exaggeration": 1.2`). The docs now say exactly that: README.md:32 (palette-stepped, no slider) and SPEC.md:392 flow 3 (step it from the palette; was "adjust exaggeration in status bar"). Keeping it palette-only is a proposed decision (README, Architecture) awaiting the owner. Partial.
- [Platform behavior] Linux without an xdg-desktop-portal: Open loads the newest `.loadpath` in `~/Documents/Loadpath` with no choice (documented fallback; since `3dd0928` the toast says so) - evidence R; the picker path itself X here.
- [Visual quality] Cold start draws the mono labels (title, toggles) in the fallback face for the first seconds (seen at 0.5 s, and still at 9 s on one loaded run) - evidence R (screenshots); settles without interaction.
- [Visual quality] With `LOADPATH_MODE=forces` over a saved Utilization mode the toolbar shows UTILIZATION while the canvas draws forces, until the mode changes - `Presentation/OptionMirror.cs` `PullAsync` - evidence R (Debug fixture path; pressing 1 / 2 by hand stays in sync). Worth a check on WebAssembly, which applies settings after the views exist.
- [Visual quality] (Partly addressed at `65aab78`: projections can no longer land out of order, which explains a stale status or inspector; the outline header still read "No elements yet" for a few seconds after a sample loaded in Chromium at `65aab78`, then caught up.) On WebAssembly, right after the load tool loaded the selected N3, the node inspector still read "Free node", δ "—" and Y "-0.000 m" while FY showed −10.0; reselecting N3 showed the right values - evidence R (one observation; desktop showed them at once). Seen again in the fix pass (one Chromium run, screenshots 2.5-3 s after the key): after Ctrl+Z from the Pratt preset to the empty document the summary kept the old OVER CAPACITY list (M15 / M16 BUCKLES 109 %); after Ctrl+Y the outline's NODES header read 0 above 16 rows and the summary showed LIGHTEN MEMBERS beside 109 %; the undo / redo buttons looked disabled in both. A reload 3 s after that Ctrl+Y restored the empty document (not investigated; a reload 8 s after a tile load restored the preset). A pass 2, desktop: 0.6 s after a long press selected N3 the inspector's title still read N4 while its connected members were N3's.
- [Accessibility] The structure on the canvas has one automation name ("Structure workspace") and no per-node/member peers; the outline list is the accessible route - evidence C.
- [Testing] No UI automation (`.tests/` scenarios or runtime tests); states are reached through Debug fixtures, which is fine for a local tool - evidence C.

## Not checked (X)
- Real touch, pinch and two-finger pan - `xdotool` has one pointer; `LOADPATH_TOUCH=1` proved tap (18 px off N4 selects it) and long press (node menu with 44 px rows) through the touch path - check on Android.
- Mouse-wheel zoom - X11 button 4/5 arrive as presses on this host (README) - check on Windows or macOS.
- Platform file pickers (Open / Save as) - no portal in the container - check on Windows and a Linux desktop session.
- Escape on every dialog, text scaling, a real screen reader - not driven - check on Windows with Narrator. Tab order was driven (A pass, focus log) and contrast computed (text in Tokens.xaml, input edges 3:1).
- Release cold start - Debug only here (first layout 2.5 s under a shared CPU) - time a Release publish.
- Failure-row click in the summary inspector - read in code (`InspectorPanel.xaml:127`), not driven; J and the outline select the same members.

## Noted for other skills
- uno-audit: done. `MainPage.xaml.cs` is 186 lines (was 789); the menu, the section drag, toasts, autosave, sizing and fixtures have their own classes, and the edits go through the engine.
- gold-standard-pass: reaction labels overlap the legend when a structure sits low on the sheet; at 844 x 390 the wrapped rail sits over the canvas caption ("Truss, 12.000 m span"). (The floating bar over the truss at phone width is fixed.)

## Unresolved Questions
- Authentication N/A: single-user local tool with files, no accounts (SPEC scope). Confirm.
- The seven critical workflows are derived (SPEC flows 1-3, Stage 5, README). Confirm presets as critical (it set the verdict at audit; fixed since).
- Deflection exaggeration: restore a slider (README, SPEC) or change the docs to "palette only" (HANDOFF open question)?
- iOS and WinAppSDK have no runtime proof; Android has a CI launch smoke. In scope for this sample, or CI build only?
- Safe areas: proven on the API 34 emulator (status and gesture bars); an iOS notch device is still unchecked.
- WebAssembly: workflows 3 and 6, and the Save/Open download path, were not driven in Chromium.
