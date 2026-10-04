# MET macOS download — Beta.2 / Living World V1

Download the ZIP in this branch and extract `MET · 来生.app`.
The application supports Apple Silicon (arm64) and Intel (x86_64).
Game source commit: bc5971972a64659d5d63a379b02d67783d6e51d5.

The ZIP CRC, universal Mach-O architectures, executable permissions and embedded
signature blobs were checked. Its exported PCK started successfully and rendered
the exact Hero Valley golden screenshot with Linux Godot. This is not macOS
hardware validation. The app uses ad-hoc signing and is not Apple-notarized.

Default startup is the GL/2D Beta game. Optional 3D fixture launch from Terminal:

```sh
open -n "/absolute/path/MET · 来生.app" --args --rendering-method forward_plus -- --hero-valley
```

This branch distributes the validated app because the cloud environment's GitHub
Release API returned Forbidden. Gameplay source is on codex/met-beta-v3; this
branch only contains download artifacts. The attached SHA-256 verifies the ZIP.
