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

> 📖 完整使用手冊（連線、介面說明、語音備註、觀眾提問、FAQ）見下方「[使用手冊](#使用手冊)」，zip 內亦附 `README.md`

## 快速開始

1. 電腦用 PowerPoint 開啟簡報（或啟動 Open Design）
2. 執行 Agent，視窗顯示 **QR Code**
3. 手機掃 QR → 自動開啟控制頁（PWA）
4. 電腦開始簡報後，即可用手機換頁、看備註、播語音

> 💡 手機與電腦需在同一 WiFi；現場沒網路時，用手機開**個人熱點**、電腦連上熱點即可。

---

## 使用手冊

### 連線準備

**方式 A：同一 WiFi（推薦）** — 手機與電腦連同一個 WiFi 基地台。

**方式 B：手機熱點（現場沒有網路時）**
1. 手機開「個人熱點」
2. 電腦斷開原本 WiFi，連上手機的熱點
3. 重新啟動 Agent（IP 會變，QR Code 也會更新）

> ⚠️ 開熱點的那支手機無法同時當控制端，需要第二支手機/平板控制。

### 手機控制介面

```
┌─────────────────────────────┐
│ ● 已連線     3 / 42   📁 💬 │  ← 狀態列：連線、頁碼、面板開關
│ [ 簡報挑選 ▾ ]              │  ← 多簡報時下拉切換
│                             │
│  講者備註內容…               │  ← Notes 區（可調字級/字色）
│                             │
│ ▶ 開始簡報   ↩ 回簡報        │
│       ◀ PREV   NEXT ▶       │
└─────────────────────────────┘
```

| 按鈕 / 元素 | 功能 |
|---|---|
| 狀態列 | 連線狀態、目前頁碼 / 總頁數 |
| 📁 icon | 展開 / 收合「簡報挑選」面板 |
| 💬 icon | 展開 / 收合「觀眾提問」面板 |
| 簡報挑選 | 同時開多個簡報時，下拉選擇要控制哪一個 |
| Notes 區 | 目前頁的講者備註；A− / A+ 調整字級，色票換字色（會記住） |
| ▶ 開始簡報 | 從第一頁開始放映（PowerPoint） |
| 從目前頁開始 | 從目前頁開始放映（PowerPoint） |
| ↩ 回簡報 | 任何狀態下一鍵回到簡報視窗 |
| ◀ PREV / NEXT ▶ | 上一頁 / 下一頁（大按鈕） |

> 手機顯示的頁碼永遠以電腦實際狀態為準，不會自己跳。

### 支援的簡報來源

**PowerPoint**
- 手機可：開始簡報、從目前頁開始、換頁、看備註、回簡報
- 電腦端用鍵盤 / 滑鼠 / 簡報筆換頁時，手機頁碼同步更新
- 同時開多個簡報檔 → 手機下拉切換

**Open Design**
- 在 Open Design 中**手動開始播放**（全螢幕）後，手機負責：換頁（方向鍵）、講稿顯示
- 多個專案 → 手機下拉切換；講稿來源為專案講者備註（新版格式自動讀取）

### 語音備註（可選）

在講者備註（或 Open Design 講稿）中寫入指令，手機自動朗讀：

| 語法 | 效果 |
|---|---|
| `[voice]歡迎大家` | 手機朗讀「歡迎大家」（講稿中不顯示此行） |
| `[2 sec]` | 停頓 2 秒（獨立一行或接在語音後） |
| `[voice]第一段 [5 sec] [voice]第二段` | 唸第一段 → 停 5 秒 → 唸第二段 |

- 換頁即停止；同一頁回到時不重播。適合講者戴耳機聽提示，不打擾觀眾
- ⚠️ 手機需有中文語音：iPhone 內建；Android 若未安裝中文語音包，手機會顯示提示

### 觀眾提問（可選）

1. 放映中，手機按「💬」展開面板 → 「顯示提問 QR」
2. 電腦跳出全螢幕 QR Code 畫面（蓋在簡報上）
3. 觀眾用手機掃碼 → 提問頁 → 輸入問題送出（免登入、限 200 字、同 IP 10 秒一則）
4. 控制手機即時顯示觀眾問題 → 可刪除
5. 按「↩ 回簡報」關閉 QR 畫面

### 螢幕不休眠

- 控制頁自動嘗試保持手機螢幕亮著（Wake Lock / NoSleep）
- 顯示「⚠ Wake Lock 不可用」時點「重新啟用」即可
- 極少數手機需手動關閉自動鎖定：iPhone「設定 → 螢幕顯示與亮度 → 自動鎖定 → 永不」；Android「設定 → 顯示 → 螢幕逾時 → 永不」（用完記得改回）

### 常見問題（FAQ）

**Q1. 手機打不開控制頁 / 一直「連線中…」**
確認手機與電腦同一 WiFi（或熱點）；確認防火牆有允許「私人網路」；關掉分頁重新掃 QR。

**Q2. 手機連上了，但按按鈕沒反應**
確認簡報真的在放映中（PPT 按過 F5 / Open Design 已手動播放）；重新整理手機頁面再試。

**Q3. 按「開始簡報」沒反應**
確認 PowerPoint 視窗**沒有縮到最小**；若電腦安全軟體鎖住視窗切換，手動點一下 PowerPoint 再按 F5。

**Q4. 語音沒聲音**
iPhone 先點一下螢幕（瀏覽器需一次互動才允許發聲）；Android 檢查「未安裝中文語音」提示；確認 `[voice]` 語法正確。

**Q5. 怎麼確認版本？**
電腦端看 Agent 視窗標題 `Presenter Console Agent vX.Y.Z`；手機端看控制頁最下方「手機版本 vN」（手機介面版本，非 Agent 版本）。對不上 → 重新下載最新 zip。

**Q6. 選不到 Open Design？**
先啟動 Open Design，在 Agent「簡報軟體」下拉選 Open Design 按「套用」；提示「請先設定 OpenDesign 資料夾」→ 按「設定資料夾」選專案資料夾；播放請在 Open Design 手動開始。

**Q7. 手機頁碼跟電腦不同步**
按「↩ 回簡報」或重新整理手機頁面，會自動重新同步。

### 隱私說明

全部連線都在**同一網路內**（LAN / 熱點），不需網際網路；不上傳任何資料、無帳號系統、無雲端伺服器。

---

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
