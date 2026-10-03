#!/usr/bin/env python3
"""Reuse a matching exported Linux Godot engine, replacing ALL previous game data.
Not an editor or export-template downloader. Expects a format-v2 source PCK.
"""
import argparse, struct
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--runtime',type=Path,required=True);p.add_argument('--pck',type=Path,required=True);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
b=a.runtime.read_bytes()
if b[-4:]!=b'GDPC':raise SystemExit('Runtime has no trailing embedded PCK; use the Godot editor instead.')
length=struct.unpack('<Q',b[-12:-4])[0];start=len(b)-12-length
if start<0 or b[start:start+4]!=b'GDPC':raise SystemExit('Embedded PCK boundary is invalid.')
data=bytearray(a.pck.read_bytes())
if data[:4]!=b'GDPC' or struct.unpack_from('<I',data,4)[0]!=2:raise SystemExit('A Godot format-v2 PCK is required.')
struct.pack_into('<I',data,20,2)  # Embedded file offsets relative to PCK base.
a.out.write_bytes(b[:start]+data+struct.pack('<Q',len(data))+b'GDPC');a.out.chmod(0o755)
print(a.out)
