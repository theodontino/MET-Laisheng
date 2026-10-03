# 来生殖民城市 · pre-alpha.3

本轮继续 MET 0.4 的单 Block 经营玩法；没有新增 District、交通模拟或跨版本存档迁移。城市定位为来生生态管理区内的开放聚落。外露设备、受控庭院和本土蕨类样植物是本轮美术表达，不是新增环境资源或生态模拟。

## 成长与美术

- T1：低矮模块、独立院落和外露服务设备。
- T2：永久建筑、连续沿街立面和共享庭院。
- T3：中层复合街区，少量较高核心；保留建筑功能差异和早期建筑。
- 全部21节点与24共用资源来自可复现 Blender 几何和材质，固定正交相机预渲染，Godot2D组合。没有使用其他游戏资产。
- 原相机投影、64世界单位建筑画幅及接地点保持一致。目录增加实际地面占地、连接位置和挂件位置，避免把屋顶设备算入地基。
- 样片通过专用测试状态展示同一12格位置的三阶段，不是声称自然经营已经达成该状态；玩法路线另行验证。

## 地基与跨格连接

连续高程场先派生自然地形，再在实际占地附近整平、过渡。地基、岸线与点击判定使用一致坐标。相邻建筑按类型、等级、地形与水体约束生成庭院、附房、店面或工业连接。装饰只读展示快照，不改实体、库存、产能、随机数。连接点击归属关联主建筑。

## 绘制与手机配置

- 镜头平移/缩放移动已有世界层；地形网格、地基、街区装饰和连接不随每次镜头变动重新派生。
- 地面、建筑与实时提示分层。手机地图使用独立 SubViewport，短边最高720；UI与提示保持原分辨率。
- 图片由Godot导入为压缩纹理并生成mipmap。精确点击轮廓预处理为一位alpha遮罩，避免运行时回读或解压GPU贴图。
- 自动帧率：桌面60、手机30；显示设置可选择30或60。字体缩放、减少动态及紧凑触摸布局继续可用。
- 预算仍为目标设备峰值内存600MB、纹理160MB、稳定30FPS。桌面和headless结果不能代替Galaxy A52 5G/iPhone13持续30分钟验收。

## 复现

```sh
# Blender脚本的 --tier-preview 不会覆盖运行时目录；--all重建全部资产。
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python tools/make_art040.py -- --all
python3 tools/prepare_world041.py
"$GODOT" --headless --path . --editor --import
export MET_EVIDENCE_DIR="$PWD/evidence041"
"$GODOT" --headless --path . --script tests/run040.gd
"$GODOT" --headless --path . --script tests/playthrough040.gd
"$GODOT" --headless --path . --script tests/fabric041.gd
"$GODOT" --headless --path . --script tests/fabric041.gd -- --mobile-quality
"$GODOT" --headless --path . --script tests/performance041.gd -- --output="$MET_EVIDENCE_DIR/performance041-headless.json"
"$GODOT" --path . -- --colony041
"$GODOT" --path . -- --visual040
"$GODOT" --path . -- --ui040
python3 tools/package_colony041.py --godot "$GODOT"
python3 tools/export_mobile041.py --godot "$GODOT"
python3 tools/package_colony041.py --source
```

移动端需要匹配Godot版本的导出模板；Android还需要配置SDK/JDK。iOS导出为未签名工程，默认LOCALONLY0仅是导出占位符。在Xcode中选择实际开发团队后才能签名安装，不含有效签名IPA。相关平台流程参见[Godot Android导出说明](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_android.html)及[iOS导出说明](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_ios.html)。

旧 Visual 包保留，新产物名含 Laisheng。本轮结果与未完成验收见 LAISHENG_ACCEPTANCE_041.md；旧版本报告不代表本轮结果。
