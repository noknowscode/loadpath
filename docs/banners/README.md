# Loadpath gallery banners

Banners for the Uno Platform Studio samples gallery, made from real captures of **Loadpath** (a 2D truss workbench that
solves as you draw) running as a Release WebAssembly build in headless Chromium. Nothing in them is redrawn: the UI,
structures, forces, utilizations and copy are the app's own, from its sample structures and parametric presets. Styling
sits only around and on top of the captures: device frames drawn in CSS, crop, perspective, falloff masks, blur, light,
and grounds tinted from the app's palette.

The look is the app's own drafting sheet: the light blueprint sheet and the dark drafting table at night, the canvas's
"+" grid crosses, its dashed frame hairline, and once a deep cyanotype blue (Primary sunk into the night paper). The
stylized cut runs at the Warren web's angle (2 m rise over a 1 m half-panel, 63.4 degrees), and the title is the app's
own wordmark.

Open `index.html` for the contact sheet.

## Finals

All in `finals/`: every banner at 1x (1200 × 630) and 2x (2400 × 1260), and two heroes at 1920 × 1080.

| Banner | Family | What it shows | Source captures (state · theme · viewport · DPR · session) |
|---|---|---|---|
| `showcase-desk` | showcase | Desktop window with the Warren truss, member M12 selected (its floating bar and member inspector), between a Dark phone with the overloaded Pratt and its inspector sheet and a Light phone with the Howe roof truss; light blueprint sheet | `desk-m12-light` (Warren, M12 clicked · Light · 1440×900 · 2 · A) ; `phone-pratt-sheet-dark` (Pratt 24 m / 6 / 1.6 m, inspector sheet open · Dark · 390×844 · 2 · C) ; `phone-roof-light` (Howe roof · Light · 390×844 · 2 · C) |
| `showcase-phones` | showcase | Three phones on a deep blueprint: Warren with its inspector sheet (Dark), the Howe roof in Utilization (Light, centre, raised), the overloaded Pratt with its red pills (Dark) | `phone-warren-sheet-dark`, `phone-roof-util-light`, `phone-pratt-dark` (390×844 · 2 · C) |
| `showcase-daynight` | showcase | The Warren truss on the light sheet in front, the same truss in Utilization on the dark table behind; the ground turns from paper to night | `desk-warren-light` (Warren, Forces · Light), `desk-util-dark` (Warren, Utilization · Dark) (1440×900 · 2 · A) |
| `focus-truss` (+ hero) | focused | The truss canvas at night: the Warren sample's member forces drawn by line style (tension as an open ribbon, compression hatched), 15 kN loads, reactions, the deflected ghost and the dimension chain, laid back 24 degrees on the drafting table and dissolving into it | `truss-forces-dark` (Warren, Forces · Dark · 1080×720 · 3 · B; canvas crop 928×380 CSS px, tool rail left out) |
| `focus-utilization` | focused | The utilization ramp: the Warren truss in Utilization mode, members from Primary through Warn towards Error with percent labels (5 % to 87 %), and the inspector's result for the 87 % end post M12 (compression 41.93 kN, the Warn bar, stress, buckling governs) | `util-macro-dark` (Warren, Utilization · Dark · 1080×720 · 3 · B; crop 600×300) ; `inspector-result-dark` (Warren, M12 clicked · Dark · 1440×900 · 3 · B2; the inspector's Result section, 330×202) |
| `focus-failure` | focused | The failure highlight: an overloaded Pratt truss whose top chord buckles, with red pills naming mode and utilization (BUCKLES 204 %, 261 %, 294 %) and dashed red Euler bows on the failing struts; the inspector's result and failure list, worst first, in front | `pratt-fail-light` (Pratt 24 m / 6 / 1.6 m, Forces · Light · 1080×720 · 3 · B2; crop 556×196) ; `inspector-failures-light` (same structure · Light · 1440×900 · 3 · B2; Result and Over capacity, 330×448) |
| `focus-palette` | focused | The Ctrl+K command palette searched for "toggle": six view toggles with their category and shortcut, flat and sharp over the blurred, laid-back Dark workbench it opened on | `palette-dark`, `desk-palette-dark` (Warren, palette open · Dark · 1440×900 · 2 · A4; panel 520×272, the frame without the text caret) |
| `stylized-split` (+ hero) | stylized | One workbench, Light and Dark, on one turned window cut by a single diagonal at the Warren web's angle, through ground and window alike | `desk-warren-light`, `desk-warren-dark` (Warren · Light then Dark, same session, seconds apart · 1440×900 · 2 · A) |
| `stylized-stack` | stylized | One drawing, four renderings, stacked like tracing layers and tied by dashed construction lines: Forces (Light), Utilization (Dark), Utilization (Light), Forces (Dark) on top | `layer-forces-light`, `layer-util-dark`, `layer-util-light`, `layer-forces-dark` (Warren · 1080×720 · 3 · B; canvas crop 900×355, stored 1.5x) |
| `stylized-mosaic` | stylized | The preset catalogue as a tilted wall of drawing sheets: Pratt, Howe, Warren, K truss, Fink roof and scissor roof at their defaults, Light and Dark alternating (the twelfth sheet is the Warren sample); the outer columns soften like depth of field | `tile-<preset>-light` and `-dark` (each preset at its defaults, Forces · 1080×720 · 3 · B2), `tile-warren-sample-dark` (B) (canvas crop 950×480, stored 1.25x) |
| `focus-truss-titled` | titled (**optional**) | `focus-truss` with the display name set as the app's wordmark: LOADPATH in JetBrains Mono Medium, Dark Primary #8FA0FF | as `focus-truss` |

**Strongest: `focus-truss` and `stylized-split`** (both have heroes).
- `focus-truss` is the product in one look: the Skia canvas itself, an engineering drawing that carries real member
  forces by line style, with loads, reactions, the deflected ghost and the dimension chain. It reads from across the
  room, is unmistakably a structural tool, and stays crisp at 2x (the drawing is a 3x capture shown at 0.8 of its pixels).
- `stylized-split` shows the whole workbench (structure outline, tool rail, canvas, inspector) in both themes at once,
  and its cut runs at the angle of the Warren's own web members, so the split belongs to this app rather than being a
  generic diagonal.

## Palette

Every colour comes from `Loadpath/Styles/ColorPaletteOverride.xaml` and the Warn step of the utilization ramp in
`Loadpath/App.xaml`; they are listed in `src/frames.css`.
- Light: Primary #3446D6, PrimaryVariantLight #9AA6E8, PrimaryContainer #E5E8FA, Background and Surface #F7F8FB,
  SurfaceVariant #EEF0F7, OnSurface #15171A, OnSurfaceVariant #4D535C, Outline #7C8399, OutlineVariant #AEB5CC,
  Error #C42323, Warn #93600F.
- Dark: Primary #8FA0FF, PrimaryVariantLight #4B5BB0, PrimaryContainer #1D2540, Background and Surface #10141B,
  SurfaceVariant #181D27, TertiaryContainer #1B2130, OnSurface #E6E9F0, OnSurfaceVariant #A9B1BF, Outline #68728A,
  OutlineVariant #3D465A, Error #FF6E6A, ErrorContainer #3A1C20, Warn #E3A64B.

Grounds mix two of these at a time (`color-mix()` in the compositions); grid crosses, hairlines, glows and shadows are the
same values at reduced alpha. The phone bezels are the Dark Background with a lighter slate edge.

## How it was made

1. **Build.** The `studio` branch (HEAD acb23eb, Uno.Sdk 7.0.0-dev.132) was exported with `git archive` to a scratch
   folder (the repository was not touched) and published:
   `dotnet publish Loadpath/Loadpath.csproj -f net10.0-browserwasm -c Release -p:LoadpathTargetFrameworks=net10.0-browserwasm -p:CustomBeforeMicrosoftCommonTargets=src/tools/sample-hooks.targets`.
   The Warren, cantilever and Howe roof samples and the presets load through the UI in any build, but the app's
   deterministic start states (`LOADPATH_SAMPLE`, `LOADPATH_PRESET=pratt:24,6,1.6`, in `Presentation/DebugFixtures.cs`)
   are Debug only. `sample-hooks.targets` adds the `DEBUG` symbol (and the app's own `NO_UNO_STUDIO`, so no Studio
   toolbar) to this Release build: the app's own fixtures run, nothing else changes.
2. **Capture.** `src/tools/cap.js` (Playwright, Chromium) loads the build and patches the bootstrapper's `uno-config.js`
   in flight to hand the runtime the fixture's environment variables (`{load: {...}}` steps; each load is a fresh page,
   service workers blocked). It waits for the app's own layout line and the wordmark, parks the pointer outside the page,
   and plays a session from `src/tools/sessions/`: the system colour scheme switched for Light and Dark (the app follows
   it), keys for the display modes (1 Forces, 2 Utilization), clicks for a member, the inspector sheet and the palette.
   Each shot waits until two frames 700 ms apart are identical. Clicks check that the next state actually appeared
   (a region must change) and click again if not.
   - A: 1440×900, DPR 2: Warren in both themes and modes, M12 selected.
   - A4: 1440×900, DPR 2: the palette, Dark.
   - B: 1080×720 (the app's Normal layout, canvas only), DPR 3: Warren in both themes and modes.
   - B2: 1080×720, DPR 3: the overloaded Pratt and the six presets; then 1440×900, DPR 3: M12 and the Pratt's inspector.
   - C: 390×844, DPR 2: Warren, the Howe roof and the Pratt with and without the inspector sheet.
3. **Crop.** `src/tools/crop.py` cuts `src/cap/` from those captures at their own pixels: canvases, cards and the palette
   at 2x or 3x, mosaic and stack sheets downsampled to 1.25x and 1.5x. Nothing is upsampled.
4. **Compose and render.** One HTML file per banner in `src/` (heroes are their own files), shared frames and palette in
   `src/frames.css`, JetBrains Mono Medium (the app's wordmark face, OFL) in `src/fonts/`. `src/tools/render-all.sh`
   renders them with `src/tools/render.js` (`NODE_PATH` must reach `playwright`).

## Rules kept

- **Real pixels.** Every screen, drawing, card and the palette is a capture of the running app; nothing is redrawn or
  retouched.
- **No enlarging.** At the 2x export no capture is shown above its native pixels, including the near edge of every
  perspective (the truss, utilization and failure sheets pivot on their nearest edge). Largest ratios: the palette at exactly 1.0 (520 CSS px
  from a 1040 px capture), the Pratt failure sheet 0.98, the utilization sheet 0.92, the truss 0.81, the split's near
  corner about 0.77.
- **Clean states.** Fixtures start clean (no UNSAVED mark); no spinners, scroll indicators, focus rings, hover states,
  text caret or Studio toolbar. The selected tool (V) and the open inspector button show their checked state, as the app
  always does.
- **Generic frames.** Window bars and bezels are plain CSS: no notch, camera, buttons or logo.
- **Safe area.** Key content sits inside the central 90%; the mosaic wall runs off the frame by design and dissolves
  there, never its middle columns.

## Review

Two bounded passes over the full-size finals.
- Pass 1: `focus-palette` showed the palette's last row cut mid-word (the query "view" scrolls); recaptured with
  "toggle", whose six results fit. `focus-utilization` sat high with an empty lower quarter; recentred.
  `stylized-stack`'s outer corners touched the safe-area edge; the layers were brought in.
- Pass 2: the Dark centre phone of `showcase-phones` receded between two bright Light phones; now Light in the centre
  and Dark on the flanks. The bright outer tiles of `stylized-mosaic` pulled the eye outward; the outer columns now
  soften and the falloff is stronger. `focus-failure`'s paper sheet merged into a paper ground; the ground is now
  PrimaryContainer. Also: the palette panel was taken out of the 3D rig and laid flat at its native pixels, because
  Chromium's compositor softened its text.

## Uncertain

- **The build is Release with the Debug fixtures compiled in** (above). The captured UI and structures are the app's
  own and every state is reachable through the UI (the sample buttons; Presets with span 24, panels 6, depth 1.6 for the
  overloaded Pratt), but it is not byte-for-byte the shipped Release build.
- **Sessions.** Multi-screen compositions mostly come from one session (`stylized-split`, `showcase-daynight`,
  `showcase-phones`, `stylized-stack`, `focus-failure`, `focus-palette`). `showcase-desk` pairs the desktop session (A)
  with the phone session (C), `focus-utilization` the canvas (B) with the inspector (B2), and the mosaic's Warren sheet
  comes from B among B2's presets. The app has no clock and its fixtures and solver are deterministic, so the figures
  agree everywhere (M12 87 %, 41.93 kN; the Pratt's 294 %). Only the status bar's solve time differs between captures
  (0.7 or 1.1 ms), and no banner shows two of them side by side.
- **Turned planes** are resampled once by Chromium's compositor, so text on them is about one device pixel softer than on
  the flat elements at 2x (most visible in `stylized-split`); nothing is enlarged.
- **Small text.** The showcase banners show whole screens, so their UI text is below 10 px at 1x, as in the house-style
  banners. The stack's and the mosaic's sheet text is decorative; the focused banners keep reading text at 11 px or more.
- Session B stopped after its Howe roof shots (the first readiness probe in `cap.js` waited out its 300 s limit once);
  its remaining steps were rerun as B2. `B-focus.json` is kept as given. Session A's palette frames were discarded (the
  query field did not have keyboard focus, so the typed query was lost) and its presets-flyout frames were not used; two
  later palette runs were replaced by A4.
- The titled variant uses JetBrains Mono Medium, the app's wordmark face, rather than Inter (the UI text face), and the
  app's own all-caps wordmark spelling.
- No third-party brands appear: the section names on screen (CHS 48.3×3.2, Steel S355) are engineering standard
  designations.
