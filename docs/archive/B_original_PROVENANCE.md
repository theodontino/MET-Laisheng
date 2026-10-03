> 历史档案：B 0.1.0-exp003 原文，已被 B 0.1.1 人口版决策覆盖。不得作为本次测试结果或当前玩法规格。

# 构建来源与许可

本次源代码与数据是EXP-003新玩法实现，保持与旧P0及效率工具存档隔离。没有向用户仓库提交或写入分支。

Godot运行模板复用授权连接读取的用户旧CI工件：

- repository: `theodontino/project-MET`
- branch: `spike/godot-unified-client`
- workflow run: `35336982077`
- artifact: `10543228032` (`MET-Wild-gameplay-3`)
- prior commit: `5f74c0123fe4caf9b87fda47189b707be2b5e310`
- artifact ZIP SHA256: `47ee7d860bc6a32bba3f59a3c691fbf06253a835d29fbaf5a4c1257ea939a963`
- runtime reports: `4.7.2.stable.official.ed1daf0bf`

旧工件仅用来取得匹配的运行模板；新PCK包含这次的场景、脚本、内容，不包含旧P0经济/时间/升级实现。

本次环境没有Godot编辑器。Godot官方`4.7.2-stable`的PCK读取代码已核对可读取未加密v2包；macOS官方签名实现用于核对Info.plist / CodeResources / CodeDirectory特殊槽含义。随包脚本记录了具体打包方法。

Godot MIT许可见`GODOT-LICENSE.txt`，运行时第三方版权见`THIRD-PARTY-NOTICES.txt`，两者从本次实际运行时导出。系统字体由Godot SystemFont在本机解析，没有复制分发系统字体文件。

macOS二进制具有x86_64与arm64两段。重新组装后使用ad-hoc代码签名；没有Apple Developer ID、Team ID、私钥、公证票据或伪造开发者身份。自有脚本只验证结构/摘要，不替代Apple代码签名验证。

官方继续开发参考：
`https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_macos.html`
`https://support.apple.com/102445`

交付SHA256在外层清单中，具体源码与PCK摘要在`evidence/`。不在文档中保存下载临时令牌或私有认证信息。
