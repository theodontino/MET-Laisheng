#!/usr/bin/env python3
"""Write Godot unencrypted format-v2 PCK (source resources; no editor imports)."""
import hashlib,struct,sys
from pathlib import Path

def pack(root:Path,out:Path):
    files=[]
    for p in sorted(root.rglob('*')):
        if not any(x in {'evidence','evidence020','evidence021','evidence030','evidence031','evidence032','art_source','evidence_B_original','docs','build','dist'} for x in p.relative_to(root).parts) and p.is_file() and (not any(x.startswith('.') for x in p.relative_to(root).parts) or p.relative_to(root).as_posix()=='.godot/global_script_class_cache.cfg') and p.suffix in {'.gd','.godot','.tscn','.tres','.json','.cfg','.txt','.png','.webp','.svg','.wav','.gdshader'}:
            name=('res://'+p.relative_to(root).as_posix()).encode();name+=b'\0'*((-len(name))%4)
            data=p.read_bytes();files.append((name,data))
    directory_size=sum(4+len(n)+8+8+16+4 for n,b in files)
    base=(100+directory_size+15)//16*16
    header=struct.pack('<6IQ',0x43504447,2,4,7,2,0,base)+b'\0'*64+struct.pack('<I',len(files))
    directory=b'';payload=b''
    for n,b in files:
        directory+=struct.pack('<I',len(n))+n+struct.pack('<QQ',len(payload),len(b))+hashlib.md5(b).digest()+struct.pack('<I',0)
        payload+=b+b'\0'*((-len(b))%16)
    out.write_bytes(header+directory+b'\0'*(base-len(header)-len(directory))+payload)
    return len(files)
if __name__=='__main__':
    print(pack(Path(sys.argv[1]),Path(sys.argv[2])))
