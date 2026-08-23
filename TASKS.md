# Sprint 1 PR review fixes

## TODO
- [x] Issue #34：限制 Notes 版面高度、確保控制按鈕可見並更新 PWA 快取版本
- [x] Issue #9：修復手機 NEXT/PREV 控制、拒絕命令診斷與 PowerPoint COM log
- [x] Issue #11：手機開始簡報按鈕與放映依賴診斷
- [x] Issue #18：修復非 secure context 下 crypto.randomUUID 導致手機命令無法送出
- [x] Issue #20：修復 COMReferenceTracker 釋放放映視窗導致開始簡報立即結束

## IN PROGRESS
- [x] Issue #64：每頁語音只播一次、過濾語音命令空行並更新 PWA 快取版本
- [x] Issue #34：限制 Notes 版面高度、確保控制按鈕可見並更新 PWA 快取版本
- [x] Issue #30：手機 Notes 與換頁控制響應式版面、字級/字色設定
- [x] Issue #9：實作與驗證
- [x] Issue #11：實作與驗證
- [x] Issue #18：實作與驗證
- [x] Issue #20：實作與驗證
- [x] Issue #22：實作與驗證
- [x] Issue #22：COM 事件 marshal 到 UI thread、COM 狀態快取與 RPC 崩潰診斷
- [x] Issue #24：放映立即結束診斷與 SendKeys F5 替代放映路徑
- [x] Issue #26：SendKeys 焦點競態、移除 COM fallback、PowerPoint 崩潰重連
- [x] Issue #12：LAN HTTP Wake Lock 的 NoSleep fallback 與自動鎖定指引

## DONE
- [x] Issue #64：實作與驗證（待 PR review）
- [x] Issue #38：Wake 警告改為暫時性 toast、修復 icon 閃爍並更新 PWA 快取版本
- [x] Issue #36：修復 Wake 警告區塊的 CSS display 覆蓋 hidden
- [x] Issue #32：Wake 警告收合、Speaker Notes 換行正規化與移除頁碼
- [x] Issue #13：PWA 與 Agent 三語 i18n、命令防呆與手機版本標記
- [x] Issue #20：COM 放映視窗不得由每次操作 FinalRelease
- [x] Issue #7：發布 Office 15.0.0.0 Office.dll、加入輸出驗證並完成啟動驗證
- [x] Issue #5：改資料夾發布、加入 fallback 診斷 log 並完成驗證
- [x] Issue #3：建立 self-contained Windows 發布腳本並完成 exe 啟動驗證
- [x] Fix solution/project/test build blockers
- [x] Fix protocol and observed-state loop
- [x] Add session token and PowerPoint activation/active-instance support
- [x] Complete PWA recovery, Wake Lock, icons, and cache update
- [x] Reformat source files
- [x] Run build/test/lint and update PR

## BLOCKED
- Real PowerPoint/phone 200-run acceptance requires Windows Office hardware.

## Issue #48 工作追蹤
- [DONE] 讀取 issue #48 規格並同步 `main`
- [DONE] 永遠顯示 PowerPoint + OpenDesign，並在套用時即時偵測
- [DONE] 強化 OpenDesign process/window title 偵測
- [DONE] 執行 build、test、lint 並開 PR
- [DONE] 讀取 PR comments（目前無留言）

## Issue #46 工作追蹤
- [DONE] 讀取 `AGENTS.md`、`PROJECT_CONTEXT.md`、`docs/mvp-spec.md`
- [DONE] 同步 `main` 並建立專用分支
- [DONE] 修正 PowerPointAdapter COM 釋放與 InvalidComObjectException 錯誤處理
- [DONE] 執行 build、test、行長檢查並修正問題
- [DONE] 建立 PR、讀取 review comments（目前無留言）
- [BLOCKED] 實機 PowerPoint/手機與跨軟體切換驗收需 George 的 Windows Office 環境

## Issue #56 工作追蹤
- [DONE] 讀取 issue #56、`AGENTS.md`、`PROJECT_CONTEXT.md`、`docs/mvp-spec.md` 並同步 `main`
- [DONE] OpenDesign 啟動改為提示使用者手動開始全螢幕播放，不開外部瀏覽器、不送 F11
- [DONE] scanner 排除 `-public`、`-speaker-private`、`-private` companion artifact
- [DONE] 清理 artifact `title` 的 `.html` 後綴並補回歸測試
- [DONE] 執行 build、test、lint、建立 PR 並處理 review comments

## Issue #58 工作追蹤
- [DONE] 讀取 issue #58、`AGENTS.md`、`PROJECT_CONTEXT.md`、`docs/mvp-spec.md` 並同步 `main`
- [DONE] OpenDesign 視窗偵測加入 `Open Design` process 名稱
- [DONE] 方向鍵最多送出一次，alive 檢查失敗時只重試啟用／尋找視窗
- [DONE] key 送出後更新 expected 頁碼與 Notes fallback，並在重試耗盡時回報錯誤
- [DONE] 執行 build、test、lint、建立 PR 並處理 review comments

## Issue #62 工作追蹤
- [DONE] 讀取 issue #62、專案規範並同步 `main`
- [DONE] 語音解析改為逐行，隱藏 `[voice]` 文字並保留正常 Notes 換行
- [DONE] 語音速率調整為 1.5，並更新前端／Service Worker 快取版本至 v12
- [DONE] 執行解析驗算、lint、build、建立 PR 並處理 review comments

## Issue #68 工作追蹤
- [DONE] 讀取 issue #68、專案規範並同步 `main`
- [DONE] PowerPoint 選定文件視窗加入 AttachThreadInput／SetForegroundWindow 前景鎖穿透
- [DONE] 啟用後等待 300ms 驗證實際前景 HWND，失敗最多重試 3 次並回報明確錯誤
- [DONE] 保留 application.Activate、SendKeys F5 節奏與禁止 COM fallback
- [DONE] 執行 build、test、git diff --check

## Issue #70 工作追蹤
- [DONE] 讀取 issue #70 規格並同步 `main`
- [DONE] 將手機簡報挑選 UI 改為 `<select>`，更新 CSS、cache version/query
- [DONE] 執行 JavaScript 語法檢查、相關測試與三 viewport 版面驗證
- [DONE] 提交變更、rebase/push、開 PR 並讀取 PR comments（目前無留言）

## Issue #72 工作追蹤
- [DONE] 讀取 issue #72、專案規範並同步 `main`
- [DONE] PowerPoint 視窗啟用前偵測最小化狀態並以 `ShowWindow(SW_RESTORE)` 還原
- [DONE] 執行 build、test、`git diff --check`

## Issue #75 工作追蹤
- [DONE] 讀取 issue #75、專案規範並同步 `main`
- [DONE] OpenDesign scanner 讀取 daemon `/api/projects` 名稱並保留 fallback
- [DONE] 執行測試、build、`git diff --check`，建立 PR 並處理 review comments

## Issue #79 工作追蹤
- [DONE] 讀取 issue #79、專案規範並同步 `main`
- [DONE] ALT 模擬前景鎖穿透，保留 AttachThreadInput 與實際前景驗證
- [DONE] 加入還原、thread attach、SetForegroundWindow 與前景驗證診斷 log
- [DONE] Agent 標題顯示版本，csproj 設定 Version 0.9.0
- [DONE] 執行 build、test、`git diff --check`

## Issue #81 工作追蹤
- [DONE] 讀取 issue #81、專案規範並同步 `main`
- [DONE] 以 OpenDesign app.sqlite 取代 daemon 名稱查詢並加入診斷 log
- [DONE] 新增 SQLite 對照/ fallback 測試
- [DONE] 執行 build、test、`git diff --check`、建立 PR 並處理 review comments

## Issue #83 工作追蹤
- [DONE] 讀取 issue #83、專案規範並同步 `main`
- [DONE] ALT 前景鎖穿透改為 SHIFT，更新診斷 log
- [IN PROGRESS] 執行 build、test、`git diff --check`、建立 PR 並處理 review comments
