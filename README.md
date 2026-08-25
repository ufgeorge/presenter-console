# Presenter Console — 手機簡報中控台

[English](README.en.md) · [简体中文](README.zh-CN.md) · **繁體中文**

把手機變成簡報講者的中控台：換頁、開始簡報、同步顯示講者備註、語音備註播報、觀眾提問 — 全部透過手機瀏覽器完成，**手機零安裝 App**。

- 🎯 支援 **PowerPoint** 與 **Open Design** 兩種簡報來源
- 📱 手機不用裝 App：PWA 網頁、掃 QR Code 即配對
- 🌐 同一 WiFi / 手機熱點即可使用，不需要雲端伺服器（LAN-first）
- 🌏 三語介面：繁體中文 / 简体中文 / English

## 功能特色

| 功能 | 說明 |
|---|---|
| 換頁控制 | NEXT / PREV、開始簡報、從目前頁開始、一鍵回簡報 |
| 講者備註同步 | 手機即時顯示目前頁的 Notes（以 Agent 實測頁碼為準） |
| 語音備註 | Notes 寫 `[voice]文字 [2 sec]` 語法，手機自動 TTS 播報（講者耳機聽，最不干擾） |
| 多簡報挑選 | 同時開多個 PowerPoint 檔 / Open Design 專案，手機下拉切換 |
| 觀眾提問（Audience Q&A） | 放映中手機顯示提問 QR Code，觀眾瀏覽器送問題，控制端檢視 / 刪除 |
| 螢幕不休眠 | Wake Lock（secure context）+ NoSleep.js fallback |
| 三語介面 | 繁體中文 / 简体中文 / English，隨手機語言自動切換 |

## 系統需求

- **電腦**：Windows 10/11（x64）
- 控制 **PowerPoint**：需安裝 PowerPoint（Desktop 版，含講者備註）
- 控制 **Open Design**：需安裝 Open Design（播放由使用者手動開始，手機負責換頁與備註）
- **手機**：iOS 16.4+（Safari）或 Android（Chrome），與電腦連同一網路
- **免安裝 .NET Runtime**：發布檔為 self-contained

## 下載與安裝

1. 到 **[Releases](https://github.com/ufgeorge/presenter-console/releases)** 下載最新版 zip
2. 解壓到任意資料夾，執行 `PresenterConsole.Desktop.exe`
3. Windows SmartScreen 若出現「Windows 已保護您的電腦」→ **更多資訊 → 仍要執行**（未簽章的個人專案，屬正常）
4. 首次執行 Windows 防火牆詢問 → 勾選**私人網路**並允許

> 📖 完整使用手冊（安裝、介面說明、語音備註、觀眾提問、FAQ）：[docs/user-guide.md](docs/user-guide.md)（目前為繁體中文，zip 內亦附 `UserGuide.md`）

## 快速開始

1. 電腦用 PowerPoint 開啟簡報（或啟動 Open Design）
2. 執行 Agent，視窗顯示 **QR Code**
3. 手機掃 QR → 自動開啟控制頁（PWA）
4. 電腦開始簡報後，即可用手機換頁、看備註、播語音

> 💡 手機與電腦需在同一 WiFi；現場沒網路時，用手機開**個人熱點**、電腦連上熱點即可。

## 開發者建置

Windows + .NET 8 SDK：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

輸出 `publish\` 資料夾（win-x64、Release、self-contained），**整包複製**到目標電腦執行即可。一般驗證：

```powershell
dotnet restore PresenterConsole.sln
dotnet build PresenterConsole.sln
dotnet test PresenterConsole.sln
```

## 專案結構

```
src/PresenterConsole.Contracts/   通訊協定（命令 / 狀態 / 訊息型別）
src/PresenterConsole.Sync/        同步引擎（command_id + sequence、冪等、重連恢復）
src/PresenterConsole.Desktop/     Windows Agent（WebSocket server、COM 控制、QR、wwwroot 手機前端）
tests/PresenterConsole.Sync.Tests/ 同步引擎測試
docs/                             MVP 規格、實機測試指引
```

## 架構原則

- **PC Agent 是唯一真相來源**：手機顯示的頁碼必須來自 Agent 實測狀態（Observed State），禁止手機端自行推算
- **命令冪等**：每條命令帶 command_id + sequence，網路重試不會跳頁
- **Recovery**：斷線重連自動恢復完整狀態；任何狀態下可一鍵回簡報

## 第三方元件與致謝

**Runtime 依賴**
- [QRCoder](https://github.com/codebude/QRCoder)（MIT）— QR Code 產生
- [NoSleep.js](https://github.com/richtr/NoSleep.js)（MIT）— 手機螢幕不休眠 fallback
- Microsoft.Data.Sqlite（MIT）— Open Design 專案名稱讀取
- Microsoft Office Interop Assemblies（微軟官方）— PowerPoint 控制

**設計參考（未複製程式碼）**
- [DeckTap](https://github.com/Rico00121/DeckTap)（MIT）— LAN-first + QR 配對架構概念
- [PPT-Remote-control](https://github.com/PuZhiweizuishuai/PPT-Remote-control)（GPL-3.0）— PowerPoint COM 控制架構參考
- [PhoneAsPrompter](https://github.com/yangzhongke/PhoneAsPrompter)（GPL-3.0）— COM 物件生命週期管理參考
- [mobslide](https://github.com/thewh1teagle/mobslide)（無授權）— 行動端 UX 概念

## 授權

[MIT License](LICENSE)
