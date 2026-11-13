// Loadpath capture driver (adapted from Cargo's and QuoteCraft's src/tools/cap.js).
//
//   node cap.js <url> <outdir> <scheme> <WxH> <dpr> <steps.json>
//
// Loads the Release WebAssembly build in headless Chromium and plays a list of steps in one browser session.
// Loadpath's own Debug fixtures (Presentation/DebugFixtures.cs: LOADPATH_SAMPLE, LOADPATH_PRESET, LOADPATH_EDIT...)
// are compiled into this build by sample-hooks.targets and read from environment variables; the bootstrapper's
// uno-config.js is patched in flight to hand the runtime the variables of the current {load} step. Each run is a fresh
// browser profile: no saved settings, no autosave.
//
// steps: [{load:{LOADPATH_SAMPLE:"warren"}}   reload the app with these variables (none: the empty state)
//         {click:[x,y]}, {click:[x,y], expect:[x,y,r,g,b(,tol)]}, {click:[x,y], change:[x,y]}, {click:[x,y], area:{...}}, {probe:[x,y]}, {dbl:[x,y]}, {move:[x,y]}, {wheel:[dx,dy], at:[x,y]},
//         {key:"Control+K"}, {scheme:"dark"}, {viewport:[w,h]}, {wait:ms}, {settle:ms},
//         {shot:"name", clip:{x,y,width,height}}]
// {settle} waits until two screenshots taken 700 ms apart are identical (animations done), at most <ms>.
// {click, expect} polls that pixel (CSS px) for up to 8 s and clicks again, up to 3 times, until the colour the next
// state shows is there: at a high device pixel ratio the app can drop the first click after a selection.
// {click, change:[x,y]} does the same until that pixel differs from what it was before the click (a panel opened,
// a selection dimmed the drawing). {click, area:{x,y,width,height}} parks the pointer outside the page and waits until
// that region differs from what it showed before the click. {probe:[x,y]} logs a pixel.
const { chromium } = require('playwright');
const fs = require('fs');

async function run(url, out, scheme, size, dpr, steps, log = console.log) {
  let [w, h] = size.split('x').map(Number);
  let env = {};
  const b = await chromium.launch({ args: ['--hide-scrollbars'] });
  const ctx = await b.newContext({ viewport: { width: w, height: h }, deviceScaleFactor: Number(dpr), colorScheme: scheme, serviceWorkers: 'block' });
  const p = await ctx.newPage();
  const errors = [];
  p.on('pageerror', e => errors.push(String(e)));
  p.on('console', m => { if (/VERIFY|fixture/i.test(m.text())) log('app:', m.text()); });
  await p.route(/uno-config\.js(\?.*)?$/, async route => {
    const res = await route.fetch();
    let body = await res.text();
    const anchor = 'config.environmentVariables = config.environmentVariables || {};';
    if (!body.includes(anchor)) log('WARNING: uno-config.js has no environmentVariables block');
    const extra = Object.entries(env).map(([k, v]) => `config.environmentVariables[${JSON.stringify(k)}] = ${JSON.stringify(v)};`).join('\n');
    body = body.replace(anchor, anchor + '\n' + extra);
    await route.fulfill({ response: res, body, headers: { ...res.headers(), 'cache-control': 'no-store' } });
  });

  const pixel = async (x, y) => {
    const png = await p.screenshot({ clip: { x, y, width: 1, height: 1 }, scale: 'css' });
    return p.evaluate(async b64 => {
      const img = new Image(); img.src = 'data:image/png;base64,' + b64; await img.decode();
      const c = new OffscreenCanvas(1, 1).getContext('2d'); c.drawImage(img, 0, 0); return [...c.getImageData(0, 0, 1, 1).data];
    }, png.toString('base64'));
  };

  let laidOut = false;
  p.on('console', m => { if (/VERIFY layout=/.test(m.text())) laidOut = true; });
  const load = async vars => {
    env = vars || {};
    laidOut = false;
    log('load', JSON.stringify(env));
    await p.goto(url, { waitUntil: 'load', timeout: 180000 });
    const t0 = Date.now();
    // The app prints "VERIFY layout=..." once its page is built (MainPage, in this build); the splash leaves
    // the title strip blank, so the wordmark's pixels in its first 160 x 56 px follow.
    while (Date.now() - t0 < 300000) {
      let ink = 0;
      if (laidOut) {
        try {
          const strip = await p.screenshot({ clip: { x: 0, y: 0, width: 160, height: 56 }, scale: 'css', timeout: 120000 });
          ink = await p.evaluate(async b64 => {
            const img = new Image(); img.src = 'data:image/png;base64,' + b64; await img.decode();
            const c = new OffscreenCanvas(img.width, img.height).getContext('2d'); c.drawImage(img, 0, 0);
            const d = c.getImageData(0, 0, img.width, img.height).data; let n = 0;
            for (let i = 0; i < d.length; i += 4) if (Math.abs(d[i] - d[0]) + Math.abs(d[i + 1] - d[1]) + Math.abs(d[i + 2] - d[2]) > 60) n++;
            return n;
          }, strip.toString('base64'));
        } catch (e) { log('probe timeout'); }
      }
      if (ink > 40) break;
      await p.waitForTimeout(1000);
    }
    log('rendered after', Math.round((Date.now() - t0) / 1000), 's');
    await p.mouse.move(-20, -20);
  };

  const api = {
    page: p,
    async step(s) {
      if (s.load !== undefined) await load(s.load);
      if (s.viewport) { [w, h] = s.viewport; await p.setViewportSize({ width: w, height: h }); }
      if (s.click) {
        const press = async () => { await p.mouse.move(...s.click); await p.mouse.down(); await p.waitForTimeout(90); await p.mouse.up(); };
        const before = s.change ? await pixel(...s.change) : null;
        const area = async () => p.screenshot({ clip: s.area, scale: 'css' });
        const beforeArea = s.area ? await area() : null;
        await press();
        if (s.area) {
          let ok = false;
          for (let attempt = 1; attempt <= 3 && !ok; attempt++) {
            if (attempt > 1) { log('  no change in', JSON.stringify(s.area), 'after', JSON.stringify(s.click), '- clicking again'); await press(); }
            await p.mouse.move(-20, -20);
            for (const t0 = Date.now(); Date.now() - t0 < 8000;) { if (!(await area()).equals(beforeArea)) { ok = true; break; } await p.waitForTimeout(400); }
          }
          log(ok ? '  state changed after' : 'WARNING: no state change after', JSON.stringify(s.click));
        }
        if (s.expect || s.change) {
          const [ex, ey, ...rgb] = s.expect || s.change;
          const tol = s.expect ? (rgb[3] ?? 24) : 0;
          const differs = px => before.slice(0, 3).some((v, i) => Math.abs(v - px[i]) > 12);
          const matches = async () => { const px = await pixel(ex, ey); return s.expect ? rgb.slice(0, 3).every((v, i) => Math.abs(v - px[i]) <= tol) : differs(px); };
          let ok = false;
          for (let attempt = 1; attempt <= 3 && !ok; attempt++) {
            if (attempt > 1) { log('  no state change after', JSON.stringify(s.click), '- clicking again'); await press(); }
            for (const t0 = Date.now(); Date.now() - t0 < 8000;) { if (await matches()) { ok = true; break; } await p.waitForTimeout(400); }
          }
          if (!ok) log('WARNING: expected state not reached after', JSON.stringify(s.click), 'pixel', JSON.stringify(await pixel(ex, ey)));
        }
      }
      if (s.dbl) await p.mouse.dblclick(...s.dbl);
      if (s.move) await p.mouse.move(...s.move);
      if (s.wheel) { await p.mouse.move(...(s.at || [w / 2, h / 2])); await p.mouse.wheel(...s.wheel); }
      if (s.key) await p.keyboard.press(s.key);
      if (s.type) await p.keyboard.type(s.type, { delay: 40 });
      if (s.scheme) await p.emulateMedia({ colorScheme: s.scheme });
      if (s.wait) await p.waitForTimeout(s.wait);
      if (s.settle) {
        const until = Date.now() + s.settle;
        let prev = await p.screenshot({ scale: 'css' });
        while (Date.now() < until) {
          await p.waitForTimeout(700);
          const next = await p.screenshot({ scale: 'css' });
          if (next.equals(prev)) break;
          prev = next;
        }
      }
      if (s.probe) log('pixel', JSON.stringify(s.probe), JSON.stringify(await pixel(...s.probe)));
      if (s.shot) {
        const t = Date.now();
        await p.screenshot({ path: `${out}/${s.shot}.png`, clip: s.clip, animations: 'disabled', timeout: 180000 });
        log('shot', s.shot, (Date.now() - t) + 'ms');
      }
    },
    errors,
    close: () => b.close(),
  };
  for (const s of steps) await api.step(s);
  return api;
}

module.exports = { run };

if (require.main === module) {
  (async () => {
    const [url, out, scheme, size, dpr, stepsFile] = process.argv.slice(2);
    const steps = JSON.parse(fs.readFileSync(stepsFile, 'utf8'));
    const api = await run(url, out, scheme, size, dpr, steps);
    if (api.errors.length) console.log('errors', api.errors.slice(0, 5));
    await api.close();
  })();
}
