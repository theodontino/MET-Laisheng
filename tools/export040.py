#!/usr/bin/env python3
"""Export the macOS app, include engine notices, and verify its ad-hoc signature."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
godot = os.environ.get("GODOT", "godot")
template = os.environ.get("MET_MACOS_TEMPLATE")
preset = root / "export_presets.cfg"
original = preset.read_text()
output = root / "build/MET_0.4.0_macOS.zip"
output.parent.mkdir(exist_ok=True)

try:
    if template:
        preset.write_text(original.replace('custom_template/release=""',
                          'custom_template/release="' + str(Path(template).resolve()) + '"'))
    subprocess.run([godot, "--headless", "--path", str(root),
                    "--export-release", "macOS", str(output)], check=True)
finally:
    if template:
        preset.write_text(original)

with tempfile.TemporaryDirectory(prefix="met040-export-") as staging:
    subprocess.run(["ditto", "-x", "-k", str(output), staging], check=True)
    app = next(Path(staging).glob("*.app"))
    resources = app / "Contents/Resources"
    for notice in ("GODOT-LICENSE.txt", "THIRD-PARTY-NOTICES.txt"):
        shutil.copy2(root / "docs" / notice, resources / notice)
    subprocess.run(["codesign", "--force", "--sign", "-",
                    "--preserve-metadata=identifier,entitlements,flags,runtime",
                    str(app)], check=True)
    subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
    packaged = Path(staging) / output.name
    subprocess.run(["ditto", "-c", "-k", "--norsrc", "--keepParent",
                    str(app), str(packaged)], check=True)
    shutil.copyfile(packaged, output)
print("Exported:", output)
