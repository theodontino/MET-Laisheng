#!/usr/bin/env python3
"""Build a macOS universal .app using a matching archived Godot 4.7.2 universal engine template.

No Developer ID certificate is used. Replaces the original template's signature
with an ad-hoc SHA-256 CodeDirectory, sealing this bundle's Info.plist/resources.
This performs structural/hash verification, NOT Apple's codesign/spctl validation.
Normal development should use Godot's export preset (see README).
"""
from __future__ import annotations
import argparse, hashlib, json, math, plistlib, shutil, stat, struct
from pathlib import Path
from pack_runtime import pack

IDENTITY='com.projectmet.citydesk032'
NAME='MET City Desk'

def sha(data:bytes)->bytes:return hashlib.sha256(data).digest()
def be32(b:bytes,o:int)->int:return struct.unpack_from('>I',b,o)[0]
def architectures(blob:bytes):
    if be32(blob,0)!=0xcafebabe:raise ValueError('Expected a universal Mach-O')
    for i in range(be32(blob,4)):
        cpu,sub,offset,size,align=struct.unpack_from('>5I',blob,8+i*20)
        thin=blob[offset:offset+size]
        if struct.unpack_from('<I',thin,0)[0]!=0xfeedfacf:raise ValueError('Expected little endian Mach-O 64')
        pos=32;sig=None
        for _ in range(struct.unpack_from('<I',thin,16)[0]):
            command,length=struct.unpack_from('<II',thin,pos)
            if command==0x1d:sig=struct.unpack_from('<II',thin,pos+8)
            pos+=length
        if sig is None:raise ValueError('Template has no reserved code signature')
        yield cpu,offset,size,sig

def blobs(sig:bytes):
    if be32(sig,0)!=0xfade0cc0:raise ValueError('Not a signature SuperBlob')
    return {be32(sig,12+i*8):sig[(o:=be32(sig,16+i*8)):o+be32(sig,o+4)] for i in range(be32(sig,8))}

def resources(contents:Path)->bytes:
    files={};files2={}
    for path in sorted((contents/'Resources').rglob('*')):
        if path.is_file():
            relative=path.relative_to(contents).as_posix();data=path.read_bytes()
            files[relative]=hashlib.sha1(data).digest()
            files2[relative]={'hash':hashlib.sha1(data).digest(),'hash2':sha(data)}
    rules={'^Resources($|/)':True,'^Resources/.*\\.lproj/':{'optional':True,'weight':1000},'^Resources/.*\\.lproj/locversion.plist$':{'omit':True,'weight':1100},'^Resources/Base\\.lproj/':{'weight':1010},'^version.plist$':True}
    rules2={'.*\\.dSYM($|/)':{'weight':11},'^(.*/)?\\.DS_Store$':{'omit':True,'weight':2000},'^(Frameworks|SharedFrameworks|PlugIns|Plug-ins|XPCServices|Helpers|MacOS|Library/(Automator|Spotlight|LoginItems))/':{'nested':True,'weight':10},'^.*':True,'^Info\\.plist$':{'omit':True,'weight':20},'^PkgInfo$':{'omit':True,'weight':20},'^Resources($|/)':{'weight':20},'^[^/]+$':{'nested':True,'weight':10},'^Resources/.*\\.lproj/':{'optional':True,'weight':1000},'^Resources/.*\\.lproj/locversion.plist$':{'omit':True,'weight':1100},'^Resources/Base\\.lproj/':{'weight':1010},'^version\\.plist$':{'weight':20}}
    return plistlib.dumps({'files':files,'files2':files2,'rules':rules,'rules2':rules2},sort_keys=True)

def sign(contents:Path,exe:Path)->dict:
    seal=resources(contents)
    (contents/'_CodeSignature').mkdir(exist_ok=True)
    (contents/'_CodeSignature'/'CodeResources').write_bytes(seal)
    info=(contents/'Info.plist').read_bytes()
    requirements=struct.pack('>III',0xfade0c01,12,0)
    signature=struct.pack('>II',0xfade0b01,8) # empty CMS, ad-hoc only
    binary=bytearray(exe.read_bytes());out=[]
    for cpu,offset,size,(dataoff,datasize) in architectures(binary):
        thin=bytes(binary[offset:offset+size]);old=blobs(thin[dataoff:dataoff+datasize])[0]
        if be32(old,8)!=0x20400 or old[36:40]!=bytes([32,2,0,12]):raise ValueError('Unexpected template CodeDirectory')
        limit=be32(old,32);pages=be32(old,28)
        if limit!=dataoff or pages!=math.ceil(limit/4096):raise ValueError('Unexpected code extent')
        identifier=IDENTITY.encode()+b'\0'
        special=b''.join([sha(seal),sha(requirements),sha(info)])
        codehashes=b''.join(sha(thin[i*4096:min((i+1)*4096,limit)]) for i in range(pages))
        header=bytearray(old[:88]);hashoffset=88+len(identifier)+len(special)
        struct.pack_into('>I',header,4,hashoffset+len(codehashes))
        struct.pack_into('>I',header,12,2) # CS_ADHOC
        struct.pack_into('>I',header,16,hashoffset)
        struct.pack_into('>I',header,20,88)
        struct.pack_into('>I',header,24,3)
        struct.pack_into('>I',header,48,0) # no Apple team ID in an ad-hoc signature
        cd=bytes(header)+identifier+special+codehashes
        entries=[(0,cd),(2,requirements),(0x10000,signature)]
        index=b'';payload=b'';start=12+8*len(entries)
        for kind,data in entries:
            index+=struct.pack('>II',kind,start+len(payload));payload+=data
        superblob=struct.pack('>III',0xfade0cc0,start+len(payload),len(entries))+index+payload
        if len(superblob)>datasize:raise ValueError('New signature does not fit template reservation')
        binary[offset+dataoff:offset+dataoff+datasize]=superblob+b'\0'*(datasize-len(superblob))
        out.append({'architecture':{0x1000007:'x86_64',0x100000c:'arm64'}[cpu],'code_pages':pages,'code_bytes':limit,'signature_type':'ad-hoc SHA-256; no Developer ID, no notarization'})
    exe.write_bytes(binary);exe.chmod(0o755)
    report=verify(contents,exe);report['architectures']=out
    return report

def verify(contents:Path,exe:Path)->dict:
    info=(contents/'Info.plist').read_bytes();seal=(contents/'_CodeSignature'/'CodeResources').read_bytes()
    sealobj=plistlib.loads(seal);resource_count=0;pages_checked=0
    for relative,record in sealobj['files2'].items():
        assert sha((contents/relative).read_bytes())==record['hash2'],relative
        resource_count+=1
    binary=exe.read_bytes()
    for _,off,size,(dof,ds) in architectures(binary):
        thin=binary[off:off+size];items=blobs(thin[dof:dof+ds]);cd=items[0]
        assert be32(cd,12)==2 and be32(cd,48)==0
        ident=cd[be32(cd,20):].split(b'\0')[0].decode();assert ident==IDENTITY
        ho=be32(cd,16);n=be32(cd,28);limit=be32(cd,32)
        assert cd[ho-32:ho]==sha(info)
        assert cd[ho-64:ho-32]==sha(items[2])
        assert cd[ho-96:ho-64]==sha(seal)
        for k in range(n):assert cd[ho+k*32:ho+(k+1)*32]==sha(thin[k*4096:min((k+1)*4096,limit)])
        pages_checked+=n
    return {'bundle_resource_hashes_verified':resource_count,'mach_o_code_pages_verified':pages_checked,'apple_codesign_verified':False,'macos_launch_verified':False,'binary_sha256':hashlib.sha256(binary).hexdigest()}

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',required=True,type=Path);ap.add_argument('--template-app',required=True,type=Path);ap.add_argument('--output',required=True,type=Path);args=ap.parse_args()
    app=args.output/(NAME+'.app');contents=app/'Contents'
    if app.exists():shutil.rmtree(app)
    (contents/'MacOS').mkdir(parents=True);(contents/'Resources').mkdir()
    old=args.template_app/'Contents';oldplist=plistlib.loads((old/'Info.plist').read_bytes())
    exe=contents/'MacOS'/NAME
    shutil.copyfile(old/'MacOS'/oldplist['CFBundleExecutable'],exe)
    info=oldplist.copy();info.update({'CFBundleName':NAME,'CFBundleDisplayName':'MET · 城市工作台','CFBundleExecutable':NAME,'CFBundleIdentifier':IDENTITY,'CFBundleShortVersionString':'0.3.2','CFBundleVersion':'320','NSHumanReadableCopyright':'Project MET — City Desk 0.3.2. Includes Godot Engine (MIT).','LSApplicationCategoryType':'public.app-category.simulation-games','CFBundleIconFile':'icon.icns'})
    (contents/'Info.plist').write_bytes(plistlib.dumps(info,sort_keys=True));(contents/'PkgInfo').write_bytes(b'APPL????')
    shutil.copyfile(args.source/'assets'/'icon.icns',contents/'Resources'/'icon.icns')
    privacy=old/'Resources'/'PrivacyInfo.xcprivacy'
    if privacy.exists():shutil.copyfile(privacy,contents/'Resources'/privacy.name)
    for name in ['GODOT-LICENSE.txt','THIRD-PARTY-NOTICES.txt']:
        shutil.copyfile(args.source/'docs'/name,contents/'Resources'/name)
    count=pack(args.source,contents/'Resources'/(NAME+'.pck'))
    report=sign(contents,exe)
    report.update({'engine':'4.7.2.stable.official.ed1daf0bf','pck_files':count,'pck_sha256':hashlib.sha256((contents/'Resources'/(NAME+'.pck')).read_bytes()).hexdigest(),'pack_method':'Godot source-resource PCK v2 + existing 4.7.2 universal export template; no editor installed in build environment'})
    args.output.mkdir(parents=True,exist_ok=True)
    (args.output/'bundle-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
