#!/usr/bin/env python3
"""Export the Laisheng edition without replacing the earlier Visual packages."""
import package_visual040 as desktop
import package_source040 as source
import sys

if "--source" in sys.argv:
    source.OUTPUT = source.ROOT / "build/MET_0.4.0_Laisheng_Source.zip"
    source.ARCHIVE_ROOT = "MET_0.4.0_Laisheng_Source"
    source.EVIDENCE_DIR = source.Path("evidence041")
    print("Created", source.create_archive())
else:
    desktop.BUNDLE_VERSION = "403"
    desktop.MAC_APP_NAME = "MET · 来生.app"
    desktop.MAC_ZIP_NAME = "MET_0.4.0_Laisheng_macOS.zip"
    desktop.WINDOWS_ROOT = "MET_0.4.0_Laisheng_Windows_x64"
    desktop.WINDOWS_ZIP_NAME = desktop.WINDOWS_ROOT + ".zip"
    desktop.main()
