// Screenshot Aspire Dashboard pages in light and dark. Files are named <variant>-<page>-<mode>.png.
// Usage: node capture.js --base http://localhost:PORT [--pages resources,structuredlogs,traces,metrics,console] [--out ./shots]
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

function arg(name, def) { const i = process.argv.indexOf('--' + name); return i >= 0 ? process.argv[i + 1] : def; }
const BASE = arg('base');
const OUT = arg('out', './shots');
const PAGES = arg('pages', 'resources,structuredlogs,traces,metrics').split(',');
const URLS = { resources: '/', structuredlogs: '/structuredlogs', traces: '/traces', metrics: '/metrics', console: '/consolelogs' };

if (!BASE) { console.error('--base http://localhost:PORT is required'); process.exit(1); }
fs.mkdirSync(OUT, { recursive: true });

async function setTheme(p, mode) {
  // The dashboard toggles its own theme through app-theme.js; this drives light/dark without cookies.
  await p.evaluate(async (m) => { try { const mod = await import('/js/app-theme.js'); mod.updateTheme(m === 'dark' ? 'Dark' : 'Light'); } catch (e) {} }, mode);
  await p.waitForTimeout(900);
}

(async () => {
  const b = await chromium.launch({ headless: true, ignoreHTTPSErrors: true });
  const p = await b.newPage({ viewport: { width: 1560, height: 1000 } });
  await p.goto(BASE + '/', { waitUntil: 'networkidle', timeout: 60000 });
  const variant = await p.evaluate(() => document.documentElement.getAttribute('data-ui-variant') || 'unknown');
  for (const name of PAGES) {
    const url = URLS[name] || ('/' + name);
    await p.goto(BASE + url, { waitUntil: 'networkidle', timeout: 60000 }).catch(() => {});
    // Wait until Fluent has generated its design tokens before shooting, else colors are unstyled.
    await p.waitForFunction(() => getComputedStyle(document.documentElement).getPropertyValue('--neutral-layer-1').trim() !== '', { timeout: 30000 }).catch(() => {});
    await p.waitForTimeout(1500);
    for (const mode of ['light', 'dark']) {
      await setTheme(p, mode);
      const file = path.join(OUT, `${variant}-${name}-${mode}.png`);
      await p.screenshot({ path: file });
      console.log('saved', file);
    }
  }
  await b.close();
})().catch(e => { console.error(e.message); process.exit(1); });
