# HANDOFF — Loadpath A pass
Updated: 2026-11-04 16:00

## Where we are
The A pass ran on `feat/production-readiness` from `e00ff82`. The owner-approved DI refactor and MainPage split
(`docs/UNO-AUDIT.md`, Remaining 1) is done, the one functional Major (safe areas) is fixed and proven on the CI emulator,
and the layout, theme, keyboard and toast items from the brief are fixed. `docs/COMPLETENESS.md` verdict: Complete with
gaps, 0 functional Majors (head Majors: iOS and WinAppSDK never run, Android launch-only, WebAssembly 5 of 7 workflows).

Commits (pushed): `cd5c094` refactor (host, services, MainPage split, toasts), `1b366a7` floating bar off the drawing,
`e4a02e0` safe area, `a831978` input edges 3:1, `6356c62` rail wraps, `dac50ec` preset one click, `8a6bd7e` Tab reach and
named fields, `bacd11f` IL2026 at the host build (the WebAssembly job failed on `cd5c094`), `22d21be` preset values handed
to the command (WebAssembly race), `17525dc` docs and evidence, `e62c3e4` an unused interface member dropped, and
`adced8f` the owner's CI concurrency block (a newer push cancels the run it supersedes), `65aab78` projections guarded
against out-of-order MVUX updates (CI failed at `8ce6f59` on a model test that read a stale state; the model tests now wait
for each state and passed 20 runs in a row), and this docs commit (WebAssembly evidence re-run at `65aab78`). Runs created before `adced8f`
(41855773486, 41860765144, 41861286508, 41861638203) are not in the group and could not be cancelled from here (HTTP 403).

## Last verified state
- Build: Debug desktop 0 warnings; Release browserwasm publish 0 compiler warnings (ILLink trim warnings only, from Uno's
  and MVUX's generated code, as before). CI run 41855773486 at `8a6bd7e`: tests, desktop win/linux, WinAppSDK, Android and
  the emulator smoke green; WebAssembly failed on IL2026 (fixed in `bacd11f`); macOS jobs were queued. Check the run for
  the head after `22d21be`.
- Tests: 261/261 (was 190), 20 consecutive Release runs green: page model (`EditorModelTests`), container (`CompositionTests`), settings, toasts, close guard,
  canvas menu, canvas layout, wrap stack, autosave debounce.
- Runtime, Debug desktop under Xvfb :464 (`APP_NO_HOTDESIGN=1`, `LIBGL_ALWAYS_SOFTWARE=1`, `xdotool`): widths 390, 599,
  600, 800, 844 x 390, 1100, 1440 in Light and Dark (`docs/evidence/desktop-widths-*.png`); floating bar above the drawing
  at 390 px; rail wrapped at 844 x 390; two failure toasts in turn with file names; preset one click with a typed value;
  palette Enter; canvas menu Add node + Ctrl+Z; section chip dragged onto M3; close prompt Cancel / Discard (WM_DELETE_WINDOW
  sent with a ctypes script); kill -9 + relaunch restored the autosave UNSAVED; Tab order logged with `LOADPATH_TRACE=1`.
- WebAssembly: Release publish at `65aab78` served locally, driven in headless Chromium (Playwright): Warren sample,
  presets sheet 24 / 6 / 1.6 (last value not entered), Pratt in one click (6 over capacity, 294 %), J (M9), U (68 %),
  Ctrl+Z (294 % again), no page errors (`docs/evidence/wasm-*.png`). Under swiftshader the browser lags input by seconds;
  the driver waits 8-12 s per step.
- Android: emulator job dump at `8a6bd7e`: title strip y 177-226 (was 49-98), ZOOM 100% y 2264-2306 (was 2327-2369),
  marker found after 11 s; marker text unchanged, so the job's `APP_CONTENT_MARKER` is unchanged.
- Lint (`xaml-semantic-lint.ps1 -Path Loadpath`): CARD 0 · HEX 0 · TOKENTHEME 0 · BACKBAR 0 · CODEBEHIND 0 · OVERLAY 0 ·
  RESPONSIVE 0 · BUILTIN 0 · ICON 0. Automation names: 70 / 2,787 XAML lines = 25.1 per 1,000.

## Guidance applied
| Skill / rule | Applied at |
|---|---|
| uno-extensions-services: host, DI, ILogger, IThemeService | `Loadpath/App.xaml.cs`, `Loadpath/AppServices.cs`, `Loadpath/Services/ThemeSwitcher.cs` |
| mvux: model tested through its states, commands off the UI thread | `Loadpath/Presentation/EditorModel.cs`, `Loadpath.Tests/EditorModelTests.cs` |
| uno-toolkit: `utu:SafeArea` (margin mode) | `Loadpath/MainPage.xaml` |
| winui-xaml: tab navigation in repeaters, named inputs | `Views/OutlinePanel.xaml`, `Controls/NumberField.cs` |
| dotnet-csharp: constructor injection, narrow catches, async void guarded | `Presentation/DocumentSession.cs`, `Services/CloseGuard.cs`, `MainPage.xaml.cs` |

## Next actions (in order)
1. Confirm CI is green on the head (the run for the last commit; WebAssembly failed only at `8a6bd7e`, before `bacd11f`).
2. Owner calls on the two proposed decisions in README (unhandled exceptions keep the app running; exaggeration stays a
   palette command), and on the remaining uno-audit items 2-5 and 7 (InfoBar, ToggleSplitButton, light-dismiss palette,
   ToolTip/Separator templates, test-package bumps).
3. Minors left in COMPLETENESS: refit after a width change, coincident nodes, undo of a sample detaching the file, WebAssembly
   stale outline header after a sample loads, workflows 3 and 6 on WebAssembly.

## Notes
- The page resolves its services from `App.Services` (the window scope); the theme service is scoped to that window.
- The generated `EditorViewModel` needs the UI dispatcher: tests construct `EditorModel` with `TestUiThread`.
- Keyboard: after Enter on an outline row the row is rebuilt and focus moves to the list's first row (not investigated).

## Relaunch
```bash
cd /home/user/loadpath
dotnet build Loadpath/Loadpath.csproj -c Debug -f net10.0-desktop -p:LoadpathTargetFrameworks=net10.0-desktop
dotnet test Loadpath.Tests/Loadpath.Tests.csproj -p:LoadpathTargetFrameworks=net10.0
Xvfb :464 -screen 0 1600x1000x24 -nolisten tcp &
DISPLAY=:464 HOME=<scratch home> APP_NO_HOTDESIGN=1 LIBGL_ALWAYS_SOFTWARE=1 LOADPATH_TRACE=1 \
  LOADPATH_RESET=1 LOADPATH_SAMPLE=warren Loadpath/bin/Debug/net10.0-desktop/Loadpath   # logs go to the console
dotnet publish Loadpath/Loadpath.csproj -f net10.0-browserwasm -c Release -p:LoadpathTargetFrameworks=net10.0-browserwasm -o <dir>
```
