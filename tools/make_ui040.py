from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets" / "ui030"
OUTPUT = ROOT / "assets" / "ui040"
OUTPUT.mkdir(parents=True, exist_ok=True)

ICON_COLOR = "#75D7BE"
INK_COLOR = "#EDF3EF"
EXTRA = {
    "info": "M16 14v8 M16 9h.1 M4 16a12 12 0 1 0 24 0a12 12 0 1 0-24 0",
    "search": "M20 20l8 8 M14 24a10 10 0 1 0 0-20a10 10 0 1 0 0 20",
    "plus": "M16 5v22 M5 16h22",
    "minus": "M5 16h22",
    "refresh": "M25 11a10 10 0 1 0 2 9 M25 4v8h-8",
    "external": "M18 5h9v9 M27 5 14 18 M24 18v8H6V8h8",
    "sort": "M8 6v20 M4 10l4-4 4 4 M20 26V6 M16 22l4 4 4-4",
    "compare": "M5 7h22v18H5Z M11 12h10 M11 17h6 M11 22h8",
}

for source in sorted(SOURCE.glob("*.svg")):
    if source.stem.startswith("org_"):
        markup = source.read_text(encoding="utf-8")
        markup = markup.replace("#fff9e9", INK_COLOR).replace("#FFF9E9", INK_COLOR)
        (OUTPUT / source.name).write_text(markup, encoding="utf-8")
        continue
    match = re.search(r'<path\s+d="([^"]+)"', source.read_text(encoding="utf-8"))
    if not match:
        continue
    path_data = match.group(1)
    markup = (
        '<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" '
        'viewBox="0 0 32 32" fill="none">'
        f'<path d="{path_data}" stroke="{ICON_COLOR}" stroke-width="1.8" '
        'stroke-linecap="round" stroke-linejoin="round"/></svg>\n'
    )
    (OUTPUT / source.name).write_text(markup, encoding="utf-8")

for icon_id, path_data in EXTRA.items():
    markup = (
        '<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" '
        'viewBox="0 0 32 32" fill="none">'
        f'<path d="{path_data}" stroke="{ICON_COLOR}" stroke-width="1.8" '
        'stroke-linecap="round" stroke-linejoin="round"/></svg>\n'
    )
    (OUTPUT / f"{icon_id}.svg").write_text(markup, encoding="utf-8")

print(f"Generated {len(list(OUTPUT.glob('*.svg')))} unified SVG icons in {OUTPUT.relative_to(ROOT)}")
