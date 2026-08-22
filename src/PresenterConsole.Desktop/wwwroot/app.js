const MessageType = { State: 2, Error: 5, Pong: 6 };
const CommandType = { Next: 0, Previous: 1, SyncRequest: 3, ActivatePowerPoint: 4, Ping: 5, StartPresentation: 6, StartPresentationFromCurrent: 7 };
let socket;
let sequence = 0;
let heartbeatTimer;
let wakeLock;
let noSleep;
let wakeFallbackPending = false;

const status = document.querySelector("#status");
const slide = document.querySelector("#slide");
const notes = document.querySelector("#notes");
const wakeWarning = document.querySelector("#wake-warning");
const wakeRetry = document.querySelector("#wake-retry");

function setWakeLockWarning(visible, fallbackPending = false) {
  wakeWarning.hidden = !visible;
  wakeFallbackPending = fallbackPending;
}

async function acquireWakeLock() {
  if (!("wakeLock" in navigator)) {
    await acquireNoSleepFallback();
    return;
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
    status.textContent = "已連線（螢幕保持亮著）";
  } catch {
    setWakeLockWarning(true, true);
    status.textContent = "已連線（需要點擊才能保持螢幕亮著）";
  }
}

function renderState(state) {
  if (!state) return;
  slide.textContent = `第 ${state.currentShowPosition}/${state.slideCount} 頁`;
  notes.textContent = state.notes || "（本頁沒有 Notes）";
}

function createCommandId() {
  if (typeof crypto.randomUUID === "function") return crypto.randomUUID();
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  return [...bytes].map((byte, index) => {
    const hex = byte.toString(16).padStart(2, "0");
    return [4, 6, 8, 10].includes(index) ? `-${hex}` : hex;
  }).join("");
}

function sendCommand(type, slideNumber = null) {
  if (socket?.readyState !== WebSocket.OPEN) return;
  socket.send(JSON.stringify({ type: 1, command: {
    commandId: createCommandId(), sequence: ++sequence, type, slide: slideNumber
  }}));
}

function connect() {
  const token = new URLSearchParams(location.search).get("token");
  if (!token) {
    status.textContent = "缺少配對 QR，請重新掃描";
    return;
  }
  const protocol = location.protocol === "https:" ? "wss" : "ws";
  socket = new WebSocket(`${protocol}://${location.host}/ws?token=${encodeURIComponent(token)}`);
  socket.onopen = async () => {
    status.textContent = "已連線";
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
      status.textContent = `操作失敗：${message.error || '命令未執行，請重試'}`;
    }
  };
  socket.onclose = () => {
    clearInterval(heartbeatTimer);
    status.textContent = "斷線，3 秒後重連…";
    setTimeout(connect, 3000);
  };
  socket.onerror = () => { status.textContent = "連線失敗，請確認電腦與手機在同一網路"; };
}

document.querySelector("#prev").onclick = () => sendCommand(CommandType.Previous);
document.querySelector("#next").onclick = () => sendCommand(CommandType.Next);
document.querySelector("#back").onclick = () => {
  sendCommand(CommandType.ActivatePowerPoint);
  setTimeout(() => sendCommand(CommandType.SyncRequest), 100);
};
document.querySelector("#start").onclick = () => sendCommand(CommandType.StartPresentation);
document.querySelector("#start-current").onclick = () => sendCommand(CommandType.StartPresentationFromCurrent);
wakeRetry.onclick = acquireNoSleepFallback;
document.addEventListener("click", () => {
  if (wakeFallbackPending) acquireNoSleepFallback();
}, { capture: true });
document.addEventListener("visibilitychange", () => {
  if (document.visibilityState === "visible") acquireWakeLock();
});
if ("serviceWorker" in navigator) navigator.serviceWorker.register("sw.js");
connect();
