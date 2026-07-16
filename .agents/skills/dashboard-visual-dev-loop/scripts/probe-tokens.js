// Print the active UI variant and key resolved design tokens — a fast check that a token change took
// effect and that the expected variant is live.
// Usage: node probe-tokens.js --base http://localhost:PORT
const { chromium } = require('playwright');

function arg(name, def) { const i = process.argv.indexOf('--' + name); return i >= 0 ? process.argv[i + 1] : def; }
const BASE = arg('base');
if (!BASE) { console.error('--base http://localhost:PORT is required'); process.exit(1); }

(async () => {
  const b = await chromium.launch({ headless: true, ignoreHTTPSErrors: true });
  const p = await b.newPage();
  await p.goto(BASE + '/structuredlogs', { waitUntil: 'networkidle', timeout: 60000 });
  await p.waitForFunction(() => getComputedStyle(document.documentElement).getPropertyValue('--neutral-layer-1').trim() !== '', { timeout: 30000 }).catch(() => {});
  await p.waitForTimeout(1200);
  const out = await p.evaluate(() => {
    const g = n => getComputedStyle(document.documentElement).getPropertyValue(n).trim();
    return {
      variant: document.documentElement.getAttribute('data-ui-variant'),
      accent: g('--accent-fill-rest'),
      controlRadius: g('--control-corner-radius'),
      neutralLayer1: g('--neutral-layer-1'),
      neutralLayer2: g('--neutral-layer-2'),
      neutralStroke: g('--neutral-stroke-rest'),
      bodyFont: getComputedStyle(document.body).fontFamily
    };
  });
  console.log(JSON.stringify(out, null, 2));
  await b.close();
})().catch(e => { console.error(e.message); process.exit(1); });
