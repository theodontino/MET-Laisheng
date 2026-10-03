# 0.2.1 来源与交付边界

唯一游戏代码基线：当前用户交付的 **MET_B_0.2.0_BuildLab_Source.zip**。

- ZIP SHA256：`ad0d2ac4a4de97da3557cd51b11b03667bae572e73f2b75a5aab9d7d7b9f9dbc`
- 本地原样基线提交：`fbbd09b60dac5e8aea03ef25aae6b6926d839155`
- 来源为B0.1.1→B0.2.0已有工程，不是另一个C或HTML重写。
- 0.2.0之前的历史文档/测试保留；当前只引用evidence021。
- 本次没有GitHub写操作、没有远端Release、没有在用户电脑执行命令。

## 引擎和Mac应用

原生Linux引擎复用已在对话中取回的Godot 4.7.2导出运行时，旧游戏数据包被完整替换。其原始归档为MET_engine_template_artifact.zip，SHA256 `30abbcc0a750fbc655ba5390a226dc5d30104da1f8f891e4ee8513666ddd0af9`。

Mac使用**当前0.2.0 Mac包**内的universal引擎，替换本轮PCK和应用信息，以MET River Valley.app独立交付。package_macos.py重写ad-hoc代码目录与资源摘要，自己复核各架构代码页/资源一致性。

**没有Mac运行环境：未在Mac启动，未执行Apple codesign/spctl，未验证Retina与触控板实机，没有Developer ID公证。** 离线结构校验不代表这些平台验证成功。

实际执行测试使用最终Mac PCK中的相同脚本/内容，放入Linux同版引擎；嵌入时只修改PCK基址标志。没有另用Python重写一个经济模型充当游戏测试。

正常后续开发有Godot4.7.2编辑器和官方模板时，可直接使用export_presets.cfg导出，无需依赖此离线拼包方式。没有打包任何字体文件；运行使用系统中文字体回退。
