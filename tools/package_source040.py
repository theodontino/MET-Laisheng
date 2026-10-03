#!/usr/bin/env python3
"""Create the reproducible MET 0.4 Visual source archive.

Run from any directory with:
    python3 tools/package_source040.py

The archive intentionally contains project source and authored art, not build
outputs, engine caches, historical evidence bundles, or player data.
"""

from __future__ import annotations

import os
import re
import tempfile
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "build" / "MET_0.4.0_Visual_Source.zip"
ARCHIVE_ROOT = "MET_0.4.0_Visual_Source"

SOURCE_DIRS = (
    "core",
    "ui",
    "content",
    "assets",
    "tests",
    "tools",
    "docs",
    "art_source",
)
ROOT_FILES = (
    ".gitignore",
    "AGENTS.md",
    "README.md",
    "project.godot",
    "export_presets.cfg",
    "main.tscn",
)

SYSTEM_FILES = {".ds_store", "thumbs.db", "desktop.ini"}
EXCLUDED_DIRS = {
    ".godot",
    ".agents",
    ".codex",
    "build",
    "__pycache__",
    "node_modules",
    "__macosx",
    "user_data",
    "user_saves",
    "player_saves",
    "save_data",
}
SAVE_EXTENSIONS = {".sav", ".save", ".savegame"}
SAVE_JSON_NAME = re.compile(
    r"^(?:player[-_ ]?save|default[-_ ]?save|save[-_ ]?(?:slot|game|player))",
    re.IGNORECASE,
)
EVIDENCE_DIR = Path("evidence040_visual/final")
EVIDENCE_SUFFIXES = {".json", ".log", ".html"}


def is_system_or_hidden(rel: Path) -> bool:
    for index, part in enumerate(rel.parts):
        lower = part.lower()
        if lower in SYSTEM_FILES or lower.startswith("._"):
            return True
        if part.startswith(".") and part != ".gdignore" and not (index == 0 and part == ".gitignore"):
            return True
    return False


def is_excluded(rel: Path, is_dir: bool) -> bool:
    parts = rel.parts
    lower_parts = [part.lower() for part in parts]

    if is_system_or_hidden(rel):
        return True
    if any(part in EXCLUDED_DIRS for part in lower_parts[:-1] if not is_dir) or (
        is_dir and lower_parts[-1] in EXCLUDED_DIRS
    ):
        return True
    if any(part.startswith(".baseline") for part in parts):
        return True

    # Historical evidence directories stay out. The visual-final directory is
    # added separately with a narrow extension allowlist below.
    if any(part.lower().startswith("evidence") for part in parts):
        allowed_visual_final = (
            not is_dir
            and len(parts) == len(EVIDENCE_DIR.parts) + 1
            and tuple(part.lower() for part in parts[:len(EVIDENCE_DIR.parts)]) == tuple(
                part.lower() for part in EVIDENCE_DIR.parts
            )
            and Path(rel.name).suffix.lower() in EVIDENCE_SUFFIXES
        )
        if not allowed_visual_final:
            return True

    if is_dir:
        return False
    if Path(rel.name).suffix.lower() in SAVE_EXTENSIONS:
        return True
    if Path(rel.name).suffix.lower() == ".json" and SAVE_JSON_NAME.match(rel.name):
        return True
    return False


def walk_source_dir(name: str) -> list[Path]:
    base = ROOT / name
    if not base.is_dir():
        raise FileNotFoundError(f"Required source directory is missing: {base}")

    found: list[Path] = []
    for current, dirs, files in os.walk(base, topdown=True, followlinks=False):
        current_path = Path(current)
        kept_dirs: list[str] = []
        for dirname in sorted(dirs):
            rel = (current_path / dirname).relative_to(ROOT)
            candidate = current_path / dirname
            if candidate.is_symlink() or is_excluded(rel, is_dir=True):
                continue
            kept_dirs.append(dirname)
        dirs[:] = kept_dirs

        for filename in sorted(files):
            candidate = current_path / filename
            rel = candidate.relative_to(ROOT)
            if candidate.is_symlink() or not candidate.is_file():
                continue
            if not is_excluded(rel, is_dir=False):
                found.append(rel)
    return found


def collect_paths() -> list[Path]:
    paths: set[Path] = set()
    for name in SOURCE_DIRS:
        paths.update(walk_source_dir(name))

    for name in ROOT_FILES:
        path = ROOT / name
        if path.is_file() and not path.is_symlink():
            paths.add(Path(name))
        elif name != ".gitignore":
            raise FileNotFoundError(f"Required project file is missing: {path}")

    evidence_root = ROOT / EVIDENCE_DIR
    if evidence_root.is_dir() and not evidence_root.is_symlink():
        for candidate in sorted(evidence_root.iterdir()):
            if (
                candidate.is_file()
                and not candidate.is_symlink()
                and candidate.suffix.lower() in EVIDENCE_SUFFIXES
                and not is_system_or_hidden(candidate.relative_to(ROOT))
            ):
                paths.add(candidate.relative_to(ROOT))

    return sorted(paths, key=lambda path: path.as_posix())


def archive_name(rel: Path) -> str:
    return f"{ARCHIVE_ROOT}/{rel.as_posix()}"


def validate_archive(path: Path, expected: list[Path]) -> None:
    with zipfile.ZipFile(path, "r") as archive:
        bad_member = archive.testzip()
        if bad_member:
            raise RuntimeError(f"ZIP CRC check failed for {bad_member}")

        names = archive.namelist()
        expected_names = [archive_name(rel) for rel in expected]
        if names != expected_names:
            raise RuntimeError("ZIP members differ from the selected source file list")
        if not names or any(not name.startswith(f"{ARCHIVE_ROOT}/") for name in names):
            raise RuntimeError("ZIP does not have the required single top-level folder")

        for name in names:
            rel = Path(name).relative_to(ARCHIVE_ROOT)
            if is_excluded(rel, is_dir=False):
                raise RuntimeError(f"Excluded content found in ZIP: {name}")
            if rel.parts[:len(EVIDENCE_DIR.parts)] == EVIDENCE_DIR.parts and Path(name).suffix.lower() not in EVIDENCE_SUFFIXES:
                raise RuntimeError(f"Non-text evidence file found in ZIP: {name}")

        required = {archive_name(Path(name)) for name in ROOT_FILES if name != ".gitignore"}
        missing = required.difference(names)
        if missing:
            raise RuntimeError(f"Required project files absent from ZIP: {sorted(missing)}")


def create_archive() -> Path:
    paths = collect_paths()
    if not paths:
        raise RuntimeError("No project files selected for the source archive")

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    fd, temp_name = tempfile.mkstemp(
        prefix="MET_0.4.0_Visual_Source_", suffix=".tmp", dir=OUTPUT.parent
    )
    os.close(fd)
    temp_path = Path(temp_name)

    try:
        with zipfile.ZipFile(
            temp_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9
        ) as archive:
            for rel in paths:
                source = ROOT / rel
                info = zipfile.ZipInfo(archive_name(rel), date_time=(2020, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = (0o100644 & 0xFFFF) << 16
                archive.writestr(info, source.read_bytes(), compress_type=zipfile.ZIP_DEFLATED)

        validate_archive(temp_path, paths)
        os.replace(temp_path, OUTPUT)
    except Exception:
        temp_path.unlink(missing_ok=True)
        raise

    return OUTPUT


if __name__ == "__main__":
    result = create_archive()
    with zipfile.ZipFile(result, "r") as archive:
        print(f"Created {result}")
        print(f"Files: {len(archive.namelist())}; size: {result.stat().st_size:,} bytes")
