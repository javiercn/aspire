---
name: dashboard-ui-variants
description: Maintains the Aspire Dashboard's two UI variants (classic Fluent and GitHub/Primer looks) that share one build and one switch (ASPIRE_DASHBOARD_UI_VARIANT env var, surfaced as a data-ui-variant attribute). USE FOR changing dashboard styling, adding or restyling a component in src/Aspire.Dashboard so it works in both variants, tuning the GitHub look, adding/remapping an icon, or rebasing the overlay after a Microsoft.FluentUI.AspNetCore.Components package bump. It explains where each change belongs (design-token seed vs gated CSS vs icon), the cascade-layer and gating invariants that keep the overlay resilient to Fluent changes, and how to verify both variants. DO NOT USE FOR building/running the dashboard or capturing screenshots (use dashboard-visual-dev-loop), or for Blazor styling outside src/Aspire.Dashboard.
---

# Dashboard UI variants

The Aspire Dashboard renders two looks from one codebase and one build:

- **Fluent** — the classic look (Fluent System Icons, .NET-purple accent, 4px radii, Segoe UI).
- **GitHub** — a GitHub/Primer look (Octicons, blue accent, 6px radii, Primer typography, label pills, segmented tabs, box inputs).

The variant is chosen at launch by `ASPIRE_DASHBOARD_UI_VARIANT` (`github` is the default; `fluent` switches back). `Model/ThemeState.cs` reads it once and exposes `Variant` and `UseGitHubUI`. `Components/App.razor` renders `<html data-ui-variant="@ThemeState.Variant">`.

Guiding rule: **reconfigure Fluent's tokens, don't fight its CSS.** Most of the look is a reseed of Fluent (FAST) design tokens through the public API; CSS is the thin last resort. This is why the redesign survives Fluent updates.

## Where a change belongs — decide before editing

Work top-down. Reach for a lower row only when the higher one cannot express the change.

| The change is about… | Put it in | Mechanism |
|---|---|---|
| Color, accent, corner radius, font, dark surface darkness | `wwwroot/js/app-theme.js` | A **design-token seed** (FAST regenerates all derived tokens) |
| A component's shape Fluent tokens can't express (pills, segmented tabs, box inputs, NavList rows, muted headers, hover/focus) | `wwwroot/css/github-theme.css` | A **gated CSS rule** targeting a `::part()` or a semantic class |
| Which glyph an icon shows | `Components/CustomIcons/Octicons.cs` | An **icon mapping** (regenerated; see below) |

## Invariants (these keep the overlay resilient — never break them)

1. **Gate every visual rule** in `github-theme.css` under `[data-ui-variant="github"]`. The only ungated block is `@font-face`. In the Fluent variant the overlay must match nothing so base styles fall through.
2. **The overlay wins by cascade layer, not specificity.** `wwwroot/css/aspire-layers.css` declares `@layer aspire-base, aspire-github;` and `@import`s every sheet into those layers, with `github-theme.css` in `aspire-github` (declared last). Later layers beat earlier ones regardless of specificity, so **never add `!important`** to a new overlay rule — if a rule "needs" it, it belongs in the `aspire-github` layer already and the real problem is elsewhere.
3. **Prefer `::part()` and custom-property indirection.** Style documented Fluent shadow parts (`::part(root)`, `::part(control)`, `::part(tablist)`, `::part(activeIndicator)`) and route values through bridge vars (`--gh-*`, `--resource-color`). A value change should be one line in a `[data-ui-variant="github"] { … }` block.
4. **Never target Fluent's scope hashes** (`b-xxxxx` attributes). They change every build. Target stable classes (`.fluent-data-grid`, `.trace-service-tag`, `.state-column-cell`, `.log-row-<level>`), element names (`fluent-tabs`, `fluent-search`), or `::part()`.

## Add or change a design token (highest leverage)

Edit `wwwroot/js/app-theme.js`. It branches on `useGitHubVariant()` (reads the `data-ui-variant` attribute; anything but `fluent` is GitHub). Keep both branches:

| Seed | GitHub | Fluent |
|---|---|---|
| accent base (`setAccentColor`) | `#0969DA` | `#512BD4` |
| neutral base (`setNeutralColor`, via `updateNeutralBaseColor`) | dark `#818b98`, light `#808080` | `#808080` both |
| `controlCornerRadius` / `layerCornerRadius` | `6` / `8` | `4` / `8` |
| `bodyFont` | Primer system stack (`-apple-system, BlinkMacSystemFont, "Segoe UI", "Noto Sans", Helvetica, Arial, sans-serif`) | `"Segoe UI Variable", "Segoe UI", sans-serif` |
| dark `baseLayerLuminance` | `0.15` | `0.19` |

`app-theme.js` is a static asset: changes are served on page reload with no rebuild.

Verify a seed change with the `dashboard-visual-dev-loop` skill's `probe-tokens.js`, which prints the resolved accent, control radius, neutral layers, and body font for the running dashboard.

## Add or change a gated CSS rule

Add the rule to `wwwroot/css/github-theme.css`, scoped under `[data-ui-variant="github"]`, targeting a `::part()` or stable class. Example shape:

```css
[data-ui-variant="github"] fluent-search::part(root) {
    border: 1px solid var(--neutral-stroke-rest);
    border-radius: 6px;
}
```

For a resource-colored pill, the color arrives as the `--resource-color` custom property on the element (set in the `.razor` markup); build the pill from it and mix against the theme neutrals so every one of the ~20 resource colors stays readable in light and dark:

```css
[data-ui-variant="github"] .resource-pill {
    background: color-mix(in srgb, var(--resource-color) 18%, var(--neutral-layer-1));
    color: color-mix(in srgb, var(--resource-color) 45%, var(--neutral-foreground-rest));
    border: 1px solid color-mix(in srgb, var(--resource-color) 45%, transparent);
    border-radius: 999px;
}
```

`github-theme.css` and `aspire-layers.css` are static assets — served on reload, no rebuild.

## Add or remap an icon

Icons are baked into markup at ~76 call sites as `new Octicons.SizeN.Glyph()`. Each `Octicons` class returns the Octicon glyph when `ThemeState.UseGitHubUI`, otherwise the same-named Fluent glyph, so the variant flips without touching call sites:

```csharp
internal sealed class AppFolder : Icon
{
    public AppFolder() : base("AppFolder", IconVariant.Regular, IconSize.Size24,
        global::Aspire.Dashboard.Model.ThemeState.UseGitHubUI
            ? @"<path d=""…octicon…"" />"
            : new global::Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size24.AppFolder().Content) { }
}
```

Rules:
- The class name **must** match a real Fluent glyph at that size in the `Regular` variant (that is the fallback).
- A new `FluentIcon` reference in a component uses `Octicons.SizeN.Glyph` (not `Icons.…`). If that glyph/size isn't in `Octicons.cs` yet, add it.
- These five have **no Octicon equivalent** and correctly stay Fluent in both variants: `Pause`, `PauseOff`, `TextWrap`, `TextWrapOff`, `ArrowTurnDownRight`.
- `Octicons.cs` is generated. To regenerate it or add a mapping, edit `MAP` (and `USAGE` for a new glyph/size) in `scripts/gen-octicons.js`, then from the repo root run `node .agents/skills/dashboard-ui-variants/scripts/gen-octicons.js` — it writes `src/Aspire.Dashboard/Components/CustomIcons/Octicons.cs` and prints the class count plus any glyph with no Octicon. To point call sites at Octicons, run `node .agents/skills/dashboard-ui-variants/scripts/replace-icon-refs.js` — it swaps `Icons.<variant>.SizeN.Glyph` → `Octicons.SizeN.Glyph` for generated pairs, fixes stale fully-qualified prefixes, and removes now-unused `using Icons = …` aliases. Then build; if an IDE0005 unused-`using Icons` error remains, an `Icons.` inside a comment kept the alias — delete that alias by hand.

## New component checklist

When you add or restyle a dashboard component:

1. Does it render an icon? Use `Octicons.SizeN.Glyph` and confirm the class exists in `Octicons.cs`.
2. Does it need GitHub styling beyond tokens? Add a gated rule (invariant 1) to `github-theme.css`.
3. Does it show resource identity (a colored bar)? Give the span `class="resource-pill"` and `style="--resource-color: @(ColorGenerator.Instance.GetColorVariableByKey(...))"` instead of an inline `border-left-color`, then style `.resource-pill` in CSS.
4. Verify **both** variants render (see Verify).

## Rebase after a Fluent package bump

When `Microsoft.FluentUI.AspNetCore.Components` is updated:

1. Build. If `Octicons.cs` fails, a Fluent glyph the fallback references was renamed/removed — fix that class's fallback to an existing `Regular` glyph.
2. Run **both** variants in light and dark (see the `dashboard-visual-dev-loop` skill) and screenshot-compare against the prior look.
3. Check the small set of Fluent shadow-part names the overlay depends on still resolve: `::part(root)`, `::part(control)`, `::part(tablist)`, `::part(activeIndicator)`, plus `--fluent-data-grid-resize-handle-color`. If a part was renamed, the overlay rule for it is the only thing that breaks — fix it in `github-theme.css`.
4. Confirm the `@import` targets in `aspire-layers.css` still resolve (the reboot path and `Aspire.Dashboard.styles.css`).

## File map

| Concern | File |
|---|---|
| Variant source of truth (`Variant`, `UseGitHubUI`) | `Model/ThemeState.cs` |
| `<html data-ui-variant>`, stylesheet entry, anti-flash bg | `Components/App.razor` |
| Design-token reseed (both variants) | `wwwroot/js/app-theme.js` |
| Cascade-layer entry (`@layer` + `@import layer()`) | `wwwroot/css/aspire-layers.css` |
| GitHub CSS overlay (gated) | `wwwroot/css/github-theme.css` |
| Octicons + Fluent fallback | `Components/CustomIcons/Octicons.cs` |
| Mona Sans font (+ OFL) | `wwwroot/fonts/` |

## Verify (completion criteria — do not stop before these pass)

1. **Build is clean:** `dotnet build src/Aspire.Dashboard/Aspire.Dashboard.csproj -c Debug` → 0 errors, 0 warnings (IDE0005 unused-using counts as an error here).
2. **Every new overlay rule is gated:** it appears under `[data-ui-variant="github"]` and is not `@font-face`. Grep the added selectors to confirm the prefix is present.
3. **No new `!important`** was added to `github-theme.css` (grep the diff).
4. **Both variants render correctly** in light and dark — GitHub (default launch) and Fluent (`ASPIRE_DASHBOARD_UI_VARIANT=fluent`). Use the `dashboard-visual-dev-loop` skill to launch and screenshot both.
