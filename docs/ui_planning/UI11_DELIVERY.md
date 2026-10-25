# UI11 — map readings and compact tile facts

2026-10-25. Implements the seven requested corrections in the existing checkout.

- Employment: occupied tiles display resident employment ratio; empty/workplace tiles display provider access strength in 可达度, never percent. Same-unit comparisons only; simulation allocations unchanged.
- Standalone Shift release toggles map numeric labels. Shift+L remains reverse layer cycling. Modal/text/tooltip keyboard ownership is respected. Map hover dwell/progress removed; ordinary UI explanations keep their dwell.
- Map overlay opacity and tonal range increased; employment uses teal for resident ratios and blue for access strength. Numeric markers have darker backing and borders.
- Tile population/employment values no longer use a clipped short number line. All four cells open local population/employment or workplace sources.
- Tile resource overview shows net food/material/goods/currency monthly amounts beside a two-column service/environment matrix. Full receipts remain expandable. Wet terrain has a Chinese label.
- New-build/upgrade same-step city changes default collapsed (scope confirmed by user). Costs and confirmation stay visible.
- Bonus installation comparison omits unchanged displayed values and empty resource groups, while retaining costs, copy consumption, confirmation and source details.

Engineering checks: UI11Map checks passed 10 assertions; actual Unity universal macOS compilation/build and bundle verification passed. The initial standalone UI compile used a stale MET.Core assembly and was superseded by the successful Unity rebuild. No Player visual/input acceptance was performed; the user owns it.

Local app: `build/ui11-delivery/MET Laisheng UI11.app`.
ZIP: `build/ui11-delivery/MET-Laisheng-0.7.0-ui.11-macOS-universal.zip`.
Release: https://github.com/theodontino/MET-Laisheng/releases/tag/v0.7.0-ui.11
