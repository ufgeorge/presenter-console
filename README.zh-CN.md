# Presenter Console — 手机简报中控台

[English](README.en.md) · **简体中文** · [繁體中文](README.md)

把手机变成简报讲者的中控台：换页、开始简报、同步显示讲者备注、语音备注播报、观众提问 — 全部通过手机浏览器完成，**手机零安装 App**。

- 🎯 支持 **PowerPoint** 与 **Open Design** 两种简报来源
- 📱 手机不用装 App：PWA 网页、扫码即配对
- 🌐 同一 WiFi / 手机热点即可使用，不需要云端服务器（LAN-first）
- 🌏 三语界面：简体中文 / 繁體中文 / English

## 功能特色

| 功能 | 说明 |
|---|---|
| 换页控制 | NEXT / PREV、开始简报、从当前页开始、一键回到简报 |
| 讲者备注同步 | 手机实时显示当前页的 Notes（以 Agent 实测页码为准） |
| 语音备注 | Notes 写 `[voice]文字 [2 sec]` 语法，手机自动 TTS 播报（讲者戴耳机听，最不干扰） |
| 影片播放 | Notes 写 `<video>文件名</video>`（与简报同一文件夹），手机出现「播放影片」按钮，系统播放器前台播放，可暂停 / 继续 |
| 多简报选择 | 同时打开多个 PowerPoint 文件 / Open Design 项目，手机下拉切换 |
| 观众提问（Audience Q&A） | 放映中手机显示提问二维码，观众用浏览器送问题，控制端查看 / 删除 |
| 屏幕不休眠 | Wake Lock（secure context）+ NoSleep.js fallback |
| 三语界面 | 简体中文 / 繁體中文 / English，随手机语言自动切换 |

## 系统需求

- **电脑**：Windows 10/11（x64）
- 控制 **PowerPoint**：需安装 PowerPoint（桌面版，含讲者备注）
- 控制 **Open Design**：需安装 Open Design（播放由用户手动开始，手机负责换页与备注）
- **手机**：iOS 16.4+（Safari）或 Android（Chrome），与电脑连接同一网络
- **免安装 .NET Runtime**：发布文件为 self-contained

## 下载与安装

1. 到 **[Releases](https://github.com/ufgeorge/presenter-console/releases)** 下载最新版 zip
2. 解压到任意文件夹，执行 `PresenterConsole.Desktop.exe`
3. Windows SmartScreen 若出现「Windows 已保护您的电脑」→ **更多信息 → 仍要运行**（未签名的个人项目，属正常）
4. 首次运行 Windows 防火墙询问 → 勾选**专用网络**并允许

> 📖 完整使用手册（连接、界面说明、语音备注、观众提问、FAQ）见下方「[使用手册](#使用手册)」，zip 内亦附 `README.md`（目前为繁体中文）

## 快速开始

1. 电脑用 PowerPoint 打开简报（或启动 Open Design）
2. 执行 Agent，窗口显示 **QR Code**
3. 手机扫码 → 自动打开控制页（PWA）
4. 电脑开始简报后，即可用手机换页、看备注、播语音

> 💡 手机与电脑需在同一 WiFi；现场没网络时，用手机开**个人热点**、电脑连上热点即可。

## 开发者构建

Windows + .NET 8 SDK：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

输出 `publish\` 文件夹（win-x64、Release、self-contained），**整包复制**到目标电脑执行即可。一般验证：

```powershell
dotnet restore PresenterConsole.sln
dotnet build PresenterConsole.sln
dotnet test PresenterConsole.sln
```

## 项目结构

```
src/PresenterConsole.Contracts/   通讯协议（命令 / 状态 / 消息类型）
src/PresenterConsole.Sync/        同步引擎（command_id + sequence、幂等、重连恢复）
src/PresenterConsole.Desktop/     Windows Agent（WebSocket server、COM 控制、QR、wwwroot 手机前端）
tests/PresenterConsole.Sync.Tests/ 同步引擎测试
docs/                             MVP 规格、实机测试指引
```

## 架构原则

- **PC Agent 是唯一真相来源**：手机显示的页码必须来自 Agent 实测状态（Observed State），禁止手机端自行推算
- **命令幂等**：每条命令带 command_id + sequence，网络重试不会跳页
- **Recovery**：断线重连自动恢复完整状态；任何状态下可一键回到简报

## 第三方组件与致谢

**Runtime 依赖**
- [QRCoder](https://github.com/codebude/QRCoder)（MIT）— QR Code 生成
- [NoSleep.js](https://github.com/richtr/NoSleep.js)（MIT）— 手机屏幕不休眠 fallback
- Microsoft.Data.Sqlite（MIT）— Open Design 项目名称读取
- Microsoft Office Interop Assemblies（微软官方）— PowerPoint 控制

**设计参考（未复制代码）**
- [DeckTap](https://github.com/Rico00121/DeckTap)（MIT）— LAN-first + QR 配对架构概念
- [PPT-Remote-control](https://github.com/PuZhiweizuishuai/PPT-Remote-control)（GPL-3.0）— PowerPoint COM 控制架构参考
- [PhoneAsPrompter](https://github.com/yangzhongke/PhoneAsPrompter)（GPL-3.0）— COM 对象生命周期管理参考
- [mobslide](https://github.com/thewh1teagle/mobslide)（无授权）— 移动端 UX 概念

## 授权

[MIT License](LICENSE)
