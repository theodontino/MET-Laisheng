# 0.3.1 制作与来源

- 唯一基线：用户的 `MET_0.3.0_CityView_Source.zip`，SHA256 `8aa876daea4571d50dbfd0c1e8eceba08c956a06de3a97bd049fd83843121b95`。
- 规范包：`MET_0.3.1_UI分层与卡牌化_开发规格包.zip`，SHA256 `3c01d8ba3b5691f96fb90baf35f302e65b12ff0b19bc80bc56334bf4630b708c`。
- 修改范围：原生UI、显示配置、测试/打包与独立显示设置。核心模拟和经济数据不变。
- 新主题纹样、插口轮廓和卡片布局通过Godot代码绘制；图标和建筑精灵继续使用0.3.0原有可复现素材，无新外部资产。正文使用SystemFont，包中不分发字体文件。
- 运行时：从既有匹配Godot引擎包获取 `4.7.2.stable.official.ed1daf0bf`；没有换成其他模型或Python模拟经济。
- 本地没有Godot编辑器和Apple签名环境；`tools/pack_runtime.py` 打出源资源PCK；`tools/repack_export_runtime.py` 载入匹配Linux导出运行时执行实际游戏；`tools/package_macos.py` 使用原0.3.0 universal engine模板替换游戏资源与bundle元数据，重建ad-hoc摘要。
- 此方法不是Apple签名/公证/真机启动成功的保证。对应边界必须随Mac候选包一起保留。正常后续开发优先用匹配官方导出模板。
- `bundle-verification031.json`、`source-consistency031.json`记录同包资源与代码页核对；UI截图来自实际Godot帧缓冲，不是HTML规格截图。
- 未发布GitHub、商店，也没有对用户设备进行远程改动。
