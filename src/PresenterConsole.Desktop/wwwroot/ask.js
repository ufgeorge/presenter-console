const language = (navigator.language || "zh-TW").toLowerCase();
const historyKey = "pc-ask-history";
const translations = language.startsWith("en")
  ? {
    title: "Ask the presenter", hint: "Type your question",
    submit: "Send question", success: "Sent ✓",
    error: "Could not send. Please try again.",
    historyTitle: "My questions", historyEmpty: "No questions sent yet"
  }
  : language.startsWith("zh-cn") || language.startsWith("zh-sg")
    ? {
      title: "观众提问", hint: "请输入想问演讲者的问题",
      submit: "发送问题", success: "已发送 ✓", error: "发送失败，请重试。",
      historyTitle: "我提过的问题", historyEmpty: "还没有发送问题"
    }
    : {
      title: "觀眾提問", hint: "請輸入想問講者的問題",
      submit: "送出問題", success: "已送出 ✓", error: "送出失敗，請重試。",
      historyTitle: "我問過的問題", historyEmpty: "還沒有送出問題"
    };
document.documentElement.lang = language.startsWith("en")
  ? "en" : language.startsWith("zh-cn") ? "zh-Hans" : "zh-Hant";
document.querySelector("#title").textContent = translations.title;
document.querySelector("#hint").textContent = translations.hint;
document.querySelector("#submit").textContent = translations.submit;
document.querySelector("#history-title").textContent = translations.historyTitle;
document.querySelector("#history-empty").textContent = translations.historyEmpty;
const form = document.querySelector("#ask-form");
const input = document.querySelector("#question");
const submit = document.querySelector("#submit");
const result = document.querySelector("#result");
const historyEmpty = document.querySelector("#history-empty");
const historyList = document.querySelector("#history-list");

function readHistory() {
  try {
    const saved = JSON.parse(localStorage.getItem(historyKey) || "[]");
    return Array.isArray(saved) ? saved : [];
  } catch {
    return [];
  }
}

function saveHistory(history) {
  try {
    localStorage.setItem(historyKey, JSON.stringify(history));
  } catch { /* Storage can be unavailable in private browsing. */ }
}

function renderHistory(history) {
  historyList.replaceChildren();
  historyEmpty.hidden = history.length > 0;
  for (const question of history) {
    const item = document.createElement("li");
    const time = document.createElement("time");
    time.dateTime = question.createdAt;
    time.textContent = new Date(question.createdAt).toLocaleTimeString(
      [], { hour: "2-digit", minute: "2-digit" });
    const text = document.createElement("span");
    text.textContent = question.text;
    item.append(time, text);
    historyList.append(item);
  }
}

let history = readHistory();
renderHistory(history);

form.onsubmit = async event => {
  event.preventDefault();
  const text = input.value.trim();
  if (!text || text.length > 200) {
    result.textContent = translations.error;
    result.className = "error";
    return;
  }
  submit.disabled = true;
  result.textContent = "";
  try {
    const response = await fetch("/api/ask", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text })
    });
    if (!response.ok) {
      const payload = await response.json().catch(() => ({}));
      throw new Error(payload.error || translations.error);
    }
    const question = await response.json();
    history = [question, ...history];
    saveHistory(history);
    renderHistory(history);
    result.textContent = translations.success;
    result.className = "success";
    input.value = "";
  } catch (error) {
    result.textContent = error.message || translations.error;
    result.className = "error";
  } finally {
    submit.disabled = false;
  }
};
