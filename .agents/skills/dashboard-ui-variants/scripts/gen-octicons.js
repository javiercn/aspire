// Regenerate Octicons.cs for the Aspire Dashboard. For every (Fluent glyph, size) the dashboard uses,
// fetch the mapped Octicon from primer/octicons, strip its fill, scale it into the target viewBox with a
// transform, and emit a dual-content Icon class: the Octicon path when ThemeState.UseGitHubUI, otherwise
// the same-named Fluent Regular glyph. Glyphs with no Octicon equivalent are reported and stay Fluent.
//
// Usage: node gen-octicons.js [--out src/Aspire.Dashboard/Components/CustomIcons/Octicons.cs]
// To add or remap an icon, edit MAP (and USAGE for a new glyph/size) below, then re-run.
const https = require("https");
const fs = require("fs");

function arg(name, def) { const i = process.argv.indexOf("--" + name); return i >= 0 ? process.argv[i + 1] : def; }
const OUT = arg("out", "src/Aspire.Dashboard/Components/CustomIcons/Octicons.cs");

// Fluent glyph -> Octicon name. Best-effort visual equivalents. Omit a glyph to keep it Fluent-only.
const MAP = {
  Add: "plus", Alert: "bell", AppGeneric: "package", AppsList: "apps", AppFolder: "apps",
  ArrowCircleDown: "arrow-down", ArrowCircleRight: "arrow-right", ArrowCircleUp: "arrow-up",
  ArrowCollapseAll: "fold", ArrowDown: "arrow-down", ArrowDownload: "download", ArrowExpand: "screen-full",
  ArrowExpandAll: "unfold", ArrowHookUpLeft: "reply", ArrowReset: "sync", ArrowUp: "arrow-up",
  ArrowUpload: "upload", Book: "book", BotSparkle: "copilot", Box: "package", Braces: "code",
  Calendar: "calendar", CalendarClock: "calendar", CalendarLtr: "calendar", ChartMultiple: "graph",
  Chat: "comment-discussion", CheckboxIndeterminate: "dash", Checkmark: "check", CheckmarkCircle: "check-circle",
  CheckmarkCircleWarning: "check-circle", ChevronDown: "chevron-down", ChevronRight: "chevron-right",
  Circle: "circle", CircleHint: "dot", Code: "code", CodeCircle: "code-square", ContentSettings: "sliders",
  ContentView: "list-unordered", Copy: "copy", DataArea: "graph", Database: "database", Delete: "trash",
  Dismiss: "x", DismissCircle: "x-circle", Document: "file", DocumentError: "file", DocumentHeader: "file",
  DocumentOnePage: "file", DocumentText: "file", ErrorCircle: "x-circle", Eye: "eye", EyeOff: "eye-closed",
  Filter: "filter", Flash: "zap", FolderOpen: "file-directory-open-fill", GanttChart: "workflow",
  Heart: "heart", HeartBroken: "heart", Info: "info", Key: "key", Laptop: "device-desktop", Link: "link",
  LinkMultiple: "link", LockClosed: "lock", Mail: "mail", Mailbox: "inbox", MoreHorizontal: "kebab-horizontal",
  Open: "link-external", Options: "sliders", Person: "person", Pin: "pin", PreviewLink: "link-external",
  QuestionCircle: "question", RecordStop: "stop", Scales: "law", SelectAllOn: "checklist", Server: "server",
  Settings: "gear", ShareAndroid: "share-android", SlideSearch: "search", SlideText: "terminal",
  SlideTextSparkle: "list-unordered", Sparkle: "north-star", SplitHorizontal: "rows", SplitVertical: "columns",
  Stack: "stack", Subtract: "dash", Table: "table", Toolbox: "tools", Warning: "alert", Wrench: "tools",
  ZoomIn: "plus-circle", ZoomOut: "screen-normal", Navigation: "three-bars", Play: "play",
  CheckboxChecked: "check-circle-fill", CheckboxUnchecked: "circle", CloudError: "cloud-offline"
};
// Glyph -> sizes actually used in the dashboard. A class is generated per (glyph, size).
const USAGE = {
  Add: [16], Alert: [24], AppGeneric: [24], AppsList: [20], ArrowCircleDown: [16], ArrowCircleRight: [16], ArrowCircleUp: [16],
  ArrowCollapseAll: [16], ArrowDown: [16], ArrowDownload: [16, 24], ArrowExpand: [16], ArrowExpandAll: [16], ArrowHookUpLeft: [16],
  ArrowReset: [24], ArrowUp: [16], ArrowUpload: [16, 24], Book: [16], BotSparkle: [24], Box: [16], Braces: [16], Calendar: [16],
  CalendarClock: [16], CalendarLtr: [24], ChartMultiple: [16, 24], Chat: [24], CheckboxChecked: [16, 20], CheckboxIndeterminate: [20],
  CheckboxUnchecked: [16, 20], Checkmark: [16], CheckmarkCircle: [16, 20, 24], CheckmarkCircleWarning: [16], ChevronDown: [12, 24],
  ChevronRight: [12], Circle: [16], CircleHint: [16], Code: [16, 24], CodeCircle: [20], ContentSettings: [16], ContentView: [16],
  Copy: [16], DataArea: [24], Database: [16], Delete: [16], Dismiss: [16, 24], DismissCircle: [16, 20, 24], Document: [16], DocumentError: [16],
  DocumentHeader: [16], DocumentOnePage: [16], DocumentText: [16], ErrorCircle: [12, 16], Eye: [16], EyeOff: [16], Filter: [16, 20],
  Flash: [16, 24], FolderOpen: [16], GanttChart: [16, 24], Heart: [16], HeartBroken: [16], Info: [16, 20, 24], Key: [24], Laptop: [16],
  Link: [16], LinkMultiple: [16], LockClosed: [16], Mail: [16], Mailbox: [16], MoreHorizontal: [16, 20], Open: [16], Options: [20],
  Person: [16, 24], Pin: [16], PreviewLink: [24], QuestionCircle: [16, 24], RecordStop: [16], Scales: [24], SelectAllOn: [16], Server: [16],
  Settings: [16, 24], ShareAndroid: [24], SlideSearch: [16, 24], SlideText: [16, 24], SlideTextSparkle: [16, 24], Sparkle: [16, 20, 24],
  SplitHorizontal: [16], SplitVertical: [16], Stack: [16], Subtract: [16], Table: [24], Toolbox: [16], Warning: [16, 20, 24], Wrench: [16, 24],
  ZoomIn: [24], ZoomOut: [24], Navigation: [24], AppFolder: [24], Play: [16], CloudError: [16]
};
const OCTI = "https://raw.githubusercontent.com/primer/octicons/main/icons";
function get(url) { return new Promise((res) => { https.get(url, r => { if (r.statusCode !== 200) { r.resume(); return res(null); } let d = ""; r.on("data", c => d += c); r.on("end", () => res(d)); }).on("error", () => res(null)); }); }
function inner(svg) { let m = svg.replace(/^[\s\S]*?<svg[^>]*>/, "").replace(/<\/svg>\s*$/, "").trim(); m = m.replace(/\s+fill="[^"]*"/g, ""); return m.replace(/\s+/g, " ").trim(); }
const cache = {};
async function octiconInner(name, native) { const k = name + "-" + native; if (k in cache) { return cache[k]; } const svg = await get(`${OCTI}/${name}-${native}.svg`); cache[k] = svg ? inner(svg) : null; return cache[k]; }

(async () => {
  const bySize = { 12: [], 16: [], 20: [], 24: [] };
  const missing = [];
  for (const glyph of Object.keys(USAGE).sort()) {
    const oct = MAP[glyph];
    if (!oct) { missing.push(glyph + " (no mapping)"); continue; }
    for (const size of USAGE[glyph]) {
      let native = size <= 16 ? 16 : 24;
      let content = await octiconInner(oct, native);
      if (!content) { native = native === 16 ? 24 : 16; content = await octiconInner(oct, native); }
      if (!content) { missing.push(`${glyph} -> ${oct} (octicon missing)`); continue; }
      const scale = size / native;
      const esc = scale === 1 ? content.replace(/"/g, '""') : `<g transform=""scale(${scale.toFixed(6)})"">${content.replace(/"/g, '""')}</g>`;
      const fluent = `new global::Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size${size}.${glyph}().Content`;
      bySize[size].push(`        internal sealed class ${glyph} : Icon { public ${glyph}() : base("${glyph}", IconVariant.Regular, IconSize.Size${size}, global::Aspire.Dashboard.Model.ThemeState.UseGitHubUI ? @"${esc}" : ${fluent}) { } }`);
    }
  }
  const sizeBlock = (n) => bySize[n].length ? `    internal static class Size${n}\n    {\n${bySize[n].sort().join("\n")}\n    }\n` : "";
  const file = `// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.FluentUI.AspNetCore.Components;

namespace Aspire.Dashboard.Components.CustomIcons;

// GitHub Octicons (https://github.com/primer/octicons, MIT license) exposed as Fluent UI Icon
// instances. Each class returns the Octicon glyph when ThemeState.UseGitHubUI, otherwise the
// same-named Fluent System Icon (Regular variant), so call sites are variant-agnostic. Path data is
// the inner SVG of the matching octicon (fill stripped so it inherits the icon color), scaled with a
// transform when the octicon's native size differs from the target. Generated by scripts/gen-octicons.js.
internal static class Octicons
{
${[12, 16, 20, 24].map(sizeBlock).filter(Boolean).join("\n")}}
`;
  fs.writeFileSync(OUT, file);
  const total = [12, 16, 20, 24].reduce((a, n) => a + bySize[n].length, 0);
  console.log(`generated ${total} classes -> ${OUT}`);
  console.log("left as Fluent (no octicon):", missing.length ? missing.join(", ") : "none");
})();
