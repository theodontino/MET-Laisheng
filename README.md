# MET Unity Hero Valley source preview

[Download the Unity source project](MET_Unity_Hero_Valley_Source_98242e1.zip)

This is a standalone Unity 6000.0.58f2 source project, not a macOS app or complete gameplay migration. It reuses the MET Hero Valley geometry (37 Hex, 4 buildings, 29 tree instances) with official URP/glTFast/Newtonsoft packages. Original Godot gameplay remains in the main source checkout.

Source commit: `98242e180682051d82479056fe6d85fe790a213d` (branch `codex/met-beta-v3`).
ZIP SHA256: `6a59d13c8b4c26445dae98d10326071b0eb82c211e9f7cffecb27379bdf38090`.

Executed evidence: 36/36 Godot GLTF bridge tests, portable binary/hash/provenance validation, 401/401 existing Godot core checks and 6/6 routes, ZIP CRC/member hashes and extracted portable validation. Independent source review fixes are included.

Not executed: Unity C# compilation, package resolution/import, Player rendering, Unity screenshots/performance, Unity macOS build or native Mac launch. A lawfully activated Editor and working installation/network configuration are required. Current cloud Editor installation failed; no Unity account/activation is configured. Source files are not proof that Unity ran.

After extraction, add METLivingWorld_Unity to Unity Hub using the pinned Editor. The README and PortDocumentation explain two-phase URP setup and license boundaries. No passwords, license files, imported caches or Editor binaries are included.
