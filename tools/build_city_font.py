#!/usr/bin/env python3
"""Build the OFL Noto Sans SC regular instance used by Unity's legacy Text.

Requires fonttools. Run: python3 tools/build_city_font.py
The upstream variable font defaults to weight 100; retaining it in Resources
also packages every weight. The original and its license remain in assets/fonts.
"""
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

root = Path(__file__).resolve().parents[1]
source = root / "assets/fonts/NotoSansSC-VF.ttf"
output = root / "unity/METLivingWorld/Assets/Resources/CityFonts/NotoSansSC.ttf"
font = TTFont(source)
regular = instantiateVariableFont(font, {"wght": 400}, inplace=False)
regular.save(output)
print(f"Regular weight 400: {output.stat().st_size:,} bytes; source: {source.stat().st_size:,} bytes")
