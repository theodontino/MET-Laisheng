# macOS application delivery

Standing authorization: after completed development and acceptance, deliver a universal macOS `.app` ZIP through a GitHub prerelease. Preserve earlier artifacts.

The `.github/workflows/release-macos.yml` workflow builds on a macOS runner when a unique `macos-*` tag is pushed. It checks official Godot 4.7.2 downloads against pinned SHA-512 hashes, imports the exact tagged source, runs Beta regressions, exports the existing universal macOS preset, checks ZIP/architectures/permissions, verifies the ad-hoc signature, and starts the exported app headlessly on macOS before publishing. The workflow token only needs repository contents write permission. It does not require an Apple certificate, notarization credentials or new repository secrets.

Use the existing branch; commit reviewed source, push it, then create and push a unique release tag at that exact commit. Example tag: `macos-0.5.0-beta.2-living-world-v1`. Do not reuse or force-update tags. Observe the actual Actions result and verify release assets before claiming GitHub delivery succeeded. An accepted tag push alone is not proof of a completed build or publication. If GitHub Actions is disabled or API access is unavailable, provide the independently validated local ZIP and identify the concrete blocker.

Local Linux fallback: install checksum-verified Godot 4.7.2 `templates/macos.zip` under the XDG data `godot/export_templates/4.7.2.stable` directory, then run `"$GODOT" --headless --path . --export-release macOS build/UNIQUE_macOS_universal.zip`. Check fresh export logs and ZIP contents. Running the PCK with Linux Godot can verify packed resources, but does not validate macOS execution or Apple signature acceptance.

Default startup remains the existing GL/2D Beta. To open the separate Hero Valley from macOS Terminal:

```sh
open -n "/absolute/path/MET · 来生.app" --args --rendering-method forward_plus -- --hero-valley
```

The package is ad-hoc signed and not Apple-notarized. Native headless startup is distinct from graphical/macOS GPU acceptance. Do not claim signing, notarization, gameplay or performance checks that were not executed. Mobile remains out of scope.

## UI Impacts V1 delivery — 2026-10-05

[Download page for the universal macOS app](https://github.com/theodontino/MET-Laisheng/blob/downloads/macos-ui-impacts-v1-7e9e1a9/MET_0.5.0-beta.2_UIImpactsV1_macOS_universal_7e9e1a9.zip). Sign in with a GitHub account that can access this repository, then choose **Download raw file** and extract `MET · 来生.app`.

The new official-template export is from source commit `7e9e1a90a207039734d23fbcd79737f21213c6ff`. ZIP size: **81,409,226 bytes**; SHA-256: `b141c2f0afd118324fee9d438290ed3a6e2360b1ffe9d2e8e82cdd7970688033`. ZIP CRC, Intel/Apple Silicon architectures, executable permissions and signature structure passed. The extracted PCK passed default Linux startup and **101 native packaged-control checks**. Native macOS launch, Apple signature verification and notarization were not performed. Source acceptance is recorded in [UI_IMPACTS_ACCEPTANCE.md](UI_IMPACTS_ACCEPTANCE.md).

GitHub Release API requests from this cloud environment returned `Forbidden`. The package, checksum, `VALIDATION.json` and instructions were therefore uploaded to a dedicated artifact-only branch, preserving previous downloads; its remote ZIP blob was fetched and independently matched to the local SHA-256. Anonymous HTTP returned 404 and does not establish authenticated browser availability. The unique tag `macos-0.5.0-beta.2-ui-impacts-v1` was also pushed to trigger the existing macOS workflow; its Actions/Release outcome remains unobserved here. This is verified GitHub branch delivery, not a claim that a GitHub Release asset was published.
