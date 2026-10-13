#!/usr/bin/env python3
"""Prepare a local copy of official Unity 6.6 Burst tools when Hub omits execute bits."""
import os
from pathlib import Path
import shutil
import stat

ROOT = Path(__file__).resolve().parents[1]
EDITOR = Path(os.environ.get("UNITY_EDITOR", "/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity"))
SOURCE = EDITOR.parent.parent / "Resources/Burst/Client"
TARGET = ROOT / ".toolcache/unity-burst-6000.6.5f1/Client"


def copy_file(source, target):
    shutil.copyfile(source, target)
    os.chmod(target, stat.S_IMODE(os.stat(source).st_mode))
    return target


shutil.copytree(SOURCE, TARGET, dirs_exist_ok=True, copy_function=copy_file)
for name in ("llvm-lipo", "dsymutil"):
    path = TARGET / "bcl/hostmac" / name
    path.chmod(path.stat().st_mode | stat.S_IXUSR)
if not (TARGET / "hostmac").exists():
    (TARGET / "hostmac").symlink_to("bcl/hostmac", target_is_directory=True)
print(f"Local official Burst runtime prepared: {TARGET}")
print("The Unity installation was not modified. Set UNITY_BURST_RUNTIME_PATH to this path for builds.")
