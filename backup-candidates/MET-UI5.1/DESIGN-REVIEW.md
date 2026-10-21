# UI5.1 bounded visual acceptance

Independent review; source and supplied image inspection only. No application launch, desktop input, gameplay edits, or new display-resolution requirement. The comparison baseline is `baseline/` and the corrected UI5 complete-world captures, not the earlier captures that omitted instanced vegetation.

## Scope and minimum before/after evidence

Use 1920×1080 at the existing UI 80/100/120% settings, retaining independent text scaling through 150%. Compare identical state, camera, selected object, scroll origin and closed-fold state. These four domains are acceptance gates for this bounded finish, not a claim that all reference-art debt is eliminated.

| Domain | UI5 problem | Minimum measurable UI5.1 result |
| --- | --- | --- |
| Global HUD | Small resource symbols and secondary type compete with oversized permit count; weak surface hierarchy. | Seven source-backed resources remain visible without wrapping values or hiding units; permit count reads as the same navigation family as City/Tasks/Milestones. MET and speed remain one line. Top navigation, right guidance/source controls, construction dock, layers and clock have no intersecting bounds at supported scale extremes. Each resource symbol remains recognizable beside its label at UI80. City woodland and buildings remain bright and dominant; no larger opaque map obstruction than UI5. |
| City overview | Oversized illustrated pause card and repeated statistics bury housing/supply/construction. | At default scale, the initial viewport contains the single next-action CTA plus all three summary headings and their primary readings, without scrolling. At UI120/text150, no clipped text or overlapping controls; any overflow uses the existing page scroll. Expanded detail folds still expose supply, market, source, queue priorities and records routes. No second competing primary CTA in the closed overview. Summary cards have clearly lower emphasis than the next action. |
| Milestones | Huge introductory art and tall individual cards hide a three-item status list. | All three stage statuses and the theme action fit in the initial viewport at default scale. Each stage has a player-facing name and distinct saved/unsaved state. At UI120/text150 each full status remains readable, even if scrolling is required. Goals and saved evidence stay available through labeled folds; raw definition/history is not the first content. |
| Tasks + Bonus | Dense orange prose, generic choice actions, technical reward IDs and ambiguous preview scope. | Each task choice exposes its purpose, immediate cost and principal consequence before its action, with a named action rather than repeated generic “选择”. Decline is visibly free; blocked research gives its real reason. Raw rules and candidate lists move into optional detail. Bonus choices show benefit and copy/research meaning before click, without event/catalog IDs dominating the face. Preview explicitly distinguishes selected-building effects from whole-city readings and labels added resource consumption/pollution as costs rather than benefits. No stale preparation toast replaces the current task/tender subtitle. |

The screenshots must retain an unobstructed construction dock and time controls. One selected-building capture must show portrait, source action and time simultaneously. A task capture must include a genuinely priced choice, not only a free event. Bonus evidence must include both local and city scope where available. Reuse existing evidence states; do not invent a new resolution matrix.

## Source invariants and risk checks

1. **Task costs are not fields on choice view rows.** `Themes._event_view` provides `cost_text`, `available` and `reason`; `cost_text` combines description, payment, deadline and reward. A short structured summary must use the merged definition returned by `_event_choice_definition`, which copies the parent definition and applies branch overrides. Charge is `currency + deposit`, plus `resources`. Preserve the successful-return condition on deposits, deadlines, cash/research/copy reward meaning, and later delivery requirements. Do not equate an empty immediate payment with an obligation-free task.
2. **Token is a special case.** Accepting a reforge token is free; using it later costs DT and is restricted after theme completion. A generic zero-cost summary must not erase that deferred cost. Preserve the core-provided explanatory text in visible consequence or clearly labeled details.
3. **Availability is not affordability.** The existing `available` field represents research gating. `choose_event` checks actual currency, inventory and other validity. Preserve command validation and visible errors; never make a cosmetic enabled state a promise that payment will succeed.
4. **Overview semantics must stay exact.** Stock, monthly flow and electric capacity are different quantities. Before the first settlement, rates remain unknown rather than fabricated zero. Queue entries share construction capacity: a first item need not be the sole currently executing project. Keep the guidance action routed through `RunCityGuidanceAction` and its stale-action guard.
5. **Milestones are saved facts.** Match exact current-theme `:M1`–`:M3` IDs in saved `milestone_ids`, not live goal satisfaction or history-prefix guesses. Completed themes must not request further settlement. Preserve maximum-goal ≤ comparisons and actual stable-duration requirements.
6. **Bonus preview is read-only.** Copy counts, active instances and pending rewards are distinct. Preserve FIFO reward handling, research versus copy versus compensation choices, source-aware preview scope and consumption semantics. No RNG, wallet or entity mutation to render compact benefits.
7. **Typography must survive settings refresh.** Font overrides must participate in `_fontBases` or equivalent scaling registration; a one-time `fontSize` assignment is insufficient. Check both bounds and actual glyph rendering: prior speed wrapping passed geometry checks. Explicit label heights must accommodate translated/player-facing strings at text150.

## Initial staged-source review

`CityPages.cs` now replaces the illustrated focus/pulse stack with one compact next action and three purpose summaries; secondary routes remain in folds. It retains first-settlement guards and 1-decimal food ratios. `CityMilestones.cs` now groups three saved stages with one action, keeps raw records in a fold, and retains ≤ goals and stable time. These changes directly address the baseline hierarchy defects. No material functional regression was identified in those inspected diffs; actual initial-viewport fit and long-name wrapping remain capture-dependent.

Task/Bonus and global HUD implementation were still in progress at this review checkpoint; this document defines their acceptance criteria, not completed source or visual approval. The tasks cost-model warning was sent to its implementation owner.

## Evidence boundaries

Final approval requires fresh native captures of these four domains and source/runtime regression evidence. Headless native rendering and callback checks do not establish real mouse/keyboard targeting, actual fullscreen behavior, tooltip dwell/modifiers, clean user exit, or hardware performance. Preserve these boundaries in delivery notes. Modern surface/icon polish is judged against the approved concept, while the user-directed top resources/navigation and bottom construction/time layout remains authoritative over older reference positions.

## Second source checkpoint: HUD, surfaces and construction

Reviewed the staged changes to `CityHud`, `CityInterface`, `CityMaterialStyle`, `CityConstructionDecision`, and the first task/Bonus rewrite. No process or desktop interaction was used.

- The tender continues to use real `slot_track` names from each offer, real `Draft.offer_cost`, same-step planner results, and the guarded final `accept_offer`. The new growth-route line does not invent an operator benefit or alter price/selection behavior. The persistent building identity, effect summary and priced “开始建造” action are the important hierarchy relative to the user's old “运营者投标比较” screenshot; the additional route line is supporting information. Slot names alone still require player knowledge, so this is not yet proof that the screen is self-explanatory.
- HUD navigation uses registered font bases, resource values retain bounded fitting, and mandatory decisions now label the clock action “处理”, consistent with its existing settlement route. No source-level action mismatch found in those diffs.
- Raised cards now receive a lighter surface than the outer archive; disclosure controls are left-aligned, less emphatic and show open/closed direction. This addresses the previously flattened surface hierarchy. The tender remains opaque for legibility. Actual contrast and map dominance require capture review; slightly stronger opacity is not evidence of reference-art fidelity by itself.
- The guidance card shrinks title/reason allocations to 36/46 logical pixels times text scale. This removes excess empty space but introduces a **capture-dependent wrapping risk**: verify the longest actual title/reason at UI120/text150, not only the short pause state. A two-line title cannot safely be assumed to fit a single-line allocation.
- Operator choices now include a second line within a 67×TextScale row. Verify complete three-slot routes and full operator names at text150; neither may collide with the other line or hide below the scroll mask. Keep the priced footer visible while those rows scroll.

### Task copy risks sent to implementation owner

The rewrite correctly derives costs from the merged branch definition and preserves raw full rules in a fold. Two concrete correctness points remain in the inspected checkpoint: (1) metric outcome “医疗覆盖提高 … 个百分点” can read as an immediate granted benefit, although this is a future completion requirement—label it as a target/obligation; (2) integer formatting of money, deposits and resource payments can hide fractional real costs, whereas the core cost text preserves one decimal. Retain non-lossy player-visible amounts. Fixed task-row/text heights are already being revised by the owner for text150, so their present geometry is not final acceptance evidence.

The old screenshot's primary/secondary confusion is addressed structurally in source, but not yet visually closed. The next evidence should demonstrate that a newcomer can identify the building purpose, selected operator, consequence, immediate price and one confirm action without opening raw rules. No additional source edit is requested merely to reproduce the approved concept's old control positions.

## Second native capture pass: visual findings

Personally viewed the approved concept, `before/ui5-city-overview.png`, `before/ui5-milestones.png`, `before/ui-task-document.png`, and second-pass `runtime-headless` overview, milestones, task document, candidates, install preview, tender, plus UI120/text150 overview/milestones/tasks/Bonus/tender and task/Bonus detail captures. This is image inspection, not an inference from passing assertions.

Confirmed improvements: the default overview now exposes all three purpose summaries and the next action in its first viewport; milestones exposes all three saved stages and its action. Default tasks replaces dense orange paragraphs and generic buttons with readable consequences, isolated payment and named actions. The install preview visibly separates local rules from whole-city prediction and marks increased electricity/pollution in amber. The tender makes building purpose, predicted change, selected operator and priced fixed confirmation comprehensible. These improvements are visible against the supplied before images.

Remaining concrete findings sent to the parent:

- **P2 — default overview icon overlaps “下一步”.** In second-pass `ui5-city-overview.png`, the small building illustration renders beyond its narrow parent into the beginning of the guidance title (approximately x1225–1283 versus title starting near x1253). The high-font capture happens not to show this collision. Bound the actual art or reserve its drawn width; checking only the parent RectTransform misses it.
- **P2 — a relationship Bonus still lacks its reason to choose.** `ui-blueprint-candidates.png` shows “职住一体” only as housing ×1.000 → ×0.900. Its configured `R17.summary` also promises improved ordering of access to local/adjacent jobs; that essential nonnumeric benefit disappears when the summary selects only numeric effects. Use the source-backed summary for relationship/configuration rules, preserving the housing tradeoff. Otherwise the card presents a pure penalty as an unexplained option.
- **Known second-pass geometry failures, owner fixing:** high-font Task waiting-header height and Bonus host-button wrapped labels. The host labels visibly split building names and coordinates over the allocated row; the parent's measured clip finding confirms this needs fixing. Default tender's third growth route sits against the lower scroll edge; parent is reducing header reservation to provide room. None of these is marked closed before replacement captures.

At UI120/text150, the task first viewport contains only the first complete choice and the tender first viewport hides operator choices below its effect summary. Both are scrollable; this is reduced density rather than a claim of overlapping content. Do not advertise all-choice first-viewport comparison at that scale. The default-size overview/milestone compactness criterion is met apart from the overview icon defect.

The approved concept still has more legible, coherent pictograms and more polished city/selection illustration. UI5.1 retains small faceted/schematic icons, thin wireframe Bonus candidate art and the established cream selection outline. Those are remaining art-polish debt, distinct from the specific clipping/misleading-summary fixes above. User-directed control placement remains valid.

## Final third-pass independent disposition

**The bounded UI5.1 clarity/layout revision passes the reviewed native-offscreen visual scope.** No remaining blocking overlap or misleading summary was found in the final replacement images personally inspected. This conclusion is limited to the captured states and does not certify the entire game or complete concept-art fidelity.

Reopened final `ui5-city-overview.png`, `ui-blueprint-candidates.png`, `ui5-tender.png`, `ui51-bonuses-ui120-text150.png`, `ui51-tasks-ui120-text150.png`, `ui-city-hud.png`, and `ui5-ui120-text150.png`:

- Overview art is now contained and separate from the complete next-action title. Default overview retains all three summaries and its detail routes in the initial viewport.
- “职住一体” now states the nearby-work benefit and the actual housing multiplier reduction together. The previous unexplained-penalty finding is closed.
- Default tender shows all three operator names and complete growth routes above the fixed price/confirmation area. The third route has visible lower clearance. Building purpose, projected effects and “开始建造 · 70 通货” are legible together.
- High-font Bonus host buttons now deliberately place complete building names above complete coordinates, with enough height for both. High-font Task waiting header has clear vertical separation from its event card. The previous clipping findings are closed in these captures.
- The unmodal HUD keeps the city and restored forest prominent; top resource/navigation values and bottom construction/time controls are readable. MET and speed remain single-line at UI120/text150. The guidance card expands for larger text without colliding with the clock or construction dock.

Independently read the final `runtime-headless/ui5-headless-validation.json`: `status: passed`, 693 check records, empty `errors`. This corroborates runtime coverage but is recorded separately from visual inspection. No application or desktop input was launched by this reviewer.

Remaining boundaries: authentic mouse/keyboard interaction, fullscreen focus, tooltip dwell/modifier behavior, ordinary user clean exit, and hardware performance are not established by these offscreen captures. High-font pages intentionally scroll and do not promise simultaneous visibility of every task/operator option. More coherent icon art, stronger illustrated Bonus identity, richer city/road art and reference-quality selection/portrait polish remain documented aesthetic debt. The delivered changes improve practical hierarchy and decision clarity; they do not establish complete Stellaris-reference visual parity.
