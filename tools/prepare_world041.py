#!/usr/bin/env python3
"""Prepare exact one-bit picking masks and compressed mipmapped texture imports.
Run after Blender asset generation, before Godot import/export. No image content is changed.
"""
from pathlib import Path
from PIL import Image
import struct

ROOT = Path(__file__).resolve().parents[1]

def main():
    count = 0
    total = 0
    for path in sorted((ROOT / 'assets/world040').rglob('*.png')):
        with Image.open(path) as source:
            alpha = source.convert('RGBA').getchannel('A')
            width, height = alpha.size
            packed = bytearray((width * height + 7) // 8)
            for index, value in enumerate(alpha.getdata()):
                if value > 76:
                    packed[index >> 3] |= 1 << (index & 7)
        mask = struct.pack('<II', width, height) + packed
        path.with_suffix('.mask').write_bytes(mask)
        total += len(mask)
        count += 1
    for folder in ['assets/world040', 'assets/world030']:
        for path in (ROOT / folder).rglob('*.png.import'):
            text = path.read_text().replace('compress/mode=0', 'compress/mode=2').replace('mipmaps/generate=false', 'mipmaps/generate=true')
            path.write_text(text)
    print(f'Prepared {count} exact alpha picking masks ({total / 1048576:.2f} MiB); imports use VRAM compression and mipmaps.')

if __name__ == '__main__':
    main()
