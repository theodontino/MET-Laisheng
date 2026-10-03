# MET Laisheng Beta continuation contract

## Current presentation contract

The latest authorized presentation contract is [UI and map reset](docs/beta/UI_RESET.md), which supersedes earlier HUD/inspector layout requirements. Preserve the four layers: L0 only DT/population/food/time, L1 bounded alerts/theme progress/active legend, L2 contextual graphics and one primary action, L3 full management and records. Keep all underlying gameplay and source-backed information reachable.

Throughout pre-1.0, the game defaults to desktop fullscreen with a minimum supported resolution of **1920×1080**. The user explicitly excludes testing different window sizes. Validate this fullscreen baseline; do not reintroduce small-window or mobile layout work as an acceptance requirement.

## Project vision and scope

Current authorized implementation contract: [Beta v3](docs/beta/design-v3/00_READ_FIRST.md), with [unit audit](docs/beta/UNIT_AUDIT.md). Its explicit replacements supersede the historical 0.4 requirements below and in the constitution. Beta uses finite DT, offline duration recording only (no Reserve gameplay), Local-Time immigration, annual/population drafts, repeated Bonus copies and blueprint upgrades, theme commissions, and naming/capstones at actual DT zero. Preserve all source-backed simulation and display invariants. Work on the existing branch/checkout; do not create a worktree unless explicitly requested.

Read [MET 项目宪法](docs/PROJECT_CONSTITUTION.md) before planning design or gameplay changes. Preserve its distinction between long-term vision, stable design principles, 0.4 requirements, and future outlook. Design requirements are not proof of implementation or authorization to implement every idea. The city is the protagonist; meaningful choices change the value of later choices. Preserve the established core loop. Keep 0.4 to one Block; District and cross-Block systems remain outlook only. Pre-alpha does not require cross-version save compatibility. Follow subsequent explicit project decisions when updating this baseline.

## Team and measurement workflow

The user requests `gpt-6-luna` at max reasoning for file research and summaries, `gpt-6-astra` for independent review, and the parent model for implementation. Parallelize independent work, but serialize Godot, Blender and native performance processes. Freeze measured source/resources during acceptance; separate Simulation cost from renderer frame intervals and distinguish software Vulkan from target desktop GPU evidence.

## Pre-1.0 platform scope

Throughout pre-1.0, mobile is outside implementation and delivery scope. The only mobile-related requirement is to consider future mobile constraints in architecture, input/UI design, and performance/resource budgets, leaving reasonable headroom. Do not turn this consideration into mobile-specific implementation, adaptation, packaging, device testing, or release requirements. Missing mobile validation does not block pre-1.0 delivery. Existing mobile code, artifacts, and historical reports do not create ongoing mobile obligations; preserve their historical meaning. Reassess mobile scope separately at 1.0 or later; support is not automatically promised.

Continue this exact B-lineage game. Do not substitute another demo. Read README.md, docs/v030 and the locked design030 package. Preserve gameplay content unless explicitly authorized. Presentation reads snapshots; never alter RNG, wallets or entities to draw previews. Keep operator/provider/slot history and source-aware service capacities. Inspect exact source and use the delivered final evidence; do not rebrand old evidence. No automatic GitHub publishing. Mac/mobile hardware results must be distinguished from Linux/headless tests. New artwork is original parameterized illustration, not a verified pure image-model pipeline.

## Independent Living World3D visual axis

The optional read-only Living World3D path is documented in [architecture and migration notes](docs/living_world/ARCHITECTURE.md); acceptance evidence belongs in [V1 acceptance](docs/living_world/ACCEPTANCE.md). It is an independent presentation experiment inside this same project, not a replacement game or authorization to rewrite existing visual contracts. Preserve the default GL Compatibility / 2D startup in `project.godot` and `main.tscn`. Enter the fixed 37 Hex Hero Valley only with `"$GODOT" --path . --rendering-method forward_plus --rendering-driver vulkan -- --hero-valley`; renderer and driver overrides are per-process. Renderer modules consume detached, committed snapshots only. Do not read or write a player's save from the fixture or let presentation change gameplay state, RNG, economy, entities, or persistent data.

The V1 visual target is a fixed 37 Hex river valley with four placed buildings, one registered original tree prototype, and one shared fixed-daylight environment. Keep gameplay, renderer and asset registry identities separate. Check source and runtime permissions in `docs/living_world/ASSET_PROVENANCE.md`: `LicenseRef-MET-Project-Original` is not CC0 and does not grant general third-party reuse. Keep mobile, weather/day-night simulation, full catalog coverage, and automatic GitHub publication out of this slice. Record real execution, screenshots and performance evidence only in `ACCEPTANCE.md`; software Vulkan / lavapipe evidence is not hardware GPU performance evidence.
