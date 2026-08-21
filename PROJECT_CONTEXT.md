# PROJECT_CONTEXT.md — Presenter Console 專案脈絡

> 開發前必讀。完整 MVP 規格見 `docs/mvp-spec.md`；研究背景與三平台技術細節以 presenter-console skill 的 references 為準。

## 產品定位

手機當講者的「第二螢幕 + 整場 Presentation 流程控制器」：控制簡報換頁、同步顯示 Speaker Notes、Scene 切換（簡報↔Demo 視窗）、Presentation Flow。
PC（Desktop Agent）是 Presentation State **唯一真相來源**。

## 系統架構（LAN-first）

```
Desktop Agent (C#/.NET, Windows 11)          Mobile PWA (iOS/Android 瀏覽器)
├ PowerPoint Adapter (COM)      ◀──WebSocket──▶  ├ 上/下頁大按鈕
├ OpenDesign Adapter (鍵盤+HTML)   心跳/命令/狀態   ├ 頁碼 + Notes 顯示
├ Canva Adapter (鍵盤+Visual)                  ├ Wake Lock
├ Sync Engine                                 └ 連線狀態顯示
└ WebSocket Server (LAN/熱點, QR 配對)
```

- Agent 內建 WebSocket server（LAN/熱點零部署）；介面預留 outbound cloud relay（跨網段，Phase 2）
- 手機掃 QR（Session Token，時效 2h）加入 session
- 手機開熱點 → 電腦連熱點 → 即可用（不依賴現場 LAN）

## 三平台 Adapter 關鍵技術（已全部研究/實測確認）

### PowerPoint（COM，精準）
```
SlideShowWindow.View.Next()/.Previous()/.GotoSlide(n)
SlideShowWindow.View.CurrentShowPosition   ← actual slide
SlideShowWindow.View.Slide.NotesPage.Shapes[i].TextFrame.TextRange.Text  ← notes
Application.SlideShowNextSlide 事件（鍵盤/滑鼠/簡報筆換頁都觸發）
```
- Pitfall：COM 物件必須正確釋放（COMReferenceTracker 模式），否則 PowerPoint 卡死不關；COM 呼叫回 UI thread；COMException try/catch

### OpenDesign（Electron app 內放映，鍵盤模擬已實測有效）
- 放映：OpenDesign app（Electron 41）內，**方向鍵換頁已實測可行** → SendInput
- Notes：解析本地 deck HTML（`*-speaker-private.html`）每頁 `<aside class="speaker-notes">` 逐字稿
- 檔案偵測：掃描 `*.html.artifact.json`（`kind: "deck"`）→ 對應 HTML
- Current slide（MVP）：Visual Sync 截圖比對；CDP（`--remote-debugging-port`）列 Phase 1.5

### Canva（網頁放映）
- 換頁：鍵盤模擬（方向鍵/空白鍵）
- Notes：**無 API** → 手動輸入/匯入（Presenter Console 自存，同 PDF 模式）
- Current slide：**無 API、URL 不帶頁碼** → Visual Sync（截圖 + perceptual hash 比對）

### Visual Sync Engine（Canva/OpenDesign 共用）
- 事先建立 slide 縮圖庫 → Agent 週期截取放映視窗 → 裁切 → perceptual hash 比對 → best match
- Confidence 分級：native=1.00 / query=0.98 / visual=0.91 / inference=0.75

## WebSocket Protocol 要點

```
手機→Agent: {command_id, sequence, type: NEXT|PREVIOUS|GOTO_SLIDE|SYNC_REQUEST|PING}
Agent→手機: {command_id, status, actual_slide, total, notes, version, timestamp}
Agent→手機: {type: STATE | SYNC_STATUS | ERROR, ...}
```
- 心跳 1.5s；斷線 3s 內自動重連 + SYNC_REQUEST 恢復；Agent 保存最近 command_id 去重

## 手機 PWA 要點

- 上方：連線狀態 + 頁碼 `17 / 42`；中間：Notes（主要資訊）+ 縮圖；下方：大按鈕 ◀ PREV / NEXT ▶
- Wake Lock：`navigator.wakeLock.request("screen")` + visibilitychange 重新取得；失敗不得靜默（顯示「⚠ Wake Lock 不可用 [重新啟用]」）
- 繁體中文；錯誤以非技術語言 + 重試

## 驗收（Sprint 1 DoD，節錄）

1. 200 次混合換頁（手機/鍵盤/簡報筆/Goto）手機每次取得實際頁碼
2. 手機熱點完整簡報流程可運作
3. Wake Lock 10 分鐘螢幕不關；斷線 3s 重連 + 狀態恢復
4. 手機重整頁面後 Resume Session
5. OpenDesign 方向鍵控制 + 手機顯示正確 notes；Canva 翻頁 + 手動 notes
6. 打包 Windows 執行檔；手機可加到主畫面

## 技術棧

- Desktop Agent：C#/.NET 8（WinForms 或 WPF + Kestrel WebSocket + PowerPoint COM interop）
- 手機：純 HTML/CSS/JS + PWA Manifest
- 鍵盤模擬：SendInput（Windows）；QR：QRCoder；影像：SkiaSharp/ImageSharp + perceptual hash

## MVP 明確不做

Remote mouse / Remote keyboard / Screen streaming / 雲端 relay（僅預留介面）/ 帳號系統 / Google Slides 深度整合 / CDP 精準同步（Phase 1.5）/ Native App / macOS / Linux
