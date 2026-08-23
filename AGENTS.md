# AGENTS.md — Presenter Console 協作規範

本 repo 由 **Vivia（PM）** 與 **Codex（實作）** 協作開發。開發前**必讀**：`PROJECT_CONTEXT.md`（架構鐵律）+ `docs/mvp-spec.md`（MVP 規格）。

## 🤝 Agent 間溝通協定（Vivia PM ↔ Codex）

1. Codex 開 PR 後，Vivia 在 PR 留 review 留言
2. Codex 必須主動讀取 PR comments：
   ```
   gh pr view <PR_NUMBER> --comments
   # 或
   gh api repos/ufgeorge/presenter-console/issues/<PR_NUMBER>/comments --jq '.[].body'
   ```
3. Codex 回覆：`gh pr comment <PR_NUMBER> --body "已修正：..."`
4. Vivia 要求修改 → Codex push 新 commit → PR 自動更新 → Vivia 再 review
5. Codex 有疑問可直接在 PR 留言問 Vivia
6. Merge 由 Vivia 或人類執行，Codex 永不 merge 自己的 PR

## 🔌 Codex 執行環境（網路權限，必讀）

本環境 Codex sandbox **預設關閉外部網路**（官方文件："By default, the agent runs with network access turned off"）。

- GitHub 遠端操作（`git pull`、`git push`、`gh pr`、`gh issue`）一律**直接使用 `require_escalated` 外部網路權限執行**，不要先用 sandbox 預設權限重試
- 其他外網指令（`curl`、`npm/pip install`、`gh api` 等）同樣先請求網路權限再執行
- 嚴禁自行改用 `--yolo` / `--sandbox danger-full-access` / `--dangerously-bypass-approvals-and-sandbox` 等繞過 sandbox 的參數 — 那是全開放權限，不是「只開網路」的正確做法
- 網路被拒時停下來明確告知「需要外部網路權限執行 <指令>」並請求批准，不要跳過 git/gh 操作

## 🏁 Task Completion Policy

- 不得在分析/規劃/部分實作/進度摘要後停止；不得只描述下一步，要直接執行；進度摘要 ≠ 任務完成
- **可以停止的 4 種情況**：① repo 無法推斷的資訊（商業規則/產品決策）② 缺憑證/外部存取權限 ③ 高風險不可逆操作需核准 ④ 需求重大矛盾。除此之外一律繼續
- 停止前必須回報：完成的 requirements（逐項）/ 修改檔案 / 執行過的指令與測試 / 剩餘 blockers
- 長任務開 TASKS.md（TODO/IN PROGRESS/DONE/BLOCKED），只要還有 TODO/IN PROGRESS 就不得輸出完成訊息
- 標準結尾：每次認為工作完成前 — 1. 重新閱讀原始要求 2. 逐項核對 3. 執行測試與建置 4. 修正錯誤 5. 確認沒有未執行的「下一步」

## 任務邊界

1. **一個任務 = 一個分支 = 一個 PR**，禁止多任務夾帶
2. 開新任務分支前先 `git checkout main && git pull`
3. 開 PR 前 `git rebase origin/main`
4. 任務中途發現相關需求 → 先記下，不夾帶，當前任務完成後再開新任務

## PR 描述四要素（缺一打回）

- 🎯 目標（Goal）— 一句話講清楚改什麼
- 📁 脈絡（Context）— 動到哪些檔、錯誤訊息、決策來源
- 🚧 限制（Constraints）— 遵守哪些鐵律、明確沒動什麼
- ✅ 完成定義（Done when）— 跑了什麼驗證（指令＋結果）、列出實際動過的檔案清單

## ⚡ 專案鐵律（違反 = review 打回）

1. **Observed State > Expected State**：手機顯示的頁碼必須是 Agent 確認後的 actual state。禁止手機端 `currentSlide++` 推算當真實狀態來源
2. **Command Idempotency**：命令帶 `command_id + sequence`；網路 retry 不得跳兩頁；Agent 保存最近 command_id 去重
3. **Recovery**：斷線重連後 SYNC_REQUEST 恢復完整狀態；任何狀態下手機有「回簡報」
4. 同步引擎 / protocol 獨立成 package，不寫死在 Desktop/Mobile UI
5. 開源引用前驗證 License：MIT/Apache 可抄；GPL 只能讀；無 license 只看概念

## 驗證指令（依改動類型）

- C# 改動：`dotnet build`（必須零 error）
- 前端改動：`node --check <file>.js`
- 所有 PR：本地跑完整驗證後在 PR description 記錄結果
- 不要為了讓測試過而改測試
- **新增/修改行 ≤100 chars（超長行檢查）**：`awk '{ if (length($0) > 100) print FILENAME":"FNR" ("length($0)")" }' <改動的檔案>` — 有任何輸出（超 100 chars）必須拆行後才能開 PR（Vivia review 打回項目，已連續打回 #80/#82）

### .NET build/test 執行規範（George 2026-08-23 明令，違反 = 打回）

1. 執行 .NET build/test **必須依序執行**（同一時間只跑一個，不並行），指令一律帶：`-m:1 -p:UseSharedCompilation=false`
2. **完成、失敗或中斷後**必須執行 `dotnet build-server shutdown` 清理 build server
3. 逾時（timeout）時清理**完整 process tree**（不只主 process），並**確認無殘留程序**（如 VBCSCompiler / MSBuild 子程序）後才算完成
4. 不得因 build server 殘留而產生「下一次 build 假綠燈」或連線失敗等干擾後續任務的狀態
