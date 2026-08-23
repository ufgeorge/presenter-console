const MessageType = { State: 2, Error: 5, Pong: 6 };
const CommandType = {
  Next: 0,
  Previous: 1,
  SyncRequest: 3,
  ActivatePowerPoint: 4,
  Ping: 5,
  StartPresentation: 6,
  StartPresentationFromCurrent: 7,
  SelectPresentation: 8
};
const APP_VERSION = "v11";

const LANGUAGES = {
  "zh-TW": {
    connecting: "連線中…",
    connected: "已連線",
    reconnecting: "斷線，3 秒後重連…",
    failed: "連線失敗，請確認電腦與手機在同一網路",
    missingToken: "缺少配對 QR，請重新掃描",
    slide: (current, total) => `第 ${current}/${total} 頁`,
    noNotes: "（本頁沒有 Notes）",
    notFetched: "尚未取得講稿",
    rejected: "操作失敗：命令被拒絕，請重試",
    sendFailed: "操作失敗：命令送出失敗，請重試",
    wakeTitle: "⚠ 螢幕可能會自動鎖定",
    wakeWarningExpand: "展開螢幕鎖定警告",
    wakeWarningCollapse: "收合螢幕鎖定警告",
    wakeText: "請先點擊「重新啟用」；若仍無法保持亮著，請暫時關閉裝置的自動鎖定。",
    wakeSettings: "查看設定路徑",
    ios: "iPhone/iPad：設定 → 螢幕顯示與亮度 → 自動鎖定 → 永不",
    android: "Android：設定 → 顯示 → 螢幕逾時／休眠 → 選擇較長時間",
    retryWake: "重新啟用",
    start: "▶ 開始簡報",
    startCurrent: "從目前頁開始",
    back: "↩ 回簡報",
    presentationLabel: "選擇要控制的簡報",
    noPresentations: "目前沒有開啟的簡報",
    prev: "◀ PREV",
    next: "NEXT ▶",
    fontDecrease: "A−",
    fontIncrease: "A+",
    fontSizeLabel: "字級",
    colorLabel: "講稿文字顏色",
    colorWhite: "白色",
    colorYellow: "黃色",
    colorGreen: "綠色",
    colorRed: "紅色",
    colorCyan: "青色",
    footer: "手機版本"
  },
  "zh-CN": {
    connecting: "连接中…",
    connected: "已连接",
    reconnecting: "断线，3 秒后重连…",
    failed: "连接失败，请确认电脑与手机在同一网络",
    missingToken: "缺少配对 QR，请重新扫描",
    slide: (current, total) => `第 ${current}/${total} 页`,
    noNotes: "（本页没有 Notes）",
    notFetched: "尚未取得讲稿",
    rejected: "操作失败：命令被拒绝，请重试",
    sendFailed: "操作失败：命令发送失败，请重试",
    wakeTitle: "⚠ 屏幕可能会自动锁定",
    wakeWarningExpand: "展开屏幕锁定警告",
    wakeWarningCollapse: "收起屏幕锁定警告",
    wakeText: "请先点击“重新启用”；若仍无法保持亮屏，请暂时关闭设备的自动锁定。",
    wakeSettings: "查看设置路径",
    ios: "iPhone/iPad：设置 → 显示与亮度 → 自动锁定 → 永不",
    android: "Android：设置 → 显示 → 屏幕超时／休眠 → 选择较长时间",
    retryWake: "重新启用",
    start: "▶ 开始演示",
    startCurrent: "从当前页开始",
    back: "↩ 返回演示",
    presentationLabel: "选择要控制的演示",
    noPresentations: "目前没有打开的演示",
    prev: "◀ PREV",
    next: "NEXT ▶",
    fontDecrease: "A−",
    fontIncrease: "A+",
    fontSizeLabel: "字号",
    colorLabel: "讲稿文字颜色",
    colorWhite: "白色",
    colorYellow: "黄色",
    colorGreen: "绿色",
    colorRed: "红色",
    colorCyan: "青色",
    footer: "手机版本"
  },
  en: {
    connecting: "Connecting…",
    connected: "Connected",
    reconnecting: "Disconnected, reconnecting in 3 seconds…",
    failed: "Connection failed. Check that your computer and phone are on the same network.",
    missingToken: "Pairing QR is missing. Please scan again.",
    slide: (current, total) => `Slide ${current}/${total}`,
    noNotes: "(No notes for this slide)",
    notFetched: "Speaker notes not available yet",
    rejected: "Operation failed: command was rejected. Please try again.",
    sendFailed: "Operation failed: command could not be sent. Please try again.",
    wakeTitle: "⚠ Screen may lock automatically",
    wakeWarningExpand: "Expand screen-lock warning",
    wakeWarningCollapse: "Collapse screen-lock warning",
    wakeText: "Tap “Re-enable” first. If the screen still turns off, temporarily disable auto-lock on your device.",
    wakeSettings: "View settings",
    ios: "iPhone/iPad: Settings → Display & Brightness → Auto-Lock → Never",
    android: "Android: Settings → Display → Screen timeout / Sleep → choose a longer time",
    retryWake: "Re-enable",
    start: "▶ Start presentation",
    startCurrent: "Start from current slide",
    back: "↩ Return to presentation",
    presentationLabel: "Choose presentation to control",
    noPresentations: "No presentations are open",
    prev: "◀ PREV",
    next: "NEXT ▶",
    fontDecrease: "A−",
    fontIncrease: "A+",
    fontSizeLabel: "Size",
    colorLabel: "Notes text color",
    colorWhite: "White",
    colorYellow: "Yellow",
    colorGreen: "Green",
    colorRed: "Red",
    colorCyan: "Cyan",
    footer: "Phone version"
  }
};

function getLanguage() {
  const forced = new URLSearchParams(location.search).get("lang");
  const value = forced || navigator.language || "zh-TW";
  const normalized = value.toLowerCase();

  if (normalized.startsWith("en")) return "en";
  if (normalized.startsWith("zh-cn") || normalized.startsWith("zh-sg")) return "zh-CN";
  return "zh-TW";
}

const text = LANGUAGES[getLanguage()];
const status = document.querySelector("#status");
const slide = document.querySelector("#slide");
const notes = document.querySelector("#notes");
const wakeWarning = document.querySelector("#wake-warning");
const wakeWarningToggle = document.querySelector("#wake-warning-toggle");
const wakeRetry = document.querySelector("#wake-retry");
const presentationList = document.querySelector("#presentation-list");
let socket;
let sequence = 0;
let heartbeatTimer;
let wakeLock;
let noSleep;
let wakeFallbackPending = false;
let wakeWarningTimer;
let wakeWarningHideTimer;
let voiceSequenceToken = 0;
let voiceTimer;
let voiceSlide;

function applyLanguage() {
  const language = getLanguage();
  document.documentElement.lang = language === "en" ? "en" : language === "zh-CN" ? "zh-Hans" : "zh-Hant";

  for (const node of document.querySelectorAll("[data-i18n]")) {
    node.textContent = text[node.dataset.i18n];
  }
  for (const node of document.querySelectorAll("[data-i18n-aria]")) {
    node.setAttribute("aria-label", text[node.dataset.i18nAria]);
  }

  document.querySelector("#version").textContent = `${text.footer} ${APP_VERSION}`;
}


function readPreference(key, fallback) {
  try { return localStorage.getItem(key) ?? fallback; } catch { return fallback; }
}
function writePreference(key, value) {
  try { localStorage.setItem(key, value); } catch { /* Storage can be unavailable in private browsing. */ }
}
function applyNotesFontSize(value) {
  const size = Math.min(36, Math.max(12, Number(value) || 16));
  notes.style.fontSize = String(size) + "px";
  writePreference("presenter-notes-font-size", size);
}
function applyNotesColor(value) {
  const allowed = [...document.querySelectorAll("[data-note-color]")].map(button => button.dataset.noteColor);
  const color = allowed.includes(value) ? value : "#f9fafb";
  notes.style.color = color;
  for (const button of document.querySelectorAll("[data-note-color]")) button.setAttribute("aria-pressed", String(button.dataset.noteColor === color));
  writePreference("presenter-notes-color", color);
}
function setupNotesPreferences() {
  applyNotesFontSize(Number(readPreference("presenter-notes-font-size", 16)));
  applyNotesColor(readPreference("presenter-notes-color", "#f9fafb"));
  document.querySelector("#notes-decrease").onclick = () => applyNotesFontSize(Number.parseInt(notes.style.fontSize, 10) - 2);
  document.querySelector("#notes-increase").onclick = () => applyNotesFontSize(Number.parseInt(notes.style.fontSize, 10) + 2);
  for (const button of document.querySelectorAll("[data-note-color]")) button.onclick = () => applyNotesColor(button.dataset.noteColor);
}

function setWakeLockWarning(visible, fallbackPending = false) {
  wakeWarningToggle.hidden = !visible;
  if (!visible) {
    clearTimeout(wakeWarningTimer);
    clearTimeout(wakeWarningHideTimer);
    wakeWarning.hidden = true;
    wakeWarning.classList.remove("is-visible");
    wakeWarningToggle.setAttribute("aria-expanded", "false");
  } else {
    showWakeWarningToast();
  }
  wakeFallbackPending = fallbackPending;
}

function showWakeWarningToast() {
  clearTimeout(wakeWarningTimer);
  clearTimeout(wakeWarningHideTimer);
  wakeWarning.hidden = false;
  requestAnimationFrame(() => wakeWarning.classList.add("is-visible"));
  wakeWarningToggle.setAttribute("aria-expanded", "true");
  wakeWarningToggle.setAttribute("aria-label", text.wakeWarningCollapse);
  wakeWarningTimer = setTimeout(hideWakeWarningToast, 3500);
}

function hideWakeWarningToast() {
  clearTimeout(wakeWarningTimer);
  wakeWarning.classList.remove("is-visible");
  wakeWarningToggle.setAttribute("aria-expanded", "false");
  wakeWarningToggle.setAttribute("aria-label", text.wakeWarningExpand);
  wakeWarningHideTimer = setTimeout(() => { wakeWarning.hidden = true; }, 180);
}

function toggleWakeWarning() {
  showWakeWarningToast();
}

async function acquireWakeLock() {
  if (!("wakeLock" in navigator)) {
    return acquireNoSleepFallback();
  }

  try {
    wakeLock = await navigator.wakeLock.request("screen");
    wakeLock.addEventListener("release", () => {
      if (document.visibilityState === "visible") {
        setWakeLockWarning(true);
      }
    });
    setWakeLockWarning(false);
  } catch {
    await acquireNoSleepFallback();
  }
}

async function acquireNoSleepFallback() {
  noSleep ??= new NoSleep();

  try {
    await noSleep.enable();
    setWakeLockWarning(false);
  } catch {
    setWakeLockWarning(true, true);
  }
}

function parseVoiceSequence(noteText) {
  const sequence = [];
  const commandPattern = /\[(voice|\d+\s*sec)\]/gi;
  let activeCommand;
  let cursor = 0;
  let match;

  while ((match = commandPattern.exec(noteText || "")) !== null) {
    if (activeCommand === "voice") {
      const voiceText = noteText.slice(cursor, match.index).trim();
      if (voiceText) sequence.push({ type: "voice", text: voiceText });
    }

    activeCommand = match[1].toLowerCase() === "voice" ? "voice" : "delay";
    if (activeCommand === "delay") {
      sequence.push({ type: "delay", seconds: Number.parseInt(match[1], 10) });
    }
    cursor = commandPattern.lastIndex;
  }

  if (activeCommand === "voice") {
    const voiceText = noteText.slice(cursor).trim();
    if (voiceText) sequence.push({ type: "voice", text: voiceText });
  }

  return sequence;
}

function stripVoiceCommands(noteText) {
  return (noteText || "").replace(/\[\d+\s*sec\]/gi, "").replace(/\[voice\]/gi, "");
}

function cancelVoiceSequence() {
  voiceSequenceToken++;
  clearTimeout(voiceTimer);
  voiceTimer = undefined;
  speechSynthesis.cancel();
}

function playVoiceSequence(noteText, token) {
  const sequence = parseVoiceSequence(noteText);
  let index = 0;

  const playNext = () => {
    if (token !== voiceSequenceToken || index >= sequence.length) return;

    const item = sequence[index++];
    if (item.type === "delay") {
      voiceTimer = setTimeout(playNext, item.seconds * 1000);
      return;
    }

    const utterance = new SpeechSynthesisUtterance(item.text);
    utterance.lang = "zh-TW";
    utterance.onend = () => {
      if (token === voiceSequenceToken) playNext();
    };
    speechSynthesis.speak(utterance);
  };

  playNext();
}

function updateVoiceForState(state) {
  const currentSlide = state.currentShowPosition;
  if (currentSlide === voiceSlide) return;

  cancelVoiceSequence();
  voiceSlide = currentSlide;
  playVoiceSequence(state.notes, voiceSequenceToken);
}

function renderState(state) {
  if (!state) return;

  slide.textContent = text.slide(state.currentShowPosition, state.slideCount);
  notes.textContent = stripVoiceCommands(state.notes) || text.noNotes;
  updateVoiceForState(state);
  renderPresentations(state);
}

function renderPresentations(state) {
  presentationList.replaceChildren();
  const presentations = state.presentations || [];
  if (!presentations.length) {
    const empty = document.createElement("p");
    empty.textContent = text.noPresentations;
    presentationList.append(empty);
    return;
  }

  for (const item of presentations) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "presentation-option";
    button.textContent = item.name || item.fullName;
    button.title = item.fullName;
    button.setAttribute("aria-pressed", String(item.id === state.selectedPresentationId));
    button.onclick = () => sendCommand(CommandType.SelectPresentation, null, item.id);
    presentationList.append(button);
  }
}

function createCommandId() {
  if (typeof crypto.randomUUID === "function") return crypto.randomUUID();

  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  bytes[6] = (bytes[6] & 15) | 64;
  bytes[8] = (bytes[8] & 63) | 128;

  return [...bytes]
    .map((byte, index) => [4, 6, 8, 10].includes(index)
      ? `-${byte.toString(16).padStart(2, "0")}`
      : byte.toString(16).padStart(2, "0"))
    .join("");
}

function sendCommand(type, slideNumber = null, presentationId = null) {
  try {
    if (socket?.readyState !== WebSocket.OPEN) {
      status.textContent = text.sendFailed;
      return false;
    }

    socket.send(JSON.stringify({
      type: 1,
      command: {
        commandId: createCommandId(),
        sequence: ++sequence,
        type,
        slide: slideNumber,
        presentationId
      }
    }));
    return true;
  } catch (error) {
    status.textContent = `${text.sendFailed} (${error.message || error})`;
    return false;
  }
}

function connect() {
  const token = new URLSearchParams(location.search).get("token");
  if (!token) {
    status.textContent = text.missingToken;
    return;
  }

  status.textContent = text.connecting;
  const protocol = location.protocol === "https:" ? "wss" : "ws";
  socket = new WebSocket(`${protocol}://${location.host}/ws?token=${encodeURIComponent(token)}`);

  socket.onopen = async () => {
    status.textContent = text.connected;
    sendCommand(CommandType.SyncRequest);
    await acquireWakeLock();
    clearInterval(heartbeatTimer);
    heartbeatTimer = setInterval(() => sendCommand(CommandType.Ping), 1500);
  };

  socket.onmessage = event => {
    const message = JSON.parse(event.data);
    if (message.type === MessageType.State || message.type === MessageType.Pong) {
      if (message.state) sequence = Math.max(sequence, message.state.sequence);
      renderState(message.state);
    } else if (message.type === MessageType.Error) {
      status.textContent = message.error || text.rejected;
    }
  };

  socket.onclose = () => {
    clearInterval(heartbeatTimer);
    status.textContent = text.reconnecting;
    setTimeout(connect, 3000);
  };

  socket.onerror = () => {
    status.textContent = text.failed;
  };
}

document.querySelector("#prev").onclick = () => sendCommand(CommandType.Previous);
document.querySelector("#next").onclick = () => sendCommand(CommandType.Next);
document.querySelector("#back").onclick = () => {
  if (sendCommand(CommandType.ActivatePowerPoint)) {
    setTimeout(() => sendCommand(CommandType.SyncRequest), 100);
  }
};
document.querySelector("#start").onclick = () => sendCommand(CommandType.StartPresentation);
document.querySelector("#start-current").onclick = () => sendCommand(CommandType.StartPresentationFromCurrent);
wakeWarningToggle.onclick = toggleWakeWarning;
wakeRetry.onclick = acquireNoSleepFallback;

document.addEventListener("click", event => {
  if (wakeFallbackPending && !event.target.closest?.("#wake-warning, #wake-warning-toggle")) acquireNoSleepFallback();
}, { capture: true });
document.addEventListener("visibilitychange", () => {
  if (document.visibilityState === "visible") acquireWakeLock();
});

applyLanguage();
setupNotesPreferences();
if ("serviceWorker" in navigator) navigator.serviceWorker.register("sw.js?v=11");
connect();
