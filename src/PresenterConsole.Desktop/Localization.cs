using System.Globalization;

namespace PresenterConsole.Desktop;

internal static class Localization
{
    private static readonly bool IsEnglish = CultureInfo.CurrentUICulture.Name
        .StartsWith("en", StringComparison.OrdinalIgnoreCase);
    private static readonly bool IsSimplifiedChinese = CultureInfo.CurrentUICulture.Name
        .StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase)
        || CultureInfo.CurrentUICulture.Name.StartsWith(
            "zh-SG",
            StringComparison.OrdinalIgnoreCase);

    public static string NotConnected => IsEnglish
        ? "Not connected"
        : IsSimplifiedChinese ? "未连接" : "未連線";
    public static string SlideNumber(int current, int total) => IsEnglish
        ? $"Slide: {current}/{total}"
        : IsSimplifiedChinese ? $"当前页码：{current}/{total}" : $"目前頁碼：{current}/{total}";
    public static string StartedUnavailable => IsEnglish
        ? "Agent started · Not connected to PowerPoint · QR valid for 2 hours"
        : IsSimplifiedChinese
            ? "Agent 已启动 · 尚未连接到 PowerPoint · QR 有效 2 小时"
            : "Agent 已啟動 · 尚未連線到 PowerPoint · QR 有效 2 小時";
    public static string Started => IsEnglish
        ? "Agent started · LAN WebSocket · QR valid for 2 hours"
        : IsSimplifiedChinese
            ? "Agent 已启动 · LAN WebSocket · QR 有效 2 小時"
            : "Agent 已啟動 · LAN WebSocket · QR 有效 2 小時";
    public static string CommandRejected => IsEnglish
        ? "Command rejected (old sequence or duplicate command). Refresh the page and try again."
        : IsSimplifiedChinese
            ? "命令被拒绝（sequence 过旧或命令重复），请刷新页面后重试"
            : "命令被拒絕（sequence 過舊或命令重複），請重整頁面後重試";
}
