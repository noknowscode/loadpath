// node render.js <html> <out.png> <W> <H> <scale> [...more jobs]: renders compositions at an exact CSS size and device
// scale (Playwright, Chromium). Each job gets a fresh page and is retried once if Chromium fails to capture.
const { chromium } = require('playwright');
(async () => {
  const jobs = [];
  const a = process.argv.slice(2);
  for (let i = 0; i < a.length; i += 5) jobs.push(a.slice(i, i + 5));
  let b = await chromium.launch();
  for (const [html, out, W, H, s] of jobs) {
    for (let attempt = 1; ; attempt++) {
      const ctx = await b.newContext({ viewport: { width: +W, height: +H }, deviceScaleFactor: +s });
      try {
        const p = await ctx.newPage();
        await p.goto('file://' + require('path').resolve(html));
        await p.evaluate(() => document.fonts.ready);
        await p.waitForFunction(() => [...document.images].every(i => i.complete && i.naturalWidth > 0), null, { timeout: 30000 });
        await p.waitForTimeout(300);
        await p.screenshot({ path: out, clip: { x: 0, y: 0, width: +W, height: +H } });
        console.log('rendered', out);
        break;
      } catch (e) {
        if (attempt >= 2) throw e;
        console.log('retrying', out, String(e).split('\n')[0]);
        await b.close(); b = await chromium.launch();
      } finally {
        await ctx.close().catch(() => {});
      }
    }
  }
  await b.close();
})();
