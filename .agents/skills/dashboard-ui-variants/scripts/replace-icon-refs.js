// Swap Fluent icon references for Octicons across the dashboard: replace `Icons.<variant>.SizeN.Glyph`
// with `Octicons.SizeN.Glyph` for every (size, glyph) that exists in Octicons.cs (others stay Fluent),
// fix stale fully-qualified prefixes, remove now-unused `using Icons = ...` aliases, and add the
// CustomIcons import to .cs files that need it. Run gen-octicons.js first.
//
// Usage: node replace-icon-refs.js [--root src/Aspire.Dashboard]
//
// Gotchas this handles / to watch for:
// - Fully-qualified `Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.SizeN.Glyph` leaves a stale
//   `...Components.Octicons.SizeN.Glyph` prefix after the first pass; the second pass strips it.
// - A now-unused `using Icons = ...;` alias becomes an IDE0005 error (warnings are errors). Removed here.
//   But `Icons.` in a COMMENT is a false positive that keeps the alias — after building, remove any
//   remaining IDE0005-flagged alias by hand.
const fs = require("fs");
const path = require("path");

function arg(name, def) { const i = process.argv.indexOf("--" + name); return i >= 0 ? process.argv[i + 1] : def; }
const ROOT = path.resolve(arg("root", "src/Aspire.Dashboard"));

const oct = fs.readFileSync(path.join(ROOT, "Components", "CustomIcons", "Octicons.cs"), "utf8");
const available = new Set();
let curSize = null;
for (const line of oct.split("\n")) {
  const s = line.match(/internal static class Size(\d+)/); if (s) { curSize = s[1]; }
  const c = line.match(/internal sealed class (\w+) : Icon/); if (c && curSize) { available.add(`Size${curSize}.${c[1]}`); }
}

function walk(dir, acc = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!/[\\/](obj|bin)[\\/]?$/.test(p)) { walk(p, acc); } }
    else if (/\.(razor|cs)$/.test(e.name) && !/Octicons\.cs$|AspireIcons\.cs$/.test(e.name)) { acc.push(p); }
  }
  return acc;
}

const report = [];
for (const file of walk(ROOT)) {
  let text = fs.readFileSync(file, "utf8");
  const orig = text;
  let replaced = 0; const skipped = [];
  text = text.replace(/Icons\.(?:Regular|Filled|Light)\.Size(\d+)\.(\w+)/g, (full, size, glyph) => {
    if (available.has(`Size${size}.${glyph}`)) { replaced++; return `Octicons.Size${size}.${glyph}`; }
    skipped.push(`${glyph}@${size}`); return full;
  });
  // Fix stale fully-qualified prefix left when the original ref was fully qualified.
  text = text.replace(/Microsoft\.FluentUI\.AspNetCore\.Components\.Octicons\./g, "Octicons.");
  if (replaced === 0 && text === orig) { continue; }

  const remainingIcons = /\bIcons\./.test(text.replace(/using Icons = Microsoft\.FluentUI\.AspNetCore\.Components\.Icons;\s*/g, ""));
  let aliasRemoved = false, usingAdded = false;
  if (!remainingIcons) {
    const before = text;
    text = text.replace(/[ \t]*using Icons = Microsoft\.FluentUI\.AspNetCore\.Components\.Icons;\r?\n/g, "");
    aliasRemoved = text !== before;
  }
  if (file.endsWith(".cs") && !/using Aspire\.Dashboard\.Components\.CustomIcons;/.test(text) && /\bOcticons\./.test(text)) {
    text = text.replace(/(using [^\n]+;\r?\n)/, `$1using Aspire.Dashboard.Components.CustomIcons;\n`);
    usingAdded = true;
  }

  if (text !== orig) {
    fs.writeFileSync(file, text);
    report.push(`${path.relative(ROOT, file)}: replaced ${replaced}${aliasRemoved ? ", alias removed" : ""}${usingAdded ? ", using added" : ""}${skipped.length ? ", skipped " + skipped.join("/") : ""}`);
  }
}
console.log(report.join("\n"));
console.log("\nFILES CHANGED:", report.length);
