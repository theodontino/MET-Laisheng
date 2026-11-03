# Default save impact audit — actual read-only comparison

Default path: `/Users/lizhirui/Library/Application Support/com.projectmet.laisheng.unitybeta/Wild-G1/`.
Current copies: `build/rollback/wild-content-20261029/default-save-audit/` (main, .bak, display settings, Player logs, manifest, field diff). No original files were written or restored by this audit.

## Precise conclusion
The default save was actually written during the accidental non-isolated launch. It is NOT unchanged. Main mtime is 2026-10-29 02:35:30.901532 UTC; .bak is 02:35:28.916975 UTC. This is local 10:35 on the host. Player.log identifies test PID98599 and the current Unity6000.6.5f1 startup; log timestamp is 02:34:03 UTC. The process was terminated after the isolation error was discovered.

Current main SHA256: cf0e4db669afa78101ce1534c8ba992bdd7e1e297503b7e63ce1f692a9d15a99
Current .bak SHA256: 90992358346c7a89b9b7edabc48ee23ef4505c4620e6a0ac14d8db41ffa0b8c6

Exactly three differences between CURRENT .bak and CURRENT main:
- /last_seen_utc: 1793241328.909 -> 1793241330.896
- /blocks/K000003/state/beta_time/last_seen_utc: same change
- /blocks/K000003/state/wall_mark: same change
All other JSON fields, including economy, entities, technology and reward state, are equal between these two snapshots. Display settings mtime remains 2026-10-27; no evidence of this run changing display settings.

## Important evidence limit
Neither current file is a proven pre-launch copy: both were written during this launch. The earlier 16-directory backup selected MET-named directories and MISSED the bundle-ID directory above. Its manifest contains no Wild-G1 save for this actual default path. Searching all game-save files under this repository build tree found no matching city_id and seed (city:1676170440 / 1676170440). Thus no full pre-launch baseline is available here. The current .bak must not be described as a pre-operation backup.

We can prove the last two writes differ only in timestamps; we CANNOT prove the whole launch only changed timestamps. CitySession.Resume explicitly adds offline_seconds, forces pause, and calls local resume_time; SaveInternal sets the three observed timestamps. Current offline_seconds is 169985.50699973106, but its pre-launch value is absent, so the delta cannot be measured. Current city is paused, external_seconds=3.7994250000000003, housing_levels=0, material_exported=0, no II ownership/reward. These are observations, not proof of unchanged pre-launch gameplay. Player logs do not contain a complete save-field audit.

No meaningful gameplay action was confirmed through CUA, but a timed-out click cannot be retroactively certified as unexecuted. No automatic rollback was performed. Preserve both current copies before any future choice; restoring .bak would only roll back the last ~1.987 seconds of timestamp updates, not reliably restore the pre-launch state.

## Final deliverable status
HousingII20/90, W01 conditional food15%, persisted finite three-choice reward10material/1goods/W01 are implemented. W02 deferred. Core automated suites: content115, trade59, G0 33, G1 241, network102, Core174 pass; not natural-play evidence. Official isolated Unity compile and universal macOS Player build exit0, ad-hoc signature verifies, Metal startup observed. App: /tmp/MET Wild Content 20261029.app. No publish/push.
Actual II purchase/three-choice/manual install/save-reopen UI remains unverified. CUA capture errors -3811 and noWindowsAvailable stopped interaction. Isolated new save subsequently reached origin screen; requested2560x1440 but actual surface3456x2168, so 2K baseline not accepted. Hub authorization reused successfully; no repeated permission requests are needed. No system capture/security settings changed or diagnosed as a proven fix.
