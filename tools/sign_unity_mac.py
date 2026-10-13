#!/usr/bin/env python3
"""Ad-hoc sign this project's exported app from nested native code outward."""
from pathlib import Path
import subprocess
import sys

app = Path(sys.argv[1] if len(sys.argv) > 1 else "build/MET Laisheng Unity.app")
magic = {bytes.fromhex(x) for x in ("feedface", "cefaedfe", "feedfacf", "cffaedfe", "cafebabe", "bebafeca", "cafebabf", "bfbafeca")}
native = []
for path in app.rglob("*"):
    if path.is_file() and not path.is_symlink() and path.parent.name != "MacOS":
        with path.open("rb") as stream:
            if stream.read(4) in magic:
                native.append(path)
for path in sorted(native, key=lambda p: len(p.parts), reverse=True):
    # Fresh requirements are necessary: old SDK ad-hoc requirements name its old code.
    subprocess.run(["codesign", "--force", "--sign", "-", "--timestamp=none", str(path)], check=True)
for path in sorted(app.rglob("*.framework"), key=lambda p: len(p.parts), reverse=True):
    subprocess.run(["codesign", "--force", "--sign", "-", "--timestamp=none", str(path)], check=True)
subprocess.run(["codesign", "--force", "--sign", "-", "--timestamp=none", str(app)], check=True)
subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
print("Ad-hoc signature verified. This is not Developer ID signing or notarization.")
