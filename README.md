# MET Beta.2 — 设置入口修复 macOS app

[GitHub 下载页面](https://github.com/theodontino/MET-Laisheng/blob/downloads/macos-settings-fix-ad0372a/MET_0.5.0-beta.2_SettingsFix_macOS_universal_ad0372a.zip)：登录有仓库权限的 GitHub 账号后，点击 **Download raw file**。

请使用此新 ZIP 解压出的 `MET · 来生.app`。此包修复左下角齿轮点击时因焦点排版取消点击的问题，并修复打包后的手册图标加载。进入城市后，点击左下角齿轮 → 显示设置，可选择界面 80%／100%／120%；地图缩放独立。

源码提交：`ad0372a20b2153a3fc1e7e33f52eebd19511502e`，源码分支 `codex/met-beta-v3`。
文件：`MET_0.5.0-beta.2_SettingsFix_macOS_universal_ad0372a.zip`，81,410,127 bytes。
SHA-256：`c709ffbbc001b8ecda5549dbed77c04b4894193102920526913dfe4bdb13574b`。

Godot 4.7.2 官方 macOS universal 模板新导出，包含 Intel x86_64 与 Apple Silicon arm64。ZIP CRC、执行权限、架构与签名结构通过。固定 1920×1080 全屏的源码界面套件 1085 项通过；实际包内 PCK 的齿轮、手册、显示设置、界面缩放、关闭后重新进入等 118 项 Linux 原生检查通过，日志无脚本或资源错误。包内默认启动通过。

没有在 macOS 真机启动或验签，未 Apple 公证；Godot 内置工具为 ad-hoc 签名。Linux 软件绘制不能证明 Mac GPU 性能。若系统拦截，可在系统设置的隐私与安全性中核实来源后允许打开。

Release API 返回 Forbidden，因此通过独立下载分支交付。旧包、旧下载分支和 tag 保留。唯一 tag `macos-0.5.0-beta.2-settings-fix-ad0372a` 会触发既有 macOS CI；本环境不能观察其 Actions/Release 结果，tag push 不代表 Release 已发布。

实际验证记录见 `VALIDATION.json`；源码验收见 `docs/beta/SETTINGS_FIX_ACCEPTANCE.md`。
