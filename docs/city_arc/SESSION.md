# Permanent city session, governance and save format

Implemented scope: `CitySession` owns a dictionary of `Game` instances and permanent City/District/Block metadata. A simulation replacement of `Game.s` remains authoritative. `snapshot()` deep-copies each latest local state into `blocks[id].state`; that field never belongs to live metadata. Focusing a Block changes only the focus ID, and direct construction permission follows the unique active District.

`New` creates a nonnull bootstrap Game and a paused `choose_district` City. `BeginDistrict` selects a landing and leaves the established origin UI available. Its four-argument overload lets the player choose an actual sponsor. The first District uses the existing initial charter. Further Blocks and Districts withdraw the startup bundle from an existing playable sponsor before creating the new Block. `AfterLocalCommand` replaces the chosen origin's initial cash/stocks with that prepaid bundle exactly once. A playable paid Block with an unsettled startup cannot be saved.

Governance consumes population-weighted actual food, finite service coverage, pollution and actual fiscal readings. District support and mandate respond over Local months; the promise is two stable Blocks. Confirming a Block stable requires at least one actual Local month and the existing supply/housing/electricity/fiscal safety check. Stabilizing a Block leaves its District in office. Voluntary handover requires the promise and actual supply/governance thresholds. Sustained shortage/bankruptcy or governance failure creates a crisis and ultimately retires the District to autonomy. All entities and Games remain owned by the City. A return pays real cash, costs support and begins another recorded term, without reinitializing the region.

Local Bonus copies and installed instances remain in their original Block. Portable eligibility is data-driven; transfer moves one actually unused license charge, adds a unique source to the recipient, and stores the source/target/event record. Stable specialized Blocks with actual operating relevant industry establish permanent District institutions and City capabilities. Institution source rules are projected by the economy partial; City capabilities reduce later startup investment. No old stock is recreated for these capabilities.

History has permanent City IDs, keyed receipts, observed project/reward/population records and a persisted local journal cursor. The last local journal entry uses the same canonical encoder as the save envelope, including numeric normalization; repeated capture and save/restore do not replay events or rewards. Actual building definition/operator/location, industries, waterfront layout, residents and institutions produce inspectable identity readings.

The independent filename is `met_city_v1.json`; the former single-Block Unity save is untouched. `CityStore.write` validates all local and City state before creating a temporary file, revalidates its persisted bytes and checks the previous main/backup before replacement. `read` semantically checks main, temporary and backup in that order and only returns a valid full envelope. Invalid candidates remain available for inspection. `Restore` validates before replacing the owner graph and rebuilds derived world/science context; `Resume` pauses and records offline duration without advancing a Block, world stocks or DT.

## Reversible candidate parameters

The JSON source is `content/beta/city_arc.json`, mirrored in Unity StreamingAssets. These defaults are implementation candidates, not a claim of proven pacing or playability.

| Parameter | Candidate | Purpose |
| --- | --- | --- |
| Startup currency | 140, plus 60 for a later District | Actual source investment retained in the new treasury |
| Startup stocks | Food 12, material 10, goods 2 | Actual transfer to the new municipal warehouse |
| Agricultural / industrial start | Additional food 3 / material 2 | Specialized investment differs with the selected direction |
| Crisis / retirement | 3 / 6 Local months in actual bad readings | Opportunity to fix ongoing supply/fiscal/governance failure |
| Return cost | 90 currency and 12 support | Reopening old governance carries a real cost |
| City startup reduction | 5% per sourced capability, capped at 20% | Prior specialized development changes later investment |
| Institution startup reduction | Agricultural food / industrial material requirement ×0.9 | An established District institution changes later Block choices |

## Current validation evidence

Pure C# core compilation passed with zero errors and zero warnings. `tools/unity_core/CityGovernance.Checks.csproj` executed 39 assertions covering permanent ownership, paid startup and origin settlement, Local/Portable license semantics, latest replaced `Game.s` capture, focus and RNG preservation, persisted history, semantic corruption/backup recovery, offline recording, crisis asset retention, paid return and final envelope validation. The runner used explicitly seeded funds/licenses/shortage for branch coverage. This is focused mechanism evidence, not a no-injection full progression, engine/UI, macOS Player, performance or playability acceptance. Integrated evidence belongs in `ACCEPTANCE.md`.
