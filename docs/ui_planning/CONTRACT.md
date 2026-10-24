# Modern strategy HUD — UI.7

The visual language follows the user-approved Stellaris-reference concept of 2026-10-15 (Library `libfile_25717c339e688191a18ed99c487e2ffe`). The 2026-10-23 page-remake request and subsequent left-sidebar/two-level corrections supersede the old uniform archive and centered-window layout. Existing city assets and gameplay rules are preserved.

## Hierarchy and organization

Compact resource icons, stocks and numeric changes run across the top for the currently viewed block; clicks open the relevant details. MET remains the wordmark and opens the system menu. CP and electricity supply/occupation bars occupy the top right, distinguishing city-project CP reservation and power shortage. Date, pause and speed remain in the bottom-right time dock. Map layers remain at the bottom. The existing 3D city is the main view.

The left rail has seven entries: 城市〔暂未开放〕, 居民与服务, 施工, 财政, Bonus, 任务, 档案. The 2026-10-24 approved reorganization removes the umbrella 城市状况 page. Common pages adjoin the rail and contain usable information and actions: residents and service gaps (640), the construction queue itself (600), fiscal balances with peer 收支/市场/政策 tabs (680 for balances), owned Bonus copies (760), active tasks and pending decisions (720), and direct classified archives (1200). Existing detailed tables retain their wider comparisons; their left edge stays attached to the navigation.

Each page has a fixed section and parent. Switching sections remembers choices, filters and scroll without making the previous section its parent; re-clicking the current section opens its common page. Peer tabs and object/resource selection do not add a Back layer. Detail headers show the block and path and name their fixed return target. Local object details retain their originating section. Cross-section actions name their destination and highlight that section. Map inspection retains a named return and restores selection and scroll. Common pages have only the top-right close control; details add a parent return in the header. Esc closes explanation/confirmation, local detail, detailed page, then the section; on an unobstructed map it opens MET settings.

Topbar hover explains only the pointed indicator for the current block: population/housing/service gaps; resource stock, capacity and actual flows; operating cash rates and separately recorded transactions; CP capacity/use/reservation; power supply/demand/shortage. It never opens a page or pauses the simulation. Existing two-second progress and readable pointer entry into the explanation remain. Resource clicks select the corresponding fixed fiscal detail; population and currency open their common pages, CP opens construction, DT opens system time/lifecycle settings. Before the first settlement, actual flows are explicitly pending. Quick explanations use short, readable rows for stock, recent monthly change and the relevant shortage; no zero-valued receipt dump, fractional simulation period or trade-clock paragraph. Normalize displayed flows to the same monthly unit and label their recent-settlement basis. Full receipts remain in detailed tables. Topbar body type is 18 with the normal high-contrast text color.

Permanent-city tree, governance, successor expansion, cross-block logistics, city research and city projects are unavailable in this iteration. Saved data and simulation remain intact, and historical city information stays read-only. Initial city creation, the current block, local policies, external markets, rewards, naming, capstones and settlement remain available. Already handed-over worlds get no reappointment exception. Claiming rewards stays in Tasks; using owned copies belongs to Bonus and requires an explicit navigation action.

## Style and readability

Use dark translucent thin-bordered panels, restrained teal accents, consistent original vector icons and short Chinese. Remove paper, clips, leather, wooden framing, pin and scroll props from the active interface. Blueprint previews retain original parameterized line art and Dock-style hover selection. Claim confirmation transfers a small glowing card toward Bonus. Installed blueprint cards carry an explicit deployed marker and location.

## Preserved rules

Use the actual FIFO reward queue, `Bonus.choose`, wallet copies, slot eligibility and `Bonus.install`. Previews use detached same-step baseline/proposal simulation. Installing consumes one unused copy. Clearing an actual building slot deactivates the instance without returning a copy. Replacement spends a new copy and deactivates the old one. No new organization/Block uninstall rule is invented. Duplicate restrictions and reduced stacking come from the actual configuration. Save/load and provider/contract/source history remain intact.

## Validation and delivery boundary

Unity 6000.6.5f1 and existing uGUI 2.6.0 helpers remain. Desktop fullscreen 1920×1080 is the supported acceptance baseline. UI scale (80/100/120%), map zoom and font controls remain independent; keyboard ownership and reduced motion remain supported. Main/column/map-source scroll sensitivity is 48, explanation scrolling 24. Ordinary UI and map hover share their existing two-second wait, visualized with a thin progress bar that clears on cancellation or completion.

The 2026-10-23 implementation request explicitly adopts the standing macOS delivery authorization in AGENTS.md, superseding this document's earlier local-only restriction: after required checks, package the actual macOS app as a ZIP and deliver a corresponding prerelease. Preserve earlier artifacts and unrelated uncommitted work. Source publication and native input/visual acceptance must be reported separately and accurately.

New Player screenshots must be compared to the approved concept for hierarchy, organization, style and readability. Automated callback/pointer-event regression is recorded separately from real mouse/keyboard acceptance. A pending application access request is a blocker for dependent input, not authorization to use another input path.
