# Presenter Console — Mobile Presentation Remote

**English** · [简体中文](README.zh-CN.md) · [繁體中文](README.md)

Turn your phone into a presenter's console: slide navigation, start/back-to-presentation, live speaker notes, voice notes, and audience Q&A — all from the phone browser, **no app install required**.

- 🎯 Supports **PowerPoint** and **Open Design** presentation sources
- 📱 No app to install: PWA web page, pair by scanning a QR code
- 🌐 Works over the same Wi-Fi / phone hotspot, no cloud server needed (LAN-first)
- 🌏 Trilingual UI: English / 繁體中文 / 简体中文

## Features

| Feature | Description |
|---|---|
| Slide control | NEXT / PREV, start slideshow, start from current slide, one-tap back to presentation |
| Speaker notes sync | Live notes for the current slide (based on agent-verified page number) |
| Voice notes | Write `[voice]text [2 sec]` in notes; the phone speaks it via TTS (best heard through the presenter's earpiece) |
| Multiple presentations | Open several PowerPoint files / Open Design projects and switch from the phone |
| Audience Q&A | Show a QR code during the show; audience submits questions from their browser; host views / deletes them |
| Keep screen awake | Wake Lock (secure context) with NoSleep.js fallback |
| Trilingual UI | English / 繁體中文 / 简体中文, follows the phone's language |

## System Requirements

- **Computer**: Windows 10/11 (x64)
- To control **PowerPoint**: PowerPoint (Desktop edition, with speaker notes) is required
- To control **Open Design**: the Open Design app is required (playback is started manually by the user; the phone handles slide navigation and notes)
- **Phone**: iOS 16.4+ (Safari) or Android (Chrome), on the same network as the computer
- **No .NET Runtime required**: release builds are self-contained

## Download & Install

1. Download the latest zip from **[Releases](https://github.com/ufgeorge/presenter-console/releases)**
2. Extract to any folder and run `PresenterConsole.Desktop.exe`
3. If Windows SmartScreen shows "Windows protected your PC" → **More info → Run anyway** (expected for an unsigned personal project)
4. When Windows Firewall asks on first run → tick **Private networks** and allow

## Quick Start

1. Open a presentation in PowerPoint on the computer (or launch Open Design)
2. Run the Agent — the window shows a **QR code**
3. Scan the QR with your phone → the control page (PWA) opens automatically
4. Once the slideshow is started, control slides, read notes, and play voice notes from the phone

> 💡 The phone and computer must be on the same Wi-Fi. No network at the venue? Turn on the phone's **personal hotspot** and connect the computer to it.

## Building from Source

Windows + .NET 8 SDK:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

Output goes to `publish\` (win-x64, Release, self-contained) — copy the **whole folder** to the target computer. General verification:

```powershell
dotnet restore PresenterConsole.sln
dotnet build PresenterConsole.sln
dotnet test PresenterConsole.sln
```

## Project Structure

```
src/PresenterConsole.Contracts/   Communication protocol (commands / state / message types)
src/PresenterConsole.Sync/        Sync engine (command_id + sequence, idempotency, reconnect recovery)
src/PresenterConsole.Desktop/     Windows Agent (WebSocket server, COM control, QR, wwwroot phone frontend)
tests/PresenterConsole.Sync.Tests/ Sync engine tests
docs/                             MVP spec, manual testing guide
```

## Architecture Principles

- **The PC Agent is the single source of truth**: the page number shown on the phone must come from the agent's verified state (Observed State), never computed on the phone
- **Command idempotency**: every command carries command_id + sequence; network retries never skip slides
- **Recovery**: full state is restored automatically on reconnect; one tap returns to the presentation from any state

## Third-Party Components & Credits

**Runtime dependencies**
- [QRCoder](https://github.com/codebude/QRCoder) (MIT) — QR code generation
- [NoSleep.js](https://github.com/richtr/NoSleep.js) (MIT) — keep-screen-awake fallback
- Microsoft.Data.Sqlite (MIT) — Open Design project name lookup
- Microsoft Office Interop Assemblies (Microsoft official) — PowerPoint control

**Design references (no code copied)**
- [DeckTap](https://github.com/Rico00121/DeckTap) (MIT) — LAN-first + QR pairing architecture concept
- [PPT-Remote-control](https://github.com/PuZhiweizuishuai/PPT-Remote-control) (GPL-3.0) — PowerPoint COM control architecture reference
- [PhoneAsPrompter](https://github.com/yangzhongke/PhoneAsPrompter) (GPL-3.0) — COM object lifetime management reference
- [mobslide](https://github.com/thewh1teagle/mobslide) (no license) — mobile UX concepts

## License

[MIT License](LICENSE)
