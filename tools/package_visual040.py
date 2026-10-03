#!/usr/bin/env python3
"""Package the visual 0.4.0 builds from official Godot PCK exports.

This keeps the already verified macOS and Windows engine runtimes and places
fresh Godot-exported PCKs beside them. It never reads or writes player saves.
"""
from __future__ import annotations

import argparse
import copy
import os
import plistlib
import re
import shutil
import subprocess
import tempfile
import zipfile
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "build"
DEFAULT_MAC_RUNTIME = BUILD / "MET · 河谷新章.app"
DEFAULT_WINDOWS_RUNTIME = BUILD / "MET_0.4.0_Windows_x64.zip"
MAC_APP_NAME = "MET · 河谷新章 Visual.app"
MAC_ZIP_NAME = "MET_0.4.0_Visual_macOS.zip"
WINDOWS_ROOT = "MET_0.4.0_Visual_Windows_x64"
WINDOWS_ZIP_NAME = WINDOWS_ROOT + ".zip"
BUNDLE_VERSION = "402"
FONT_LICENSE = Path("assets/fonts/OFL-NotoSansSC.txt")
NOTICES = ("GODOT-LICENSE.txt", "THIRD-PARTY-NOTICES.txt")


def run(command: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(command, check=True, text=True)


def resolve_command(command: str) -> str:
    if os.sep in command:
        candidate = Path(command).expanduser().resolve()
        if not candidate.is_file():
            raise FileNotFoundError(f"Godot executable not found: {candidate}")
        return str(candidate)
    found = shutil.which(command)
    if not found:
        raise FileNotFoundError(f"Godot executable not found on PATH: {command}")
    return found


def validate_inputs(godot: str, mac_runtime: Path, windows_runtime: Path) -> str:
    if not (ROOT / "project.godot").is_file():
        raise FileNotFoundError(f"Not a Godot project: {ROOT}")
    if not (ROOT / "export_presets.cfg").is_file():
        raise FileNotFoundError(ROOT / "export_presets.cfg")
    if not mac_runtime.is_dir() or mac_runtime.suffix != ".app":
        raise FileNotFoundError(f"macOS runtime app not found: {mac_runtime}")
    if not windows_runtime.is_file():
        raise FileNotFoundError(f"Windows runtime ZIP not found: {windows_runtime}")
    for relpath in (*[Path("docs") / name for name in NOTICES], FONT_LICENSE):
        if not (ROOT / relpath).is_file():
            raise FileNotFoundError(f"Required external license is missing: {ROOT / relpath}")

    resolved = resolve_command(godot)
    version = subprocess.run([resolved, "--version"], check=True, capture_output=True,
                             text=True).stdout.strip()
    if not re.match(r"^4\.7\.2(?:\.|$)", version):
        raise RuntimeError(f"Expected Godot 4.7.2 for these runtimes, got: {version}")

    presets = (ROOT / "export_presets.cfg").read_text(encoding="utf-8")
    for preset in ('name="macOS"', 'name="Windows Desktop"'):
        if preset not in presets:
            raise RuntimeError(f"Missing required export preset: {preset}")
    return resolved


def export_pack(godot: str, preset: str, output: Path) -> None:
    run([godot, "--headless", "--path", str(ROOT), "--export-pack", preset, str(output)])
    if not output.is_file() or output.stat().st_size < 4:
        raise RuntimeError(f"Godot did not produce a PCK for {preset}: {output}")
    with output.open("rb") as stream:
        if stream.read(4) != b"GDPC":
            raise RuntimeError(f"Godot export is not a PCK: {output}")


def unique_pck(directory: Path) -> Path:
    packs = sorted(directory.rglob("*.pck"))
    if len(packs) != 1:
        raise RuntimeError(f"Expected exactly one runtime PCK in {directory}, found {len(packs)}")
    return packs[0]


def set_app_versions(app: Path) -> None:
    plist_path = app / "Contents/Info.plist"
    with plist_path.open("rb") as stream:
        info = plistlib.load(stream)
    info["CFBundleVersion"] = BUNDLE_VERSION
    info["CFBundleDisplayName"] = MAC_APP_NAME.removesuffix(".app")
    info["CFBundleName"] = MAC_APP_NAME.removesuffix(".app")
    info["CFBundleShortVersionString"] = "0.4.0"
    with plist_path.open("wb") as stream:
        plistlib.dump(info, stream, fmt=plistlib.FMT_XML, sort_keys=True)


def add_bundle_licenses(app: Path) -> None:
    resources = app / "Contents/Resources"
    resources.mkdir(parents=True, exist_ok=True)
    for name in NOTICES:
        shutil.copy2(ROOT / "docs" / name, resources / name)
    license_path = resources / FONT_LICENSE
    license_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / FONT_LICENSE, license_path)


def validate_mac_app(app: Path) -> None:
    pck = unique_pck(app / "Contents/Resources")
    if pck.stat().st_size == 0:
        raise RuntimeError(f"Empty app PCK: {pck}")
    with pck.open("rb") as stream:
        if stream.read(4) != b"GDPC":
            raise RuntimeError(f"Invalid app PCK: {pck}")
    with (app / "Contents/Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    if info.get("CFBundleVersion") != BUNDLE_VERSION or info.get("CFBundleShortVersionString") != "0.4.0":
        raise RuntimeError("macOS bundle version fields were not updated")
    resources = app / "Contents/Resources"
    required = [resources / name for name in NOTICES]
    required.append(resources / FONT_LICENSE)
    missing = [path for path in required if not path.is_file()]
    if missing:
        raise RuntimeError(f"Missing app license files: {missing}")


def validate_mac_zip(archive_path: Path) -> None:
    with zipfile.ZipFile(archive_path) as archive:
        damaged = archive.testzip()
        if damaged:
            raise RuntimeError(f"Corrupt macOS ZIP member: {damaged}")
        names = [name.rstrip("/") for name in archive.namelist()]
        prefix = MAC_APP_NAME + "/Contents/Resources/"
        pcks = [name for name in names if name.startswith(prefix) and name.endswith(".pck")]
        required = [prefix + name for name in NOTICES]
        required.append(prefix + FONT_LICENSE.as_posix())
        required.extend((MAC_APP_NAME + "/Contents/Info.plist",))
        missing = [name for name in required if name not in names]
        if len(pcks) != 1 or missing:
            raise RuntimeError(f"macOS ZIP validation failed: PCKs={pcks}, missing={missing}")
        with archive.open(pcks[0]) as stream:
            if stream.read(4) != b"GDPC":
                raise RuntimeError("macOS ZIP contains an invalid PCK")


def build_mac(godot: str, runtime: Path, work: Path) -> tuple[Path, Path]:
    exported_pck = work / "visual040-macos.pck"
    export_pack(godot, "macOS", exported_pck)

    stage_app = work / MAC_APP_NAME
    shutil.copytree(runtime, stage_app, symlinks=True, copy_function=shutil.copy2)
    shutil.copy2(exported_pck, unique_pck(stage_app / "Contents/Resources"))
    add_bundle_licenses(stage_app)
    set_app_versions(stage_app)
    validate_mac_app(stage_app)

    run(["codesign", "--force", "--deep", "--sign", "-",
         "--preserve-metadata=identifier,entitlements,flags,runtime", str(stage_app)])
    run(["codesign", "--verify", "--deep", "--strict", str(stage_app)])

    stage_zip = work / MAC_ZIP_NAME
    # Python sets the UTF-8 name flag consistently for the Chinese bundle name.
    with zipfile.ZipFile(stage_zip, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(stage_app.rglob("*")):
            if path.is_file():
                archive.write(path, path.relative_to(stage_app.parent))
    validate_mac_zip(stage_zip)
    return stage_app, stage_zip


def build_windows(godot: str, runtime_zip: Path, work: Path) -> Path:
    exported_pck = work / "visual040-windows.pck"
    export_pack(godot, "Windows Desktop", exported_pck)

    extracted = work / "windows-runtime"
    extracted.mkdir()
    with zipfile.ZipFile(runtime_zip) as source:
        damaged = source.testzip()
        if damaged:
            raise RuntimeError(f"Input Windows ZIP has a corrupt member: {damaged}")
        members = [name for name in source.namelist() if not name.endswith("/")]
        pck_members = [name for name in members if name.lower().endswith(".pck")]
        exe_members = [name for name in members if name.lower().endswith(".exe")]
        if len(pck_members) != 1 or len(exe_members) != 1:
            raise RuntimeError(f"Expected one EXE and one PCK in runtime ZIP; found {exe_members}, {pck_members}")
        roots = {PurePosixPath(name).parts[0] for name in members}
        if len(roots) != 1:
            raise RuntimeError(f"Expected one Windows package folder in ZIP, found {sorted(roots)}")
        old_root = next(iter(roots))
        new_pck_relative = PurePosixPath(pck_members[0]).relative_to(old_root)
        if new_pck_relative.name != "MET River Valley.pck":
            raise RuntimeError(f"Unexpected Windows runtime PCK name: {pck_members[0]}")

        stage_zip = work / WINDOWS_ZIP_NAME
        with zipfile.ZipFile(stage_zip, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as output:
            license_names = {f"{old_root}/{name}" for name in NOTICES}
            license_names.add(f"{old_root}/{FONT_LICENSE.as_posix()}")
            for info in source.infolist():
                if info.is_dir() or info.filename in pck_members or info.filename in license_names:
                    continue
                new_info = copy.copy(info)
                new_info.filename = WINDOWS_ROOT + "/" + str(PurePosixPath(info.filename).relative_to(old_root))
                output.writestr(new_info, source.read(info.filename))

            output.writestr(WINDOWS_ROOT + "/" + new_pck_relative.as_posix(),
                            exported_pck.read_bytes(), compress_type=zipfile.ZIP_DEFLATED)
            for name in NOTICES:
                output.write(ROOT / "docs" / name, WINDOWS_ROOT + "/" + name)
            output.write(ROOT / FONT_LICENSE,
                         WINDOWS_ROOT + "/" + FONT_LICENSE.as_posix())

    validate_windows_zip(stage_zip)
    return stage_zip


def validate_windows_zip(archive_path: Path) -> None:
    with zipfile.ZipFile(archive_path) as archive:
        damaged = archive.testzip()
        if damaged:
            raise RuntimeError(f"Windows ZIP member failed CRC validation: {damaged}")
        names = [name for name in archive.namelist() if not name.endswith("/")]
        base = WINDOWS_ROOT + "/"
        exes = [name for name in names if name.lower().endswith(".exe")]
        pcks = [name for name in names if name.lower().endswith(".pck")]
        required = [base + name for name in NOTICES]
        required.append(base + FONT_LICENSE.as_posix())
        missing = [name for name in required if name not in names]
        if len(exes) != 1 or len(pcks) != 1 or missing:
            raise RuntimeError(f"Windows ZIP validation failed: EXEs={exes}, PCKs={pcks}, missing={missing}")
        if not exes[0].startswith(base) or not pcks[0].startswith(base):
            raise RuntimeError("Windows EXE and PCK must be inside the package folder")
        if PurePosixPath(exes[0]).parent != PurePosixPath(pcks[0]).parent:
            raise RuntimeError("Windows EXE and PCK must be side by side")
        if PurePosixPath(exes[0]).stem != PurePosixPath(pcks[0]).stem:
            raise RuntimeError("Windows EXE and PCK must have the same base name")
        with archive.open(pcks[0]) as stream:
            if stream.read(4) != b"GDPC":
                raise RuntimeError("Windows ZIP contains an invalid PCK")


def install_outputs(stage_app: Path, stage_mac_zip: Path, stage_windows_zip: Path) -> tuple[Path, Path, Path]:
    BUILD.mkdir(parents=True, exist_ok=True)
    final_app = BUILD / MAC_APP_NAME
    final_mac_zip = BUILD / MAC_ZIP_NAME
    final_windows_zip = BUILD / WINDOWS_ZIP_NAME
    if final_app.exists():
        shutil.rmtree(final_app)
    os.replace(stage_app, final_app)
    os.replace(stage_mac_zip, final_mac_zip)
    os.replace(stage_windows_zip, final_windows_zip)
    return final_app, final_mac_zip, final_windows_zip


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", default=os.environ.get("GODOT", "godot"),
                        help="Godot 4.7.2 executable (defaults to $GODOT or godot on PATH)")
    parser.add_argument("--mac-runtime", type=Path, default=DEFAULT_MAC_RUNTIME,
                        help="verified Godot macOS app runtime to reuse")
    parser.add_argument("--windows-runtime", type=Path, default=DEFAULT_WINDOWS_RUNTIME,
                        help="verified Windows runtime ZIP to reuse")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    mac_runtime = args.mac_runtime if args.mac_runtime.is_absolute() else ROOT / args.mac_runtime
    windows_runtime = args.windows_runtime if args.windows_runtime.is_absolute() else ROOT / args.windows_runtime
    mac_runtime = mac_runtime.resolve()
    windows_runtime = windows_runtime.resolve()
    final_app = BUILD / MAC_APP_NAME
    final_mac_zip = BUILD / MAC_ZIP_NAME
    final_windows_zip = BUILD / WINDOWS_ZIP_NAME
    if mac_runtime in (final_app.resolve(),) or windows_runtime in (final_mac_zip.resolve(), final_windows_zip.resolve()):
        raise RuntimeError("Runtime inputs must remain the original packages; Visual outputs are separate.")

    godot = validate_inputs(args.godot, mac_runtime, windows_runtime)
    BUILD.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="met-visual040-", dir=BUILD) as temporary:
        work = Path(temporary)
        stage_app, stage_mac_zip = build_mac(godot, mac_runtime, work)
        stage_windows_zip = build_windows(godot, windows_runtime, work)
        outputs = install_outputs(stage_app, stage_mac_zip, stage_windows_zip)

    print("Packaged visual 0.4.0 outputs:")
    for output in outputs:
        print(output)


if __name__ == "__main__":
    main()
