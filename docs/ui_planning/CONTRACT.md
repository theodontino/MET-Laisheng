# Modern strategy HUD — UI.6

The visual language follows the user-approved Stellaris-reference concept of 2026-10-15 (Library `libfile_25717c339e688191a18ed99c487e2ffe`). The 2026-10-23 page-remake request and subsequent left-sidebar/two-level corrections supersede the old uniform archive and centered-window layout. Existing city assets and gameplay rules are preserved.

## Hierarchy and organization

Compact resource icons, stocks and numeric changes run across the top for the currently viewed block; clicks open the relevant details. MET remains the wordmark and opens the system menu. CP and electricity supply/occupation bars occupy the top right, distinguishing city-project CP reservation and power shortage. Date, pause and speed remain in the bottom-right time dock. Map layers remain at the bottom. The existing 3D city is the main view.

The left rail has five categories: 城市, 经营, Bonus, 任务, 档案. A category opens a compact summary immediately adjoining the rail. Summary rows explain current status and offer explicit buttons for detailed decisions. Details also extend from the left rail and use widths suited to their content, with comparison columns and a fixed action area. Fullscreen presentation is reserved for genuinely necessary space; centering every page is not the default.

First-level summaries offer one `收起侧栏` action. Detail views name the actual previous destination, such as `返回经营`, alongside `关闭面板`. Esc closes explanations before local detail/page; at a first-level sidebar it collapses the sidebar. Map inspection retains a named return entry and restores page selection and scroll position. Milestones route to 任务 → 成长路线. Historical records remain in 档案.

Each page has its own responsibility and information order: state/problem/action first; comparisons and decisions next; full provenance and history on demand. Ledger income, expense and period reconciliation remain visible together; markets separate quotes, orders and entry; Bonus separates inventory, host/slot and effects; city governance, networks, research and projects preserve their distinct scopes. Full source records remain reachable. Underlying building controls are hidden while a management page owns input.

## Style and readability

Use dark translucent thin-bordered panels, restrained teal accents, consistent original vector icons and short Chinese. Remove paper, clips, leather, wooden framing, pin and scroll props from the active interface. Blueprint previews retain original parameterized line art and Dock-style hover selection. Claim confirmation transfers a small glowing card toward Bonus. Installed blueprint cards carry an explicit deployed marker and location.

## Preserved rules

Use the actual FIFO reward queue, `Bonus.choose`, wallet copies, slot eligibility and `Bonus.install`. Previews use detached same-step baseline/proposal simulation. Installing consumes one unused copy. Clearing an actual building slot deactivates the instance without returning a copy. Replacement spends a new copy and deactivates the old one. No new organization/Block uninstall rule is invented. Duplicate restrictions and reduced stacking come from the actual configuration. Save/load and provider/contract/source history remain intact.

## Validation and delivery boundary

Unity 6000.6.5f1 and existing uGUI 2.6.0 helpers remain. Desktop fullscreen 1920×1080 is the supported acceptance baseline. UI scale (80/100/120%), map zoom and font controls remain independent; keyboard ownership and reduced motion remain supported. Main/column/map-source scroll sensitivity is 48, explanation scrolling 24. Ordinary UI and map hover share their existing two-second wait, visualized with a thin progress bar that clears on cancellation or completion.

The 2026-10-23 implementation request explicitly adopts the standing macOS delivery authorization in AGENTS.md, superseding this document's earlier local-only restriction: after required checks, package the actual macOS app as a ZIP and deliver a corresponding prerelease. Preserve earlier artifacts and unrelated uncommitted work. Source publication and native input/visual acceptance must be reported separately and accurately.

New Player screenshots must be compared to the approved concept for hierarchy, organization, style and readability. Automated callback/pointer-event regression is recorded separately from real mouse/keyboard acceptance. A pending application access request is a blocker for dependent input, not authorization to use another input path.
