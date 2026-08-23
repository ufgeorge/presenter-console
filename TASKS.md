# Sprint 1 PR review fixes

## TODO
- [x] Issue #34：限制 Notes 版面高度、確保控制按鈕可見並更新 PWA 快取版本
- [x] Issue #9：修復手機 NEXT/PREV 控制、拒絕命令診斷與 PowerPoint COM log
- [x] Issue #11：手機開始簡報按鈕與放映依賴診斷
- [x] Issue #18：修復非 secure context 下 crypto.randomUUID 導致手機命令無法送出
- [x] Issue #20：修復 COMReferenceTracker 釋放放映視窗導致開始簡報立即結束

## IN PROGRESS
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
