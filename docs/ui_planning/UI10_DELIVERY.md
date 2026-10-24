# UI10 — tile panel construction fix

2026-10-25. Fixes the UI9 regression reported in the actual Player screenshot.

The Player log reported a duplicate Image component on Identity facts followed by a NullReferenceException in RenderTileIdentity. AddDeskTip already creates the transparent raycast graphic. The second Image creation is removed, allowing population metrics, resource tables, service cells, Bonus slots and fixed actions to be constructed. The rendered-state cache is now assigned only after all panel sections have been created.

Scope: presentation only; no simulation or save changes. Existing checkout and unrelated uncommitted work are preserved.

Checks: inspected the actual UI9 Player exception and the tooltip component ownership; checked the other UI9 tooltip sites for the same duplicate creation; built the corrected macOS universal app. Package metadata and ZIP integrity are recorded separately. Compilation and packaging are not proof of Player visual or input acceptance; those remain with the user as requested.

App: `build/ui10-delivery/MET Laisheng UI10.app`.
ZIP: `build/ui10-delivery/MET-Laisheng-0.7.0-ui.10-macOS-universal.zip`.
Release: https://github.com/theodontino/MET-Laisheng/releases/tag/v0.7.0-ui.10
