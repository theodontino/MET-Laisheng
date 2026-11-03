# UI13 — demo V3 layout in the native Unity game

2026-10-25. This iteration ports the approved independent demo's organization and graphical presentation to the existing Unity game. It does not run or embed the HTML demo, replace the simulation, or change player saves.

## Delivered organization

- Default fullscreen and Canvas reference: **2560 × 1440 (2K)**. Runtime startup, Player settings, build script and the settings caption agree. UI scale and text size remain independent.
- Left global navigation: City (unavailable), Residents and Services, Resources and Finance, Construction, Tasks and Growth, Bonus, Archives. The rail groups daily operation, development and records.
- Graphical small workspaces: housing/employment/job usage, service shortfalls, stocks and signed contribution bars, construction ability composition and progress, task goals, Bonus inventory and command previews. Sources are read from the game's detached snapshot.
- Advanced tables expand across the available screen, then return to their fixed compact workspace. Peer tabs and cross-section changes do not create a browsing-history parent chain. Each workspace keeps its selection/filter and scroll.
- The selected tile stays a separate lower-left map object, with a building portrait, effective-capacity bars, net local income beside services/environment, graphical Bonus sockets and fixed actions.
- Selecting a map tile from a compact workspace temporarily hides the global workspace and provides a named return. Pointer interaction outside the sidebar is separated from keyboard ownership. Full tables and confirmation windows continue to own map input.
- Construction and upgrade confirmation stay independent centered windows. Existing payment, queue, temporary-crew, progressive specialization and upgrade-operation rules remain intact. Their city-change table remains folded by default.
- Original complete records, source ledgers, policies, reward/capstone/settlement flows and details remain reachable. Permanent-city management remains closed.

## Engineering evidence

Actual Unity 6000.6.5f1 compilation and isolated active uGUI checks exercised 185 page renders and 4,040 assertions across UI 80/100/120% and text 100/125/150%. They cover compact/advanced rendering and collapse, resource/service selection, Bonus preview, section restoration, map handoff/return, positive visible control geometry, unchanged live fixture state/RNG and no fixture save writes. This is a fixed logical-canvas engineering check, **not** a screenshot, overlap/readability or real-input acceptance.

The checks exposed missing CanvasRenderer dependencies on custom graphics and native CanvasGroup fake-null handling; both were corrected. An unrelated Unity Editor SearchDatabase startup-index exception is retained in the log and explicitly excluded from game assertions. Initial sandbox IPC/licensing startup failures were resolved by restarting only this run's failed processes through the normal Unity launch path.

Independent read-only review found a Bonus direct-entry filter issue; opening a specific owned Bonus now clears the availability filter. No gameplay algorithms, currency rules, save schema or RNG behavior were changed in this iteration.

Per the user's instruction, **Player visual and real mouse/keyboard acceptance belongs to the user**. No new Player screenshot, actual input pass, pixel-for-pixel parity, signing identity or notarization is claimed here.

## Artifacts

Unity macOS Universal build succeeded. The app is ad-hoc signed and verified (not Developer ID signed or notarized). Existing checkout, branch HEAD, staged state and earlier packages are preserved. Release source uses an independent snapshot derived from UI12 and the exact current Unity inputs, not a blanket commit of the working tree.

- Local app: `build/ui13-delivery/MET Laisheng UI13.app`
- ZIP: `build/ui13-delivery/MET-Laisheng-0.7.0-ui.13-macOS-universal.zip`
- Expected release tag: `v0.7.0-ui.13`; its source snapshot is recorded with the release.
- Raw engineering/build logs: `build/ui13-delivery/checks/` (local).
