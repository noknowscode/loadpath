# HANDOFF — Loadpath A pass 2, cleanup and showcase info file
Updated: 2026-11-04 21:30

## Where we are
A pass 2 (closed scope: the code review's list for Loadpath, `docs/production-readiness/reviews/2026-11-04-a-pass/loadpath.json` in the uno-lab repo) is done on
`feat/production-readiness`, from `7fc4042`. `docs/COMPLETENESS.md` verdict: Complete with gaps, 0 functional Majors;
0 Blockers, 3 head Majors, 7 Minors (was 10).

| # | Item | Commit |
|---|---|---|
| 1 | EditorModel split into projections (282 lines; pieces 61-199) | `ee96479`, `e7ea608` (regression fix, below) |
| 2 | WorkspaceView split (623 → 390): TouchInput, RenderSnapshotBuilder, ViewportAnimation, ViewFitting | `4453b3f`, `7a6473d` |
| 3 | Edits through the engine and model: supports, outline picks, section drop, canvas menu | `9f2d325`, `3206304` |
| 4 | A snapped point lands on the existing node | `a4f1f63` |
| 5 | Undo restores the file path | `9ec6346` |
| 6 | Refit after a width change | `f091e39` |
| 7 | Picker failures logged and reported; Open says when it falls back | `3dd0928` |
| 8 | `TryDecode` annotated | `99ab704` |
| 9 | Test-only members removed | `cde2a3a` |
| 10 | Tests for support, section, load, Undo/Redo | `69d5608`, `1162866` |
| 11 | Option mirror echoes kept apart | `f957632` |
| 12 | SPEC flow 3 and the COMPLETENESS exaggeration Minor aligned | `f7eb929` |
| - | Canvas font and cursor failures logged | `0fa8af3` |

Regression found at runtime and fixed (`e7ea608`): after the split the projections' states lived in their own
SourceContext, so the generated view model bound them read-only: the palette did not filter, typed node/load/section
values and the layer switches did nothing. The model now gives its pieces its context; `BindableStatesTests` checks
every `IState` the model exposes as the generated code does.

## Last verified state
- Tests: 325/325 (`dotnet test Loadpath.Tests/Loadpath.Tests.csproj -p:LoadpathTargetFrameworks=net10.0`).
- Builds: Debug desktop 0 warnings; Release browserwasm publish succeeded.
- Lint (`xaml-semantic-lint.ps1 -Path Loadpath`): 0 gating hits. Automation names: 70 / 2,787 XAML lines = 25.1 per 1,000.
- Runtime, Debug desktop under Xvfb :613 (`APP_NO_HOTDESIGN=1`, `LIBGL_ALWAYS_SOFTWARE=1`, `LOADPATH_TRACE=1`, xdotool):
  workflows 1-7 at `e7ea608` (see COMPLETENESS), the touch path with `LOADPATH_TOUCH=1` (tap, long-press menu,
  one-finger drag), the refit (`docs/evidence/desktop-refit-after-width-change.png`).
- WebAssembly: Release publish served locally, headless Chromium 1440 x 900: Warren, palette "sample", presets
  24 / 6 / 1.6 → Pratt, J, U, Ctrl+Z, then 1150 x 900 refit (`docs/evidence/wasm-palette-presets-refit.png`).
- CI run 41899759355 at `e7ea608`: unit tests, desktop linux/win/osx, WinAppSDK, WebAssembly, Android and the emulator
  smoke green; iOS was still running. Check the run for the docs commit after it.

## Decisions proposed (README, Architecture)
- A view the user moved is refit only when a narrower window would cut the structure off.
- A custom app icon in place of the template's.
- The `.studiolive` URLs point at Uno.Samples (resolve after the move; relative paths asked in uno-private#2444).
- Still awaiting the owner from the A pass: unhandled exceptions keep the app running; the exaggeration stays a palette
  command.

## Pre-transfer cleanup (after A pass 2)
- Analyzer pass (IDE0005/0051/0052/0060, CA1823, IDE0035 raised for one local build, not committed): 9 usings, a never-read
  field and an unused parameter removed (`10f7397`), the two null-forgiving operators in app code gone (`dea4964`).
- Dead members `IconLibrary.Names`, `Viewport.Fit` (`4aad1f4`); unused resources: `Themes/MotionTokens.xaml`, the
  TitleText style and 8 tokens, `Inter-SemiBold.ttf` (`ed2150e`); template leftovers and `tools/capture.sh` (`24ee866`).
- Stale comments and banners (`e2230cd`); SPEC's module tree marked as the plan (`37eb336`); doc references (`e53dff4`).
- Kept on purpose: `FileService._window` (read under `#if WINDOWS`), `CloseGuard.CloseWhenConfirmedAsync` (desktop only),
  event-handler and `Main` parameters, the WinUI lightweight-styling keys in Tokens.xaml, `docs/shot*.png` (README's gallery).
- Secret scan over tracked files: nothing found (CI references GitHub secrets by name only).
- C# 14,774 → 14,743 lines (app and Core 10,604 → 10,574); XAML 2,787 → 2,755. Tests 325. Lint 0 gating; 25.4 names per
  1,000 XAML lines. Desktop, WebAssembly (Release) and Android (Release) build with 0 warnings.

## Showcase info file
- `.studiolive/` (sample id `loadpath`): `sample.json`, `agent-context.json`, `user-conversation.json`, `icon.png`, four
  screenshots; the validator prints OK (`62c4d7a`).
- Custom icon in place of the template's: a roof truss on a pin and a roller, white on the accent #3446D6; splash the
  same mark on white (`64e1d63`).

## Next actions
1. Owner calls on the proposed decisions (README: refit rule, custom icon, `.studiolive` URLs; and the A pass's two).
2. The move to `unoplatform/uno.labs/samples-staging` (one pull request).

## Notes
- `EditorEngine.cs` is 499 lines (474 at the review): one job, not on the review's list.
- The page resolves its services from `App.Services` (the window scope).
- Touch input needs `WorkspaceView.Ui` set before `Editor` (MainPage does); without it touch is off and a warning is logged.

## Relaunch
```bash
cd /home/user/loadpath
dotnet build Loadpath/Loadpath.csproj -c Debug -f net10.0-desktop -p:LoadpathTargetFrameworks=net10.0-desktop
dotnet test Loadpath.Tests/Loadpath.Tests.csproj -p:LoadpathTargetFrameworks=net10.0
Xvfb :613 -screen 0 1600x1000x24 -nolisten tcp &
DISPLAY=:613 HOME=<scratch home> APP_NO_HOTDESIGN=1 LIBGL_ALWAYS_SOFTWARE=1 LOADPATH_TRACE=1 \
  LOADPATH_RESET=1 LOADPATH_SAMPLE=warren Loadpath/bin/Debug/net10.0-desktop/Loadpath
dotnet publish Loadpath/Loadpath.csproj -f net10.0-browserwasm -c Release -p:LoadpathTargetFrameworks=net10.0-browserwasm -o <dir>
```
