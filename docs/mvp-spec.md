# Presenter Console — MVP 開發規格（Sprint 1）

> 版本：v0.1 · 2026-08-21 · 依據：Presenter_Console_System_Development_Spec_v0.1 + 三平台技術研究（全部實測/實查確認）
> 完整研究背景：`/root/notes/vivia/presenter-console/README.md`

## 1. 目標

證明核心 Presenter Loop 可靠運作：

```
手機 PWA ──WebSocket──▶ Desktop Agent ──adapter──▶ 簡報軟體
        ◀──actual state──  (Observed > Expected)
```

**手機顯示的頁碼必須是 Agent 確認後的實際頁，禁止手機端 `currentSlide++` 推算。**

## 2. 系統架構（LAN-first）

```
┌──────────────────────────┐   QR 掃描配對    ┌──────────────────────┐
│  Desktop Agent (C#/.NET) │ ◀──WebSocket──▶ │  Mobile PWA          │
│  Windows 11              │   心跳/命令/狀態  │  iOS/Android 瀏覽器   │
│  ├ PowerPoint Adapter    │                  │  ├ 上/下頁大按鈕      │
│  ├ OpenDesign Adapter    │                  │  ├ 頁碼 + Notes      │
│  ├ Canva Adapter         │                  │  ├ Wake Lock         │
│  ├ Sync Engine           │                  │  └ 連線狀態          │
│  └ WebSocket Server      │                  │                      │
└──────────────────────────┘                  └──────────────────────┘
```

- Agent 內建 WebSocket server（LAN/熱點直接連，零部署）；介面預留 outbound cloud relay（跨網段，Phase 2）
- 手機掃 QR（Session Token，時效 2h）加入 session
- 不需要 LAN 也能用：手機開熱點，電腦連熱點

## 3. 不可妥協的核心原則

1. **Observed State > Expected State**：翻頁命令執行後，Agent 必須回報實際頁碼（adapter 讀取），手機只信任 actual state
2. **Command Idempotency**：`command_id + sequence`；網路 retry 不得造成跳兩頁；Agent 保存最近 command_id 去重
3. **Recovery**：斷線重連後 `SYNC_REQUEST` 恢復完整狀態；任何時候手機有「回簡報」按鈕
4. **Heartbeat**：手機 ping / Agent pong + 完整狀態（建議 1.5s）
5. Notes 優先序：personal_note > source_note > empty

## 4. 三平台 Adapter 規格（全部已研究確認可行）

### 4.1 PowerPoint Adapter（COM，精準 1.00）
```
App.Presentations.Open(file) → SlideShowSettings.Run()
  → SlideShowWindow.View
      ├── .Next()/.Previous()/.GotoSlide(n)
      ├── .Slide / .CurrentShowPosition     ← actual state
      └── .Slide.NotesPage.Shapes[i].TextFrame.TextRange.Text  ← notes
事件：Application.SlideShowNextSlide（鍵盤/滑鼠/簡報筆/Navigator 換頁都觸發）
```
- Pitfall：COM 物件釋放（用 COMReferenceTracker 模式）；COM 回 UI thread；COMException try/catch
- 骨架參考：PPT-Remote-control（GPL，只能讀不能抄；COM 呼叫路徑 + COMReferenceTracker 概念）

### 4.2 OpenDesign Adapter（鍵盤 + HTML 解析，已實測）
- **放映環境**：OpenDesign app（Electron 41）內放映；**方向鍵換頁已實測有效** → SendInput 鍵盤模擬
- **Notes**：解析本地 deck HTML（speaker-private 版）`<section class="slide">` 內 `<aside class="speaker-notes">` 逐字稿
  - 檔案偵測：掃描 `*.html.artifact.json`（kind=deck）→ 找對應 `*-speaker-private.html`
- **Current slide**：MVP 用 Visual Sync（截圖比對，與 Canva 共用引擎）；強化：CDP（`--remote-debugging-port` 啟動 app → 讀 scrollLeft）列 Phase 1.5
- 詳細格式：presenter-console skill `references/open-design-deck-format.md`

### 4.3 Canva Adapter（鍵盤 + 手動 notes + Visual Sync）
- **換頁**：放映模式吃鍵盤 → SendInput（方向鍵/空白鍵）
- **Notes**：無 API → Presenter Console 手動輸入/匯入（同 PDF 模式）
- **Current slide**：無 API、URL 不帶頁碼 → Visual Sync

### 4.4 Visual Sync Engine（MVP 共用元件）
- 事先建立 slide 縮圖庫（PowerPoint 可 COM 抓縮圖；OpenDesign 本地 HTML 可離線 render）
- Agent 週期截取放映視窗 → 裁切 → perceptual hash 比對 → best match
- Confidence：native=1.00 / query=0.98 / visual=0.91 / inference=0.75；< threshold 顯示「⚠ 同步不確定」

## 5. WebSocket Protocol（v0.1）

```
手機→Agent:  {command_id, sequence, type: NEXT|PREVIOUS|GOTO_SLIDE|SYNC_REQUEST|PING}
Agent→手機:  {command_id, status: executed|rejected, actual_slide, total, notes, version, timestamp}
Agent→手機:  {type: STATE, page, total, notes, source(adapter), confidence, version}
Agent→手機:  {type: SYNC_STATUS, ...}  {type: ERROR, ...}
```
- 心跳：手機 `PING` → Agent `PONG + 完整狀態`
- 重連：自動 reconnect（3s 內）→ `SYNC_REQUEST` → Agent 回完整 state → 手機恢復 UI
- Duplicate prevention：Agent 保存最近 N 個 command_id

## 6. 手機 PWA 規格

- 上方：連線狀態（● Connected / 重連中 / 斷線）+ 目前頁碼 `17 / 42`
- 中間：可捲動 Speaker Notes（主要資訊）+ 目前頁縮圖
- 下方固定：大型「◀ PREV / NEXT ▶」按鈕
- 右上：計時器 + 選單（黑屏、結束）
- Wake Lock：`navigator.wakeLock.request("screen")` + `visibilitychange` 重新取得；失敗顯示「⚠ Wake Lock 不可用 [重新啟用]」（不得靜默失敗）
- 錯誤以非技術語言顯示 + 重試機制
- 介面語言：繁體中文

## 7. 驗收標準（Sprint 1 DoD）

1. **200 次混合換頁**：手機換頁 + 鍵盤換頁 + 簡報筆換頁 + Goto，手機每次重新取得實際頁碼（PowerPoint）
2. 手機熱點模式完整簡報流程可運作
3. Wake Lock 10 分鐘螢幕不關
4. 斷線 3s 內自動重連 + 狀態恢復
5. 手機重整理頁面後 Resume Session
6. OpenDesign：app 內放映方向鍵控制翻頁 + 手機顯示正確 notes（解析本地 HTML）
7. Canva：翻頁控制 + 手動 notes 編輯
8. 打包成 Windows 執行檔；手機可「加到主畫面」

## 8. Issue 拆分（一任務一分支一 PR）

| Issue | Scope | 依賴 |
|---|---|---|
| #1 Sprint 1：PowerPoint 閉環 | Windows Agent 骨架 + WebSocket + 同步引擎 + PowerPoint Adapter + 手機 PWA + QR + Wake Lock | 無（核心 loop 證明） |
| #2 Sprint 1.5：OpenDesign Adapter | 檔案掃描 + HTML notes 解析 + SendInput 翻頁 + Visual Sync | #1（同步引擎） |
| #3 Sprint 1.6：Canva Adapter | SendInput 翻頁 + 手動 notes + Visual Sync | #1（同步引擎） |

## 9. 技術棧

- Desktop Agent：**C#/.NET 8**（WinForms 或 WPF + Kestrel WebSocket；PowerPoint COM interop）
- 手機：純 HTML/CSS/JS + PWA Manifest（React 可選，非必要）
- 鍵盤模擬：SendInput（Windows）
- 縮圖/視覺比對：.NET 影像處理（SkiaSharp / ImageSharp + perceptual hash）
- QR：QRCoder（.NET）

## 10. 明確不做（MVP 排除）

Remote mouse / Remote keyboard / Screen streaming / 雲端 relay（僅預留介面）/ 帳號系統 / Google Slides 深度整合 / CDP 精準同步（Phase 1.5）/ Native App / macOS / Linux
