const MessageType = {
  State: 2,
  Error: 5,
  Pong: 6
};

const CommandType = {
  Next: 0,
  Previous: 1,
  SyncRequest: 3,
  ActivatePowerPoint: 4,
  Ping: 5
};

let socket;
let sequence = 0;
let heartbeatTimer;
let wakeLock;

const status = document.querySelector("#status");
const slide = document.querySelector("#slide");
const notes = document.querySelector("#notes");
const wakeWarning = document.querySelector("#wake-warning");
const wakeRetry = document.querySelector("#wake-retry");

function setWakeLockWarning(visible) {
  wakeWarning.hidden = !visible;
}

async function acquireWakeLock() {
  if (!("wakeLock" in navigator)) {
    setWakeLockWarning(true);
    return;
  }

  try {
    wakeLock = await navigator.wakeLock.request("screen");
    setWakeLockWarning(false);
  } catch (error) {
    setWakeLockWarning(true);
    status.textContent = `已連線（Wake Lock 失敗：${error.message}）`;
  }
}

function renderState(state) {
  if (!state) {
    return;
  }

  slide.textContent = `第 ${state.currentShowPosition}/${state.slideCount} 頁`;
  notes.textContent = state.notes || "（本頁沒有 Notes）";
}

function sendCommand(type, slideNumber = null) {
  if (socket?.readyState !== WebSocket.OPEN) {
    return;
  }

  socket.send(JSON.stringify({
    type: 1,
    command: {
      commandId: crypto.randomUUID(),
      sequence: ++sequence,
      type,
      slide: slideNumber
    }
  }));
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
    heartbeatTimer = setInterval(
      () => sendCommand(CommandType.Ping),
      1500);
  };

  socket.onmessage = event => {
    const message = JSON.parse(event.data);
    if (message.type === MessageType.State || message.type === MessageType.Pong) {
      if (message.state) {
        sequence = Math.max(sequence, message.state.sequence);
      }
      renderState(message.state);
    } else if (message.type === MessageType.Error) {
      status.textContent = message.error || '命令未執行，請重試';
    }
  };

  socket.onclose = () => {
    clearInterval(heartbeatTimer);
    status.textContent = "斷線，3 秒後重連…";
    setTimeout(connect, 3000);
  };

  socket.onerror = () => {
    status.textContent = "連線失敗，請確認電腦與手機在同一網路";
  };
}

document.querySelector("#prev").onclick = () => sendCommand(CommandType.Previous);
document.querySelector("#next").onclick = () => sendCommand(CommandType.Next);
document.querySelector("#back").onclick = () => {
  sendCommand(CommandType.ActivatePowerPoint);
  setTimeout(() => sendCommand(CommandType.SyncRequest), 100);
};
wakeRetry.onclick = acquireWakeLock;
document.addEventListener("visibilitychange", () => {
  if (document.visibilityState === "visible") {
    acquireWakeLock();
  }
});

if ("serviceWorker" in navigator) {
  navigator.serviceWorker.register("sw.js");
}

connect();
