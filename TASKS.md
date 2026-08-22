# Sprint 1 PR review fixes

## TODO
- [x] Issue #9：修復手機 NEXT/PREV 控制、拒絕命令診斷與 PowerPoint COM log
- [x] Issue #11：手機開始簡報按鈕與放映依賴診斷
- [x] Issue #18：修復非 secure context 下 crypto.randomUUID 導致手機命令無法送出
- [x] Issue #20：修復 COMReferenceTracker 釋放放映視窗導致開始簡報立即結束


## IN PROGRESS
- [x] Issue #9：實作與驗證
- [x] Issue #11：實作與驗證
- [x] Issue #18：實作與驗證
- [x] Issue #20：實作與驗證
- [x] Issue #22：實作與驗證


- [x] Issue #22：COM 事件 marshal 到 UI thread、COM 狀態快取與 RPC 崩潰診斷
- [x] Issue #24：放映立即結束診斷與 SendKeys F5 替代放映路徑
- [x] Issue #26：SendKeys 焦點競態、移除 COM fallback、PowerPoint 崩潰重連

## DONE
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