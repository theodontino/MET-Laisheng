#!/usr/bin/env python3
"""Build a standalone review gallery from verified native screenshots (no image edits)."""
import argparse
import base64
import html
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ui', type=Path, required=True)
    parser.add_argument('--maps', type=Path, required=True)
    parser.add_argument('--desks', type=Path)
    parser.add_argument('--beta-ui', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    verified = json.loads((args.ui / 'ui-reset-verification.json').read_text())
    if not verified['source_unchanged'] or verified['failed']:
        raise SystemExit('Gallery requires a passing, source-stable native UI run')

    def picture(path):
        raw = path.read_bytes()
        if raw[:8] != b'\x89PNG\r\n\x1a\n' or tuple(int.from_bytes(raw[n:n+4], 'big') for n in (16, 20)) != (1920, 1080):
            raise SystemExit(f'Not an original 1920x1080 PNG: {path}')
        return 'data:image/png;base64,' + base64.b64encode(raw).decode()

    maps = {}
    for count in (2, 12, 53):
        before, after = [json.loads((args.maps / f'{side}-{count}.json').read_text()) for side in ('before', 'after')]
        for key in ('building_count', 'seed', 'terrain_signature', 'pan', 'zoom', 'harness_sha256'):
            if before[key] != after[key]:
                raise SystemExit(f'Map comparison differs at {count}/{key}')
        for side, metadata in (('before', before), ('after', after)):
            if metadata['building_count'] != count or not metadata.get('native_fullscreen'):
                raise SystemExit(f'Invalid map fixture/fullscreen contract: {count}/{side}')
            if (metadata['image_width'], metadata['image_height']) != (1920, 1080):
                raise SystemExit(f'Invalid map framebuffer: {count}/{side}')
        maps[str(count)] = {side: picture(args.maps / f'{side}-{count}.png') for side in ('before', 'after')}
    scenes = [
        ('reset-01-city-overview', '城市总览', 'L0 只留 DT、人口、粮食与时间；地图拥有主要空间。'),
        ('reset-02-residence', '住宅', '入住、宜居与人口；完整生活来源进入详情。'),
        ('reset-02-production', '生产', '真实产出、运行率与瓶颈；一个升级主动作。'),
        ('reset-02-medical', '服务', '按实际服务池呈现，不套用工厂字段。'),
        ('reset-02-logistics', '货运站', '本地库存与市场入口；订单、运力与成交在档案中。'),
        ('reset-02-construction', '施工', '进度与有条件的预计时间；主动作是调整优先级。'),
        ('reset-02-paused', '暂停', '状态决定内容，主动作是恢复。'),
        ('reset-02-abnormal', '异常', '真实瓶颈决定处理入口，短缺不会伪装成可恢复的暂停。'),
        ('reset-05-theme-near-complete', '主题进度', '一张主题卡，按阶段与持续条件表达真实进度。'),
        ('reset-05c-severe-alerts', '严重警报', '最多两条，其他收进计数；点击定位问题来源。'),
        ('reset-06-local-site', '地图选址', '就地气泡跟随地块，展示有限预测与投标入口。'),
        ('reset-06b-operator-comparison', '运营商比较', 'L3 比较能力和成长槽位，一个采用并建设主动作。'),
        ('reset-04-building-archive', '建筑档案', '真实投入、运行与产出先呈现，完整来源和管理按需展开。'),
        ('reset-03-market', '市场工作台', '买卖报价和共享运力先呈现，订单与来源按步骤展开。'),
        ('reset-08-command-error-feedback', '操作反馈', '失败操作立即说明原因，不遮挡状态或主动作；城市名称与命名权保持不变。'),
    ]
    items = [dict(title=title, caption=caption, image=picture(args.ui / (name + '.png'))) for name, title, caption in scenes]
    checks, actions = verified['passed'], verified['native_input_actions']
    if args.desks:
        deep = json.loads((args.desks / 'desk-verification.json').read_text())
        if not deep['source_unchanged'] or deep['failed']:
            raise SystemExit('Deep-page gallery requires a passing, source-stable run')
        if deep['fingerprint'] != verified['fingerprint']:
            raise SystemExit('Main and deep-page evidence must use identical sources')
        checks += deep['passed']
        actions += deep['native_input_actions']
        deep_scenes = [
            ('archive-index', '档案入口', '九个主题以当前状态和图标导航，每张卡都有明确的目的地。'),
            ('resource-overview', '资源流向', '先看库存、实际供消和流向，再展开来源记录。'),
            ('resource-sources', '资源来源', '按实际贡献显示来源，完整精确账本仍可展开。'),
            ('population-overview', '居民与岗位', '住房和就业用容量表示，需求与住区关系进入矩阵。'),
            ('population-needs', '需求覆盖', '只呈现已启用的真实需求，未知收据显示等待结算。'),
            ('finance-overview', '经营预算', '收入、支出和余额分工明确；走势只用已有历史。'),
            ('finance-sources', '财政来源', '来源贡献用同一单位比较，避免长文逐行寻找。'),
            ('policy-options', '制度选项', '同槽互斥关系、现行制度和候选方案可比较。'),
            ('policy-preview', '制度影响', '实际城市预演、调整代价和确认动作各有位置。'),
            ('development', '建筑成长', '建筑图像、Tier轨道与能力帮助选择专业方向。'),
            ('upgrade-preview', '升级差分', '现在与升级后的能力并列，失去的能力也清楚呈现。'),
            ('construction', '共享施工', '共享能力、工程进度与优先级分开呈现。'),
            ('bonus-wallet', '副本与蓝图', '先选择副本，再选择合格宿主，最后确认安装。'),
            ('theme', '委任阶段', '阶段轨道、权威目标状态与真实持续达标时间。'),
            ('reward-three', '奖励比较', '已保存的候选横向比较，副本与研究含义明确。'),
            ('market', '市场报价', '先比较当前报价与真实运力，再设置买卖订单。'),
            ('market-order', '订单确认', '资源、方向、数量、限价及现金方向可以核对。'),
            ('building-overview', '建筑经营', '建筑身份与实际生产关系或住户容量先于完整账本。'),
            ('building-slots', '方案槽位', '槽位类型、已安装方案与可用部署入口可辨。'),
            ('building-sources', '建筑来源', '实际贡献引导阅读，Rule与Field细节按需展开。'),
        ]
        selected = [dict(title=title, caption=caption, image=picture(args.desks / ('desk-' + name + '.png')))
                    for name, title, caption in deep_scenes if (args.desks / ('desk-' + name + '.png')).is_file()]
        if len(selected) < 10:
            raise SystemExit('Need at least ten actual deep-page captures')
        items = selected + items
    if args.beta_ui:
        beta = json.loads((args.beta_ui / 'beta-verification.json').read_text())
        if not beta['source_unchanged'] or beta['failed'] or not beta['no_cli_display_override']:
            raise SystemExit('Beta-page gallery requires a passing default-startup run')
        if beta['fingerprint'] != verified['fingerprint']:
            raise SystemExit('Default-startup and UI evidence must use identical sources')
        beta_scenes = [
            ('beta-08-ending-unfinished', '城市尾声', '真实 DT 归零、一次命名权与本轮保存步骤。'),
            ('beta-09-ending-qualified', '定型建筑', '资格来自归零时保存的结果，挂牌效果可以核对。'),
            ('beta-10b-city-record', '城市留念', '保存的人口、资金、预算与构筑；完整历史可展开。'),
        ]
        extra = [dict(title=title, caption=caption, image=picture(args.beta_ui / (name + '.png')))
                 for name, title, caption in beta_scenes if (args.beta_ui / (name + '.png')).is_file()]
        if len(extra) != len(beta_scenes):
            raise SystemExit('Need all actual ending and city-record captures')
        items = items[:len(items) - len(scenes)] + extra + items[len(items) - len(scenes):]
    payload = json.dumps(dict(maps=maps, scenes=items), ensure_ascii=False).replace('</', '<' + chr(92) + '/')
    document = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>MET Beta.2 · 界面与地图重置</title><style>
*{box-sizing:border-box}body{margin:0;background:#17252b;color:#e7ecdf;font:16px/1.6 system-ui,sans-serif}main{max-width:1500px;margin:auto;padding:36px 32px 60px}h1{font-size:34px;margin:4px 0 10px}h2{font-size:23px;margin:34px 0 12px}p{max-width:1000px;color:#bdcec6}small{color:#acc1b6}button{background:#2e434b;color:#e7ecdf;border:1px solid #52696b;border-radius:6px;padding:9px 15px;font:inherit;cursor:pointer}button[aria-pressed=true]{background:#79c8b4;color:#152c31;border-color:#79c8b4}button:focus-visible,input:focus-visible{outline:3px solid #f2c776;outline-offset:3px}.tabs{display:flex;flex-wrap:wrap;gap:8px;margin:12px 0}.frame{position:relative;aspect-ratio:16/9;overflow:hidden;background:#0d191e;border:1px solid #536367;border-radius:8px}.frame img{position:absolute;inset:0;width:100%;height:100%;object-fit:contain}.label{position:absolute;top:12px;background:#17252bea;padding:5px 12px;border-radius:4px;font-size:14px}.old{left:12px}.new{right:12px}.line{position:absolute;top:0;bottom:0;width:2px;background:#e7ecdf;pointer-events:none}.range{display:flex;align-items:center;gap:16px;margin:12px 0}.range input{flex:1;accent-color:#79c8b4}.caption{min-height:2em}.facts{padding:16px 20px;border-left:3px solid #79c8b4;background:#20333b}a{color:#8ee2cb}footer{border-top:1px solid #40565b;margin-top:35px;padding-top:20px;color:#a8bdb5}
</style><main><small>MET · 来生 / 0.5.0-beta.2 / 2026-10-03</small><h1>让城市回到画面中心</h1>
<p>常驻状态、被动提醒、当前操作与深入研究各归其位。次级页面先呈现概况、关系和选择，再展开精确记录；图形全部来自实际城市状态。</p>
<div class="facts">原生全屏 1920×1080；@@CHECKS@@ 项 UI 检查通过，@@ACTIONS@@ 次鼠标或键盘输入，源码指纹稳定。全部画面来自显式测试场景，不是自然经营路线或真人试玩。Linux 软件渲染结果不能代表目标桌面 GPU。</div>
<h2>从概况，到比较与操作</h2><div class="tabs" id="scenes" aria-label="界面场景"></div><p class="caption" id="caption"></p><div class="frame"><img id="scene" alt=""></div>
<h2>同一座城，重置前后</h2><p>左右地图原图使用相同种子、建筑数、地貌、镜头和截图脚本。拖动分界查看，或用键盘调整滑杆。截图未作图像编辑。</p>
<div class="tabs" id="counts" aria-label="城市规模"></div><div class="frame"><img id="before" alt="重置前地图"><img id="after" alt="重置后地图"><div class="line" id="line"></div><span class="label old">重置前</span><span class="label new">Beta.2</span></div>
<label class="range">对照分界<input id="split" type="range" min="0" max="100" value="50" aria-label="地图前后对照分界"></label>
<footer>本轮只验收桌面全屏 1080p；移动端不在 pre-1.0 范围。原建筑资产继续复用，本轮完成地格绘制、建筑呈现尺度与连接模块的重置，未宣称逐栋重建 21 套模型。完整日志、JSON、原图与性能对比在配套 Evidence.zip。</footer></main>
<script type="application/json" id="data">@@DATA@@</script><script>
const data=JSON.parse(document.getElementById('data').textContent);const byId=id=>document.getElementById(id);
function buttons(parent,labels,select){labels.forEach((label,i)=>{const b=document.createElement('button');b.textContent=label;b.setAttribute('aria-pressed',i===0?'true':'false');b.onclick=()=>{[...parent.children].forEach(c=>c.setAttribute('aria-pressed','false'));b.setAttribute('aria-pressed','true');select(i)};parent.appendChild(b)})}
const counts=['2','12','53'];function map(i){byId('before').src=data.maps[counts[i]].before;byId('after').src=data.maps[counts[i]].after}
buttons(byId('counts'),counts.map(x=>x+' 栋建筑'),map);function split(){const n=byId('split').value;byId('after').style.clipPath='inset(0 0 0 '+n+'%)';byId('line').style.left=n+'%'}byId('split').oninput=split;
function scene(i){const item=data.scenes[i];byId('scene').src=item.image;byId('scene').alt=item.title;byId('caption').textContent=item.caption}buttons(byId('scenes'),data.scenes.map(x=>x.title),scene);map(0);split();scene(0);
</script></html>'''
    document = document.replace('@@CHECKS@@', html.escape(str(checks))).replace('@@ACTIONS@@', html.escape(str(actions))).replace('@@DATA@@', payload)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(document, encoding='utf8')
    print(json.dumps({'output': str(args.output), 'bytes': args.output.stat().st_size, 'screenshots': len(items) + 6}))


if __name__ == '__main__':
    main()
