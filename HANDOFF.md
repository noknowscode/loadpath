# HANDOFF — Loadpath uno-audit
Updated: 2026-11-04 06:10

## Where we are
The uno-audit pass ran on `feat/production-readiness` from `1c788fc` (report: `docs/UNO-AUDIT.md`). It removed 15
unused usings and 13 dead Core members, merged the duplicated `IsDown` and `SafeName` helpers, moved the canvas menu's
add-node edit and the section drop into `EditorEngine`, bound the palette rows to a generated `RunPalette` command,
replaced the silent autosave and settings-write catches with toasts, and re-checked every `xaml-lint: allow` comment
(13 that suppressed nothing are gone; the rest name why the replacement does not fit). The preset-tile fix from
`997f7be` is unchanged. The owner approved the DI and MainPage split for the next pass; its plan is item 1 of
"Remaining" in `docs/UNO-AUDIT.md`.

## Last verified state
- Build: Release `net10.0-desktop`, `net10.0-browserwasm` and `net10.0-android` 0 warnings / 0 errors each.
  Tests 190/190 (was 183). WinAppSDK and iOS are CI-only.
- Runtime: Debug desktop under Xvfb, driven with `xdotool`: palette row click (light 1440 px, dark 390 px), Enter in the
  palette, canvas menu "Add node here" plus Ctrl+Z, section chip dragged onto a member, Shift-click in the outline, and
  the autosave/settings failure toasts (files replaced by folders). Idle CPU 0 % over 10 s.
- Git: `feat/production-readiness`, audit commits `e19ef48` to `8b8292c` plus the docs commit with this file.
- Lint: CARD 0 · HEX 0 · TOKENTHEME 0 · BACKBAR 0 · CODEBEHIND 0 · OVERLAY 0 · RESPONSIVE 0 · BUILTIN 0 · ICON 0.
  Allowed with reasons: CARD 8 · CODEBEHIND 29 · RESPONSIVE 1. Info: TOKEN 172 · TEMPLATE 11.

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| uno-scaffolding code-behind budget: edits live in the engine, not the page | `Loadpath/Presentation/EditorEngine.cs:193`, `:201` |
| mvux: generated command bound from a template | `Loadpath/Presentation/EditorModel.cs:437`, `Loadpath/Views/CommandPalette.xaml:34` |
| dotnet-csharp: narrow catch filters, failures reported | `Loadpath/Presentation/DocumentSession.cs:213`, `Loadpath/Services/SettingsService.cs:80` |
| uno-toolkit: breakpoints as `{utu:Responsive}` | `Loadpath/MainPage.xaml:28` |
| winui-xaml: virtualized outline | `Loadpath/Views/OutlinePanel.xaml:75` |
| skiasharp-uno: paints allocated once | `Loadpath/Workspace/WorkspaceRenderer.cs:21` |

Read, not applied: uno-extensions-services hosting/DI (planned, Remaining item 1); `utu:SafeArea` (COMPLETENESS Major,
next pass); uno-navigation (single page).

## Next actions (in order)
1. DI and the MainPage split (`docs/UNO-AUDIT.md`, Remaining 1): Hosting + Logging + ThemeService, services registered,
   `MainPage.xaml.cs` from 764 to about 200 lines, then re-run the seven critical workflows on desktop and WebAssembly.
2. `utu:SafeArea` on the page root for the phone heads (COMPLETENESS Major), checked on Android.
3. Owner calls: InfoBar banners, ToggleSplitButton for Reduce noise, the palette as a light-dismiss Popup, the
   ToolTip/Separator templates (Remaining 2-5), and the two test-package bumps.

## Open questions
- Dark mode and Toolkit questions in the previous handoff are settled (README Architecture); still open: the
  exaggeration control (README promises a slider, there is none) and whether Android/iOS/WinAppSDK need runtime proof.
- Settings and autosave failures share one toast slot: at launch the autosave message replaces the settings one within
  a second. Fine for a sample, or should the toast queue?

## Relaunch
```bash
cd /home/user/loadpath
dotnet build Loadpath/Loadpath.csproj -c Debug -f net10.0-desktop -p:LoadpathTargetFrameworks=net10.0-desktop
dotnet test Loadpath.Tests/Loadpath.Tests.csproj -p:LoadpathTargetFrameworks=net10.0
Xvfb :454 -screen 0 1600x1000x24 -nolisten tcp &
DISPLAY=:454 HOME=<scratch home> APP_NO_HOTDESIGN=1 LIBGL_ALWAYS_SOFTWARE=1 LOADPATH_TRACE=<trace file> \
  LOADPATH_RESET=1 LOADPATH_SAMPLE=warren Loadpath/bin/Debug/net10.0-desktop/Loadpath
# Android: -p:AndroidSdkDirectory=/home/user/android-sdk -p:JavaSdkDirectory=/usr/lib/jvm/java-21-openjdk-amd64
```
Fixture hooks (Debug only) are listed at the end of README's Validation section. `tools/drive.sh` and
`tools/capture.sh` use display :99 and `pkill -x Loadpath`; prefer a private display and killing only your own PID.
