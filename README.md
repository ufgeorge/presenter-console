# Presenter Console

Sprint 1：PowerPoint 閉環的 Windows Desktop Agent + 手機 PWA。

需要 Windows、.NET 8 SDK 與 PowerPoint。執行 `dotnet restore PresenterConsole.sln`、`dotnet build PresenterConsole.sln`、`dotnet test PresenterConsole.sln`。

手機頁碼只採信 Agent 的 CurrentShowPosition；重連透過 SYNC_REQUEST 恢復。此工作區沒有 .NET SDK、PowerPoint、手機熱點或 GitHub remote，無法執行真實 200 次驗收與 PR review。