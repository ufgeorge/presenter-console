# Presenter Console

Sprint 1：PowerPoint 閉環的 Windows Desktop Agent + 手機 PWA。

開發與建置需要 Windows、.NET 8 SDK；簡報控制功能需要 PowerPoint。

## 發布

在 Windows 且已安裝 .NET 8 SDK 的環境執行：

```powershell
.\scripts\build-release.ps1
```

腳本會以 `win-x64`、Release、self-contained 模式發布單一執行檔，產出位置為：

```text
publish\PresenterConsole.Desktop.exe
```

將整個 `publish` 資料夾複製到目標 Windows 電腦後，直接執行 `PresenterConsole.Desktop.exe`；目標電腦不需要安裝 .NET SDK 或 .NET Runtime。啟動後會顯示 Agent 視窗與手機配對 QR Code。若要控制 PowerPoint，目標電腦仍需安裝 PowerPoint。

一般開發驗證：

```powershell
dotnet restore PresenterConsole.sln
dotnet build PresenterConsole.sln
dotnet test PresenterConsole.sln
```

手機頁碼只採信 Agent 的 CurrentShowPosition；重連透過 SYNC_REQUEST 恢復。真實 200 次驗收仍需具備 PowerPoint 與手機熱點的 Windows 測試環境。