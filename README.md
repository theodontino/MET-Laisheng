# MET Beta.2 — UI Impacts V1 macOS app

[直接下载 ZIP](https://github.com/theodontino/MET-Laisheng/raw/refs/heads/downloads/macos-ui-impacts-v1-7e9e1a9/MET_0.5.0-beta.2_UIImpactsV1_macOS_universal_7e9e1a9.zip)

[GitHub 下载页面](https://github.com/theodontino/MET-Laisheng/blob/downloads/macos-ui-impacts-v1-7e9e1a9/MET_0.5.0-beta.2_UIImpactsV1_macOS_universal_7e9e1a9.zip)：登录有仓库权限的 GitHub 账号后，可在文件页点击 Download raw file。

解压后打开 `MET · 来生.app`。本次为 Godot 主线：整体 UI 80%/100%/120%，图形化建设前后供需、库存、电力与环境影响；`L` / `Shift+L` 循环地图图层。默认桌面全屏，最低1920×1080。

源码提交：`7e9e1a90a207039734d23fbcd79737f21213c6ff`，源码分支 `codex/met-beta-v3`。
包：`MET_0.5.0-beta.2_UIImpactsV1_macOS_universal_7e9e1a9.zip`，81,409,226 bytes。
SHA-256：`b141c2f0afd118324fee9d438290ed3a6e2360b1ffe9d2e8e82cdd7970688033`。

Godot 4.7.2 官方 macOS universal 模板新导出，含 Intel x86_64 与 Apple Silicon arm64。ZIP CRC、可执行权限、架构与签名结构通过；包内PCK在Linux完成默认启动与101项原生界面检查。源码核心401项、6条经营路线、新界面1040项及原有界面977项通过。

此包由Godot内置工具临时签名（ad-hoc），未Apple公证，未在macOS真机启动或验签。Linux软件绘制不能证明Mac GPU性能。若系统拦截，可在系统设置的隐私与安全性中核实来源后允许打开。

GitHub Release API当前返回Forbidden，因此沿用已有artifact-only下载分支交付；旧下载分支和文件保持原样。已推送唯一release tag `macos-0.5.0-beta.2-ui-impacts-v1` 触发已有macOS CI，但本环境无法观察Actions/Release结果，不能将tag push当成Release已发布。

校验与证据边界见 `VALIDATION.json`；源码中的 `docs/beta/UI_IMPACTS_ACCEPTANCE.md` 记录真实截图和验收。
