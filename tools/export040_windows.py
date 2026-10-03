#!/usr/bin/env python3
"""Build a Windows x64 ZIP from Godot's matching export template."""
import os
from pathlib import Path
import subprocess
import tempfile
import zipfile

root = Path(__file__).resolve().parents[1]
godot = os.environ.get("GODOT", "godot")
template = os.environ.get("MET_WINDOWS_TEMPLATE")
preset = root / "export_presets.cfg"
original = preset.read_text()
anchor = '[preset.1.options]\ncustom_template/debug=""\ncustom_template/release=""'
output = root / "build/MET_0.4.0_Windows_x64.zip"
output.parent.mkdir(exist_ok=True)

with tempfile.TemporaryDirectory(prefix="met040-windows-") as staging:
    exe = Path(staging) / "MET River Valley.exe"
    try:
        if template:
            assert anchor in original
            preset.write_text(original.replace(anchor, anchor[:-1] + str(Path(template).resolve()) + '"'))
        subprocess.run([godot, "--headless", "--path", str(root),
                        "--export-release", "Windows Desktop", str(exe)], check=True)
    finally:
        if template:
            preset.write_text(original)

    pack = exe.with_suffix(".pck")
    assert exe.is_file() and pack.is_file(), "Godot did not export both EXE and PCK"
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for item in (exe, pack):
            archive.write(item, Path("MET_0.4.0_Windows_x64") / item.name)
        for notice in ("GODOT-LICENSE.txt", "THIRD-PARTY-NOTICES.txt"):
            archive.write(root / "docs" / notice, Path("MET_0.4.0_Windows_x64") / notice)
        archive.writestr("MET_0.4.0_Windows_x64/README.txt",
                         "MET 0.4.0 pre-alpha · Windows x64\n"
                         "请先解压整个文件夹，再运行 MET River Valley.exe。\n"
                         "EXE 与同名 PCK 必须放在同一文件夹。\n"
                         "整个 pre-alpha 阶段不兼容其他版本存档。\n")
print("Exported:", output)
