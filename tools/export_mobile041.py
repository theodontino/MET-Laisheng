#!/usr/bin/env python3
"""Export an Android debug APK and an unsigned iOS test project.
Requires Godot 4.7.2 templates and Android SDK/JDK configured in the chosen editor.
Without --team-id, iOS uses a clearly local-only placeholder; select your team in Xcode before device signing.
"""
import argparse
import os
from pathlib import Path
import subprocess
import shutil

ROOT = Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', default=os.environ.get('GODOT', 'godot'))
    parser.add_argument('--platform', choices=['all', 'android', 'ios'], default='all')
    parser.add_argument('--team-id', default='')
    args = parser.parse_args()
    build = ROOT / 'build'
    build.mkdir(exist_ok=True)
    if args.platform in ['all', 'android']:
        subprocess.run([args.godot, '--headless', '--path', str(ROOT), '--export-debug', 'Android', str(build / 'MET_0.4.0_Laisheng_Android.apk')], check=True)
    if args.platform in ['all', 'ios']:
        output = build / 'MET_0.4.0_Laisheng_iOS'
        output.mkdir(exist_ok=True)
        presets = ROOT / 'export_presets.cfg'
        original = presets.read_text()
        team_id = args.team_id or 'LOCALONLY0'
        if len(team_id) != 10 or not team_id.isalnum():
            raise ValueError('Apple team id must contain 10 letters or digits.')
        try:
            presets.write_text(original.replace('application/app_store_team_id=""', f'application/app_store_team_id="{team_id}"'))
            subprocess.run([args.godot, '--headless', '--path', str(ROOT), '--export-debug', 'iOS', str(output / 'METLaisheng.zip')], check=True)
        finally:
            presets.write_text(original)
        (output / 'README.txt').write_text('MET 来生 iOS 测试工程\n\n这是未签名工程，不是可直接安装的 IPA。\n用 Xcode 打开同目录的 METLaisheng.xcodeproj。\n在 Signing & Capabilities 选择自己的开发团队和目标 iPhone 后运行。\nLOCALONLY0 是仅用于导出工程的占位符，不是有效 Apple 开发者团队。\n真机触控、性能、温升与稳定性仍须另行记录。\n', encoding='utf-8')
        shutil.make_archive(str(build / 'MET_0.4.0_Laisheng_iOS'), 'zip', build, output.name)
    print('Mobile exports completed. Package generation does not establish device acceptance.')

if __name__ == '__main__':
    main()
