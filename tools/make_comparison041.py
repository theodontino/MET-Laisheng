#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Embed unmodified engine captures in a standalone before/after review page."""
from pathlib import Path
import base64
import json

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / 'evidence041'

def data(name):
    return 'data:image/png;base64,' + base64.b64encode((EVIDENCE/name).read_bytes()).decode()

def main():
    images = {
        'early':data('colony041-02-tier1.png'),
        'town':data('colony041-03-tier2.png'),
        'city':data('colony041-04-tier3.png'),
        'before':data('colony041-before-tier3.png')
    }
    html = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>MET 来生 · 三阶段与同城对照</title><style>
:root{color-scheme:dark;font:16px/1.7 system-ui,-apple-system,sans-serif;background:#182125;color:#f1e8d8}*{box-sizing:border-box}body{max-width:1480px;margin:auto;padding:32px 24px}h1{font-size:32px;line-height:1.25;margin:8px 0 16px}h2{font-size:22px;margin-top:40px}p{max-width:900px;color:#b9cac7}.eyebrow{color:#8ad5c0;letter-spacing:.15em;font-size:13px}button{font:inherit;border:1px solid #587178;background:#29363b;color:inherit;padding:10px 22px;border-radius:6px;cursor:pointer;margin:0 8px 12px 0}button[aria-pressed=true]{background:#79c9b4;color:#152d29;border-color:#79c9b4}button:focus-visible{outline:3px solid #8ad5c0;outline-offset:3px}img{display:block;width:100%;height:auto;border-radius:8px}.caption{margin:12px 0 24px}.compare{position:relative;overflow:hidden;border-radius:8px;aspect-ratio:1.6}.compare img{position:absolute;inset:0;width:100%;height:100%;object-fit:contain;border-radius:0}.compare .old{clip-path:inset(0 50% 0 0)}.line{position:absolute;top:0;bottom:0;left:50%;width:2px;background:#f1e8d8;pointer-events:none}.tag{position:absolute;top:12px;background:#182125db;padding:5px 12px;border-radius:4px}.tag.before{left:12px}.tag.after{right:12px}input{width:100%;accent-color:#79c9b4;margin-top:18px}footer{border-top:1px solid #3b4d52;margin-top:36px;padding-top:20px;color:#b9cac7;font-size:14px}
</style><div class="eyebrow">MET / LAISHENG / PRE-ALPHA.3</div><h1>让聚落长成城市</h1><p>生态管理区内的开放城市。先遣模块逐渐发展为永久街巷和都市街区；城外仍保留来生的本土生态。</p><nav aria-label="成长阶段"><button data-stage="early" aria-pressed="true">01 先遣聚落</button><button data-stage="town" aria-pressed="false">02 定居小镇</button><button data-stage="city" aria-pressed="false">03 都市化街区</button></nav><img id="stage" alt="同一12格街景的先遣聚落"><p id="caption" class="caption"></p><h2>同一城镇状态 · 新旧对照</h2><p>左右拖动分界线，比较相同种子、12个位置与建筑定义下的成熟街景。截图来自旧版本展示代码和本轮展示代码；游戏状态相同，镜头适配可能因新模型高度略有差别。</p><div class="compare"><img id="after" alt="来生版成熟街景"><img id="before" class="old" alt="旧版相同城镇状态"><div class="line"></div><span class="tag before">旧版</span><span class="tag after">来生版</span></div><label for="split">对照分界</label><input id="split" type="range" min="0" max="100" value="50"><footer>这些是引擎内的视觉测试状态：通过专用夹具直接构造三阶段，用于审查外观，不是自然经营通关证据。功能路线、性能及各平台验证分别记录在 LAISHENG_ACCEPTANCE_041.md。</footer><script>
const images=IMAGES;
const captions={early:'低矮模块、独立院落与外露服务设备，为后续建设留出空间。',town:'永久建筑形成连续街面，功能设施逐渐专业化。',city:'中层街区、公共核心与工业组团共同组成城市；邻接附属建筑仅作视觉连接。'};
const labels={early:'先遣聚落',town:'定居小镇',city:'都市化街区'};
function select(key){document.querySelector('#stage').src=images[key];document.querySelector('#stage').alt='同一12格街景的'+labels[key];document.querySelector('#caption').textContent=captions[key];document.querySelectorAll('button').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.stage===key)));}
document.querySelectorAll('button').forEach(b=>b.addEventListener('click',()=>select(b.dataset.stage)));
document.querySelector('#before').src=images.before;document.querySelector('#after').src=images.city;select('early');document.querySelector('#split').addEventListener('input',e=>{const v=Number(e.target.value);document.querySelector('#before').style.clipPath=`inset(0 ${100-v}% 0 0)`;document.querySelector('.line').style.left=v+'%';});
</script></html>'''.replace('IMAGES', json.dumps(images))
    output=ROOT/'docs/v040/LAISHENG_COMPARISON_041.html'
    output.write_text(html)
    print(output)

if __name__ == '__main__':main()
