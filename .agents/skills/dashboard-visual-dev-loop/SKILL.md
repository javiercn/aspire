---
name: dashboard-visual-dev-loop
description: Runs and visually validates the Aspire Dashboard (src/Aspire.Dashboard) locally, then captures screenshots with Playwright. USE FOR seeing a dashboard styling change, comparing the Fluent and GitHub UI variants, reproducing a UI state, generating logs/traces/metrics with real telemetry, or screenshotting pages and hover/focus states in light and dark. It handles the recurring friction — the dll-lock rebuild failure, launching standalone vs the BlazorHosted apphost, dynamic resource ports, telemetry timing, forcing interaction states on virtualized grids, and toggling the variant. DO NOT USE FOR deciding where a styling change belongs in the code (use dashboard-ui-variants) or for non-dashboard apps.
---

# Dashboard visual dev loop

Build, run, and screenshot the Aspire Dashboard to see UI changes and compare the two variants.

`scripts/` holds the reusable harness (Node + Playwright). One-time setup:

```bash
cd .agents/skills/dashboard-visual-dev-loop/scripts && npm install
```

## Build

Use the repo-local SDK (run `./restore.cmd` / `./restore.sh` once so `.dotnet/` exists), then:

```bash
dotnet build src/Aspire.Dashboard/Aspire.Dashboard.csproj -c Debug
```

**CSS and JS under `wwwroot/` are static assets** — edits are served on page reload with **no rebuild**. Only `.razor` / `.cs` changes need a build.

### The dll-lock failure

If a previous dashboard is still running, the build fails to copy the output:

```
error MSB3021: ... Aspire.Dashboard.dll ... being used by another process. The file is locked by: "…(<PID>)"
```

The C# already compiled — only the copy failed. Kill the locking process and rebuild. The error line names the PID; or find it by the loaded dll:

```powershell
$dll = (Resolve-Path "artifacts/bin/Aspire.Dashboard/Debug/net8.0/Aspire.Dashboard.dll").Path
Get-Process dotnet -ErrorAction SilentlyContinue |
  Where-Object { try { $_.Modules.FileName -contains $dll } catch { $false } } |
  Stop-Process -Force
```

Always stop the dashboard/apphost you launched before rebuilding.

## Launch — two modes

### Standalone (fast, chrome only, empty data)

Best for validating chrome, tokens, tabs, inputs, nav, typography — anything that doesn't need rows.

```powershell
$env:DASHBOARD__FRONTEND__AUTHMODE="Unsecured"; $env:ASPNETCORE_ENVIRONMENT="Development"
$env:ASPNETCORE_URLS="http://localhost:15890"
dotnet run --project src/Aspire.Dashboard/Aspire.Dashboard.csproj -c Debug --no-build --no-launch-profile
```

(bash: set the same names inline, e.g. `DASHBOARD__FRONTEND__AUTHMODE=Unsecured ASPNETCORE_URLS=http://localhost:15890 dotnet run …`.)

### BlazorHosted apphost (real logs, traces, metrics, resources)

Best for validating grids, pills, and telemetry views. It launches **your built dashboard** via `ASPIRE_DASHBOARD_PATH`:

```powershell
$env:DOTNET_ROOT="$PWD/.dotnet"
$env:ASPIRE_DASHBOARD_PATH="$PWD/artifacts/bin/Aspire.Dashboard/Debug/net8.0/Aspire.Dashboard.dll"
$env:DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS="true"; $env:ASPIRE_ALLOW_UNSECURED_TRANSPORT="true"
dotnet artifacts/bin/BlazorHosted.AppHost/Debug/net8.0/BlazorHosted.AppHost.dll
```

(bash: `export` the same names.) Run the apphost DLL **directly** (not `dotnet run` on the project) — `dotnet run` on the AppHost can delegate to the `aspire` CLI and launch a *packaged* dashboard instead of your build. Build the apphost first: `dotnet build playground/BlazorHosted/BlazorHosted.AppHost/BlazorHosted.AppHost.csproj -c Debug`.

### Pick the variant

Default launch is the GitHub look. For the classic Fluent look, set `ASPIRE_DASHBOARD_UI_VARIANT=fluent` before launching. Confirm which is live by reading `data-ui-variant` on `<html>` in the served page.

## Get the dashboard URL and app ports

The dashboard URL is in the launch log: `Dashboard:  http://localhost:<port>`. Wait for that line, then confirm the page is up (`Invoke-WebRequest http://localhost:<port>/` returns 200, or `curl -sf`) before capturing.

**Do not assume the app (weatherapi/timeapi/blazorapp) ports** — they are dynamic. Read the real resource URLs from the dashboard's Resources page (the `<a href>` links in the grid). `scripts/drive-traffic.js` does this for you.

## Generate telemetry (for logs/traces/metrics)

Traces/logs only appear after the apps handle requests. Hit the resource endpoints server-side; browser-driven page loads alone are unreliable. Run:

```bash
node scripts/drive-traffic.js --base http://localhost:<port>
```

It reads the resource origins from the dashboard, hits them repeatedly, and reports whether traces appeared. It should print `traces present`. If it prints `0 traces`, the apps may still be starting or on different ports than a previous run — re-run it.

## Capture screenshots

`scripts/capture.js` navigates a dashboard URL, toggles theme via the app's own module, and screenshots each page in light and dark:

```bash
node scripts/capture.js --base http://localhost:<port> --pages resources,structuredlogs,traces,metrics --out ./shots
```

It switches theme with `import('/js/app-theme.js').then(m => m.updateTheme('Dark'|'Light'))` and waits for `--neutral-layer-1` to resolve before shooting.

### Compare both variants

Launch the dashboard twice (once default, once with `ASPIRE_DASHBOARD_UI_VARIANT=fluent`) on different ports and capture each with `capture.js`. GitHub shows Octicons/blue/pills/segmented-tabs; Fluent shows Fluent icons/purple/underline-inputs/no-pills.

### Verify tokens

`scripts/probe-tokens.js` prints the active variant and the resolved key tokens (accent, control radius, neutral layers, body font) — a fast check that a token change took effect:

```bash
node scripts/probe-tokens.js --base http://localhost:<port>
```

## Force interaction states

Grid rows are virtualized, so `element.hover()` often fails ("element is outside of the viewport"). To screenshot hover/focus reliably, force the pseudo-state over CDP with Playwright's `page.context().newCDPSession(page)` → `CSS.forcePseudoState({ nodeId, forcedPseudoClasses: ['hover'] })`, or inject a temporary style. When a grid is empty (no telemetry), inject sample rows that mirror the real markup (same classes, e.g. `.fluent-data-grid-row`, `.resource-pill`, `.trace-service-tag`) to preview row styling.

## Verify (completion criteria)

1. The intended page rendered — inspect the saved PNG, don't assume.
2. For a token change, `probe-tokens.js` shows the new value.
3. For a variant comparison, both PNG sets exist and differ in the expected way (icons, accent, pills).
4. Stop every dashboard/apphost you launched when done (they hold the dll lock and ports).
