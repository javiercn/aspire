// Generate telemetry by reading the dashboard's resource URLs and hitting their endpoints server-side,
// then report whether traces appeared. App ports are dynamic, so never hardcode them.
// Usage: node drive-traffic.js --base http://localhost:PORT [--rounds 10]
const { chromium } = require('playwright');

function arg(name, def) { const i = process.argv.indexOf('--' + name); return i >= 0 ? process.argv[i + 1] : def; }
const BASE = arg('base');
const ROUNDS = parseInt(arg('rounds', '10'), 10);
if (!BASE) { console.error('--base http://localhost:PORT is required'); process.exit(1); }

(async () => {
  const b = await chromium.launch({ headless: true, ignoreHTTPSErrors: true });
  const ctx = await b.newContext({ ignoreHTTPSErrors: true });
  const p = await ctx.newPage();
  await p.goto(BASE + '/', { waitUntil: 'networkidle', timeout: 60000 });
  await p.waitForTimeout(4000);
  const urls = await p.evaluate(() =>
    Array.from(document.querySelectorAll('.fluent-data-grid-row a[href]')).map(a => a.href).filter(h => /^https?:\/\/localhost/.test(h)));
  const origins = [...new Set(urls.map(u => { try { return new URL(u).origin; } catch (e) { return null; } }).filter(Boolean))];
  console.log('resource origins:', origins.join(', ') || '(none — is the apphost running with resources?)');
  // Common ASP.NET Core sample endpoints; harmless 404s are fine, each request still creates a server span.
  const paths = ['/weatherforecast', '/currenttime', '/', '/fetchdata'];
  for (let r = 0; r < ROUNDS; r++) {
    for (const o of origins) { for (const pp of paths) { try { await p.request.get(o + pp, { timeout: 4000 }); } catch (e) {} } }
  }
  await p.waitForTimeout(5000);
  await p.goto(BASE + '/traces', { waitUntil: 'networkidle', timeout: 60000 });
  await p.waitForTimeout(3000);
  const empty = await p.evaluate(() => document.body.innerText.includes('No traces found'));
  console.log(empty ? '0 traces — retry; the apps may still be starting or on different ports than a prior run' : 'traces present');
  await b.close();
})().catch(e => { console.error(e.message); process.exit(1); });
