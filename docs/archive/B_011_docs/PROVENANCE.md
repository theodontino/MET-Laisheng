# B 人口版来源与打包验证边界

## 唯一游戏代码来源

- 原 B 源码附件：MET_First_Block_源码与测试.zip
- SHA256：d2f0167b5ab0c8663156dbd038060800c23db35f36d2adeb2cbe4ac5dd4d6799
- 原 B Mac附件：MET_First_Block_macOS.zip
- SHA256：6813529d3258f2b31ae9f500041afb71708ed9dd3670a0fb356aff06fba5fa3d
- 原源码解压即做本地Git基线：476f1d674db9da689498f6655f8ada3dedb811fb
- 当前版本：0.1.1-b-population；直接修改原B文件，新增population.gd。没有使用C的model.gd，也没有修改远端GitHub。

`B_population_changes.patch` 记录相对于此基线的文本实现差异。历史B说明/测试另置archive；旧结果不能当成本版结果。

## 实际运行环境

此容器没有Godot编辑器，网络无法下载；没有因此另建网页版本。使用之前工作流下载并保留的Godot 4.7.2 Linux导出运行时执行同一组B脚本。运行时来自早前P0的引擎二进制，原游戏数据包被完整替换；这是复用引擎，不是复用P0游戏规则。

导出运行时不支持命令行切换main-pack，所以测试时按嵌入PCK格式替换数据包。包字段使文件偏移相对数据包起点。Linux原生实际执行记录在evidence，不是静态语法判断。

## Mac包

使用**原B Mac App的universal运行时**，由原B已有的tools/package_macos.py重新写入这次完整PCK、Info.plist、独立bundle ID及ad-hoc资源签名。原二进制代码页除签名区外保留。

脚本验证arm64/x86_64二进制结构、代码页SHA256及Info.plist/资源摘要；本次没有macOS执行环境，因此**不是Apple codesign验证，不是Gatekeeper公证，不是Mac启动通过**。没有用户证书、密钥、模型或字体文件。

原B已由用户运行过，但这不替代本次改动的Mac验收。需要Mac实测启动、Retina、键盘/触控板及长线保存。

正式后续开发可用4.7.2 Standard编辑器和官方macOS模板按export_presets.cfg导出，不需要依赖这次离线打包方式。
