using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace PresenterConsole.Desktop;

public sealed class OpenDesignAdapter : IPresentationAdapter
{
    private readonly OpenDesignProject project;
    private int expectedPosition;
    private string currentNotes = string.Empty;

    public event EventHandler? StateChanged;
    public event EventHandler<string>? ErrorOccurred;

    public int CurrentShowPosition => TryGetCdpPosition() ?? expectedPosition;
    public int SlideCount => project.PageCount;
    public string CurrentNotes
    {
        get
        {
            var actualPosition = TryGetCdpPosition();
            return actualPosition is int position
                ? ReadNotesForPosition(position)
                : currentNotes;
        }
    }

    public OpenDesignAdapter(OpenDesignProject project)
    {
        this.project = project;
        expectedPosition = project.PageCount > 0 ? 1 : 0;
        RefreshNotes();
    }

    public void Next() => SendNavigationKey("{RIGHT}", 1);

    public void Previous() => SendNavigationKey("{LEFT}", -1);

    public void GotoSlide(int slide)
    {
        if (slide >= 1 && slide <= SlideCount)
        {
            expectedPosition = slide;
            RefreshNotes();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ActivateWindow()
    {
        foreach (var processName in new[] { "OpenDesign", "electron", "msedge", "chrome" })
        {
            var process = Process.GetProcessesByName(processName)
                .FirstOrDefault(item => item.MainWindowHandle != IntPtr.Zero);
            if (process is not null && SetForegroundWindow(process.MainWindowHandle))
            {
                return;
            }
        }
    }

    public void StartPresentation(bool fromCurrentSlide)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = project.HtmlPath,
                UseShellExecute = true
            });
            Thread.Sleep(300);
            SendKeys.SendWait("f");
            Thread.Sleep(400);
            RefreshNotes();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (InvalidOperationException exception)
        {
            ReportError($"開啟 OpenDesign deck 失敗：{exception.Message}");
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            ReportError($"開啟 OpenDesign deck 失敗：{exception.Message}");
        }
    }

    public void Dispose()
    {
    }

    private void SendNavigationKey(string key, int delta)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                ActivateWindow();
                Thread.Sleep(300);
                SendKeys.SendWait(key);
                Thread.Sleep(400);
                if (!IsBrowserAlive())
                {
                    continue;
                }

                expectedPosition = Math.Clamp(expectedPosition + delta, 1, SlideCount);
                RefreshNotes();
                StateChanged?.Invoke(this, EventArgs.Empty);
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

    private void RefreshNotes()
    {
        currentNotes = ReadNotesForPosition(expectedPosition);
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
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(250) };
            var targets = client.GetFromJsonAsync<JsonElement[]>(
                "http://127.0.0.1:9222/json").GetAwaiter().GetResult();
            if (targets is null)
            {
                return null;
            }

            var target = targets.FirstOrDefault(item =>
                item.TryGetProperty("webSocketDebuggerUrl", out _));
            if (target.ValueKind == JsonValueKind.Undefined
                || !target.TryGetProperty("webSocketDebuggerUrl", out var socketProperty))
            {
                return null;
            }

            using var socket = new ClientWebSocket();
            socket.ConnectAsync(
                new Uri(socketProperty.GetString()!),
                CancellationToken.None).GetAwaiter().GetResult();
            var request = Encoding.UTF8.GetBytes(
                "{\"id\":1,\"method\":\"Runtime.evaluate\","
                + "\"params\":{\"expression\":"
                + "\"Math.floor(scrollLeft / innerWidth) + 1\","
                + "\"returnByValue\":true}}");
            socket.SendAsync(
                request,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None).GetAwaiter().GetResult();
            var buffer = new byte[4096];
            var result = socket.ReceiveAsync(buffer, CancellationToken.None)
                .GetAwaiter().GetResult();
            using var response = JsonDocument.Parse(buffer.AsMemory(0, result.Count));
            var value = response.RootElement
                .GetProperty("result")
                .GetProperty("result")
                .GetProperty("value")
                .GetInt32();
            return value > 0 && value <= SlideCount ? value : null;
        }
        catch (Exception exception) when (IsCdpConnectionFailure(exception))
        {
            return null;
        }
    }

    private static bool IsCdpConnectionFailure(Exception exception)
    {
        return exception is HttpRequestException
            || exception is TaskCanceledException
            || exception is WebSocketException
            || exception is JsonException
            || exception is InvalidOperationException
            || exception is SocketException;
    }

    private static bool IsBrowserAlive()
    {
        return Process.GetProcessesByName("OpenDesign").Length > 0
            || Process.GetProcessesByName("electron").Length > 0
            || Process.GetProcessesByName("msedge").Length > 0
            || Process.GetProcessesByName("chrome").Length > 0;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    private void ReportError(string message)
    {
        ErrorOccurred?.Invoke(this, message);
    }
}
