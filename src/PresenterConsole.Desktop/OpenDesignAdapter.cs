using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using PresenterConsole.Contracts;

namespace PresenterConsole.Desktop;

public sealed class OpenDesignAdapter : IPresentationAdapter
{
    private readonly OpenDesignProject project;
    private readonly System.Threading.Timer refreshTimer;
    private int expectedPosition;
    private int currentPosition;
    private string currentNotes = string.Empty;
    private IntPtr targetWindowHandle;

    public event EventHandler? StateChanged;
    public event EventHandler<string>? ErrorOccurred;
    event EventHandler? IPresentationAdapter.PresentationsChanged
    {
        add { }
        remove { }
    }

    public int CurrentShowPosition => currentPosition;
    public int SlideCount => project.PageCount;
    public string CurrentNotes => currentNotes;
    public IReadOnlyList<PresentationInfo> Presentations =>
    [
        new PresentationInfo(
            project.ArtifactPath,
            project.DisplayName,
            project.HtmlPath)
    ];
    public string? SelectedPresentationId => project.ArtifactPath;

    public OpenDesignAdapter(OpenDesignProject project)
    {
        this.project = project;
        expectedPosition = project.PageCount > 0 ? 1 : 0;
        currentPosition = expectedPosition;
        RefreshActualState(raiseEvent: false);
        refreshTimer = new System.Threading.Timer(
            _ => RefreshActualState(raiseEvent: true),
            null,
            TimeSpan.FromSeconds(1.5),
            TimeSpan.FromSeconds(1.5));
    }

    public bool SelectPresentation(string presentationId) =>
        string.Equals(presentationId, project.ArtifactPath, StringComparison.OrdinalIgnoreCase);

    public void Next() => SendNavigationKey("{RIGHT}", 1);

    public void Previous() => SendNavigationKey("{LEFT}", -1);

    public void GotoSlide(int slide)
    {
        if (slide < 1 || slide > SlideCount)
        {
            ReportError($"OpenDesign 頁碼超出範圍：{slide}");
            return;
        }

        if (!TrySetCdpPosition(slide))
        {
            ReportError("OpenDesign 目前不支援跳頁，請使用上一頁或下一頁");
            return;
        }

        expectedPosition = slide;
        RefreshActualState(raiseEvent: false);
        if (currentPosition == expectedPosition)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            ReportError("OpenDesign 跳頁後未取得實際頁碼，請重試");
        }
    }

    public void ActivateWindow()
    {
        if (TryActivateTargetWindow())
        {
            return;
        }

        targetWindowHandle = FindTargetWindowHandle();
        if (targetWindowHandle != IntPtr.Zero)
        {
            SetForegroundWindow(targetWindowHandle);
        }
    }

    public void StartPresentation(bool fromCurrentSlide)
    {
        ReportError("請先在 Open Design 手動開始播放（全螢幕）");
    }

    public void Dispose()
    {
        refreshTimer.Dispose();
    }

    private void SendNavigationKey(string key, int delta)
    {
        if (SlideCount == 0)
        {
            ReportError("OpenDesign 尚未載入 deck");
            return;
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                ActivateWindow();
                Thread.Sleep(300);
                SendKeys.SendWait(key);
                Thread.Sleep(400);
                if (!IsTargetWindowAlive())
                {
                    continue;
                }

                expectedPosition = Math.Clamp(expectedPosition + delta, 1, SlideCount);
                RefreshActualState(raiseEvent: true);
                return;
            }
            catch (InvalidOperationException exception)
            {
                if (attempt == 3)
                {
                    ReportError($"OpenDesign 換頁失敗：{exception.Message}");
                }
            }
        }
    }

    private void RefreshActualState(bool raiseEvent)
    {
        var actualPosition = TryGetCdpPosition();
        var nextPosition = actualPosition ?? expectedPosition;
        var nextNotes = ReadNotesForPosition(nextPosition);
        var changed = nextPosition != currentPosition || nextNotes != currentNotes;
        currentPosition = nextPosition;
        currentNotes = nextNotes;
        if (raiseEvent && changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private string ReadNotesForPosition(int position)
    {
        var notesPath = string.IsNullOrWhiteSpace(project.SpeakerPrivatePath)
            ? project.HtmlPath
            : project.SpeakerPrivatePath;
        return OpenDesignHtmlParser.ReadNotes(notesPath, position);
    }

    private int? TryGetCdpPosition()
    {
        const string expression =
            "(() => { "
            + "const c = [document.scrollingElement, document.body, "
            + "...document.querySelectorAll('*')]; "
            + "const el = c.find(e => e && e.scrollWidth > e.clientWidth) "
            + "|| document.scrollingElement; "
            + "return Math.floor(el.scrollLeft / window.innerWidth) + 1; "
            + "})()";
        if (!TryEvaluateCdp(expression, out var response))
        {
            return null;
        }

        if (!response.TryGetProperty("result", out var result)
            || !result.TryGetProperty("result", out var remoteResult)
            || !remoteResult.TryGetProperty("value", out var value)
            || !value.TryGetInt32(out var position))
        {
            return null;
        }

        return position > 0 && position <= SlideCount ? position : null;
    }

    private bool TrySetCdpPosition(int slide)
    {
        var left = (slide - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var expression = "(() => { "
            + "const c = [document.scrollingElement, document.body, "
            + "...document.querySelectorAll('*')]; "
            + "const el = c.find(e => e && e.scrollWidth > e.clientWidth) "
            + "|| document.scrollingElement; "
            + $"el.scrollLeft = {left} * window.innerWidth; "
            + "return el.scrollLeft; })()";
        return TryEvaluateCdp(expression, out _);
    }

    private bool TryEvaluateCdp(string expression, out JsonElement response)
    {
        response = default;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(250) };
            var targets = client.GetFromJsonAsync<JsonElement[]>(
                "http://127.0.0.1:9222/json").GetAwaiter().GetResult();
            if (targets is null)
            {
                return false;
            }

            var target = targets.FirstOrDefault(item =>
                item.TryGetProperty("webSocketDebuggerUrl", out _));
            if (target.ValueKind == JsonValueKind.Undefined
                || !target.TryGetProperty("webSocketDebuggerUrl", out var socketProperty))
            {
                return false;
            }

            using var socket = new ClientWebSocket();
            socket.ConnectAsync(
                new Uri(socketProperty.GetString()!),
                CancellationToken.None).GetAwaiter().GetResult();
            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = 1,
                method = "Runtime.evaluate",
                @params = new
                {
                    expression,
                    returnByValue = true
                }
            });
            socket.SendAsync(
                payload,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None).GetAwaiter().GetResult();
            var buffer = new byte[4096];
            var receiveResult = socket.ReceiveAsync(buffer, CancellationToken.None)
                .GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(
                buffer.AsMemory(0, receiveResult.Count));
            var root = document.RootElement;
            if (!root.TryGetProperty("result", out var commandResult)
                || commandResult.TryGetProperty("exceptionDetails", out _)
                || !commandResult.TryGetProperty("result", out _))
            {
                return false;
            }

            response = root.Clone();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool TryActivateTargetWindow()
    {
        return targetWindowHandle != IntPtr.Zero
            && IsWindow(targetWindowHandle)
            && SetForegroundWindow(targetWindowHandle);
    }

    private IntPtr FindTargetWindowHandle()
    {
        foreach (var processName in new[] { "OpenDesign", "electron" })
        {
            var process = Process.GetProcessesByName(processName)
                .FirstOrDefault(item => item.MainWindowHandle != IntPtr.Zero);
            if (process is not null)
            {
                return process.MainWindowHandle;
            }
        }

        var titleParts = new[]
        {
            Path.GetFileNameWithoutExtension(project.HtmlPath),
            project.DisplayName
        };
        foreach (var processName in new[] { "msedge", "chrome" })
        {
            var process = Process.GetProcessesByName(processName)
                .FirstOrDefault(item => item.MainWindowHandle != IntPtr.Zero
                    && titleParts.Any(part => item.MainWindowTitle.Contains(
                        part,
                        StringComparison.OrdinalIgnoreCase)));
            if (process is not null)
            {
                return process.MainWindowHandle;
            }
        }

        return IntPtr.Zero;
    }

    private bool IsTargetWindowAlive()
    {
        return targetWindowHandle != IntPtr.Zero && IsWindow(targetWindowHandle);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    private void ReportError(string message)
    {
        ErrorOccurred?.Invoke(this, message);
    }
}
