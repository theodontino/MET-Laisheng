# Modern strategy HUD — local UI.3

Latest authorization: user approved the Stellaris-reference concept on 2026-10-15 (Library `libfile_25717c339e688191a18ed99c487e2ffe`). This supersedes the prior paper, leather and wood UI direction. Existing city assets and gameplay rules are unchanged.

## Hierarchy and organization

Compact resource icons and values run across the top. Date, pause and speed occupy the top right. City, construction, licenses/decisions, tasks and Bonus occupy the left rail. The right side groups tasks, decisions and Bonus and can collapse; urgent items remain visible. Local building details, actual Bonus slots and relevant actions open at the lower left only after selection. Map layers remain at the bottom. The center remains the existing 3D city.

Full management, finances, population, policy, history, settings and source records remain reachable through the archive. Archive navigation is a side column rather than eleven equal top tabs.

## Style and readability

Use dark translucent thin-bordered panels, restrained teal accents, consistent original vector icons and short Chinese. Remove paper, clips, leather, wooden framing, pin and scroll props from the active interface. Blueprint previews retain original parameterized line art and Dock-style hover selection. Claim confirmation transfers a small glowing card toward Bonus. Installed blueprint cards carry an explicit deployed marker and location.

## Preserved rules

Use the actual FIFO reward queue, `Bonus.choose`, wallet copies, slot eligibility and `Bonus.install`. Previews use detached same-step baseline/proposal simulation. Installing consumes one unused copy. Clearing an actual building slot deactivates the instance without returning a copy. Replacement spends a new copy and deactivates the old one. No new organization/Block uninstall rule is invented. Duplicate restrictions and reduced stacking come from the actual configuration. Save/load and provider/contract/source history remain intact.

## Validation and delivery boundary

Unity 6000.6.5f1 and existing uGUI 2.6.0 helpers remain. Desktop fullscreen 1920×1080 is the supported acceptance baseline. UI scale, map zoom and font controls remain independent; keyboard ownership and reduced motion remain supported. Build a separately named local application, preserve earlier builds, do not commit, push or publish.

New Player screenshots must be compared to the approved concept for hierarchy, organization, style and readability. Automated callback/pointer-event regression is recorded separately from real mouse/keyboard acceptance. A pending application access request is a blocker for dependent input, not authorization to use another input path.
