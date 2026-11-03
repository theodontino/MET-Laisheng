# UI8 delivery — 2026-10-25

Implemented the approved tile/build/queue/task/layer reorganization in the existing dirty checkout. User explicitly owns Player acceptance (「不用验收，我来验收」).

- Unified local tile information and graphical build actions; compact HUD and Bonus count badge.
- Independent centered new-build/upgrade window with same-step comparisons and fixed payment controls.
- Complete task page and three-stage growth view, without duplicate summary/tabs.
- Four distinct service layers with keyboard cycling and source links.
- Temporary crews: configured +4 CP/month, one game month, 60 × 2^active price, local-only capacity, exact expiration overlap.

Engineering evidence: Unity 6000.6.5f1 macOS universal build succeeded; ad-hoc signing passed. 46 focused core assertions passed ([log](evidence/ui8/core-checks.log)); checks cover crew quoting/payment, pause/offline, expiration, power/material limits, city reservation exclusion, serialization and detached tile receipts. Independent source review found a tile priority marker/key mismatch, fixed before the build.

Player visual, mouse/keyboard, scale/font and end-to-end acceptance are intentionally left to the user. No new Player screenshots were taken. Signing is ad-hoc, not Developer ID/notarization.

Local application: `build/ui8-delivery/MET Laisheng UI8.app`.
ZIP: `build/ui8-delivery/MET-Laisheng-0.7.0-ui.8-macOS-universal.zip`.
Release: https://github.com/theodontino/MET-Laisheng/releases/tag/v0.7.0-ui.8
