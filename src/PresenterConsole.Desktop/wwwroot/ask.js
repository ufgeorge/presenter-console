const language = (navigator.language || "zh-TW").toLowerCase();
const translations = language.startsWith("en")
  ? {
    title: "Ask the presenter", hint: "Type your question",
    submit: "Send question", success: "Sent ✓",
    error: "Could not send. Please try again."
  }
  : language.startsWith("zh-cn") || language.startsWith("zh-sg")
    ? {
      title: "观众提问", hint: "请输入想问演讲者的问题",
      submit: "发送问题", success: "已发送 ✓", error: "发送失败，请重试。"
    }
    : {
      title: "觀眾提問", hint: "請輸入想問講者的問題",
      submit: "送出問題", success: "已送出 ✓", error: "送出失敗，請重試。"
    };
document.documentElement.lang = language.startsWith("en")
  ? "en" : language.startsWith("zh-cn") ? "zh-Hans" : "zh-Hant";
document.querySelector("#title").textContent = translations.title;
document.querySelector("#hint").textContent = translations.hint;
document.querySelector("#submit").textContent = translations.submit;
const form = document.querySelector("#ask-form");
const input = document.querySelector("#question");
const submit = document.querySelector("#submit");
const result = document.querySelector("#result");
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
