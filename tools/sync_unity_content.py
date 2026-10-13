#!/usr/bin/env python3
"""Copy the source catalog and original licensed media into the Unity Player."""
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "unity/METLivingWorld/Assets"


def copy(source: Path, target: Path):
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists() or source.read_bytes() != target.read_bytes():
        shutil.copy2(source, target)
    assert source.read_bytes() == target.read_bytes(), target


def main():
    files = [ROOT / "content/core020.json"]
    files += sorted((ROOT / "content/040").glob("*.json"))
    files += sorted((ROOT / "content/beta").glob("*.json"))
    for source in files:
        copy(source, ASSETS / "StreamingAssets/MetContent" / source.relative_to(ROOT / "content"))
    for name in ("NotoSansSC-VF.ttf", "OFL-NotoSansSC.txt"):
        copy(ROOT / "assets/fonts" / name, ASSETS / "Resources/CityFonts" / name)
    for source in sorted((ROOT / "assets/audio030").glob("*.wav")):
        copy(source, ASSETS / "Resources/CityAudio" / source.name)
    notices = ASSETS / "StreamingAssets/ThirdPartyNotices"
    copy(ROOT / "assets/fonts/OFL-NotoSansSC.txt", notices / "NotoSansSC-OFL.txt")
    packages = ROOT / "unity/METLivingWorld/Packages"
    for name in ("LICENSE.md", "MET_PATCHES.md"):
        source = packages / "com.monitor1394.xcharts" / name
        if source.exists():
            copy(source, notices / ("XCharts-" + name))
    cache = ROOT / "unity/METLivingWorld/Library/PackageCache"
    for package in ("com.unity.cloud.gltfast", "com.unity.nuget.newtonsoft-json", "com.unity.render-pipelines.universal", "com.unity.render-pipelines.core", "com.unity.ugui"):
        for folder in sorted(cache.glob(package + "*")):
            for license_file in folder.glob("*LICENSE*"):
                if license_file.is_file() and not license_file.name.endswith(".meta"):
                    copy(license_file, notices / (package + "-" + license_file.name))
    print(f"Unity content synchronized: {len(files)} JSON files, licensed Chinese font, 6 original sounds.")


if __name__ == "__main__":
    main()
