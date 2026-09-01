using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;
using PresenterConsole.Contracts;

namespace PresenterConsole.Desktop;

public sealed class PowerPointAdapter : IPresentationAdapter
{
    private PowerPoint.Application application;
    private readonly SynchronizationContext uiContext;
    private PowerPoint.Presentation? presentation;
    private readonly Dictionary<string, PowerPoint.Presentation> presentations = new(StringComparer.OrdinalIgnoreCase);
    private string? selectedPresentationId;
    private int slideCount;
    private string currentNotes = string.Empty;
    private IReadOnlyList<VideoInfo> currentVideos = [];
    private IntPtr videoWindowHandle;
    private string? playingVideoId;
    private Process? videoProcess;

    public event EventHandler? StateChanged;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? PresentationsChanged;

    public int CurrentShowPosition { get; private set; }
    public int SlideCount => slideCount;
    public string CurrentNotes => currentNotes;
    public IReadOnlyList<PresentationInfo> Presentations => presentations
        .Select(pair => new PresentationInfo(pair.Key, GetPresentationName(pair.Value), pair.Key))
        .ToArray();
    public string? SelectedPresentationId => selectedPresentationId;
    public IReadOnlyList<VideoInfo> Videos => currentVideos;

    public void PlayVideo(string videoId)
    {
        LogDiagnostic($"PlayVideo received videoId={TruncateForLog(videoId)}");
        var video = currentVideos.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, videoId, StringComparison.OrdinalIgnoreCase));
        if (video is null)
        {
            ErrorOccurred?.Invoke(this, "影片不在目前頁面");
            LogDiagnostic("PlayVideo rejected reason=not-in-current-slide");
            return;
        }

        try
        {
            videoProcess = Process.Start(new ProcessStartInfo(video.Id)
            {
                UseShellExecute = true
            });
            if (videoProcess is null)
            {
                ReportVideoError("影片播放器無法啟動");
                return;
            }

            LogDiagnostic($"PlayVideo started path={TruncateForLog(video.Id)} "
                + $"processId={videoProcess.Id}");
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                videoProcess.Refresh();
                videoWindowHandle = videoProcess.MainWindowHandle;
                LogDiagnostic($"PlayVideo handle attempt={attempt} "
                    + $"hwnd={videoWindowHandle}");
                if (videoWindowHandle != IntPtr.Zero)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            if (videoWindowHandle == IntPtr.Zero
                || !BringWindowToForeground(videoWindowHandle))
            {
                videoWindowHandle = IntPtr.Zero;
                ReportVideoError("影片播放器視窗無法帶到前景");
                return;
            }

            playingVideoId = video.Id;
            RefreshActualState();
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            ReportVideoError($"影片播放失敗：{exception.Message}");
            LogDiagnostic($"PlayVideo failed error={TruncateForLog(exception.Message)}");
        }
    }

    public void PauseResumeVideo()
    {
        LogDiagnostic($"PauseResumeVideo received hwnd={videoWindowHandle}");
        if (videoWindowHandle == IntPtr.Zero)
        {
            ReportVideoError("請先播放影片");
            return;
        }

        if (!IsWindow(videoWindowHandle))
        {
            videoWindowHandle = IntPtr.Zero;
            playingVideoId = null;
            ReportVideoError("影片播放器已關閉，請重新播放");
            RefreshActualState();
            return;
        }

        var focused = BringWindowToForeground(videoWindowHandle);
        LogDiagnostic($"PauseResumeVideo focus focused={focused} hwnd={videoWindowHandle}");
        if (!focused)
        {
            ReportVideoError("無法聚焦影片播放器，請手動點播放器視窗後再按");
            return;
        }

        try
        {
            Thread.Sleep(300);
            SendKeys.SendWait(" ");
            LogDiagnostic("PauseResumeVideo sendkeys space focused=true");
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or ArgumentException)
        {
            LogDiagnostic($"PauseResumeVideo sendkeys failed error={exception.Message}");
            ReportVideoError("影片播放器未接受暫停/繼續操作");
        }
    }

    public PowerPointAdapter(SynchronizationContext? uiContext = null)
    {
        this.uiContext = uiContext ?? SynchronizationContext.Current
            ?? throw new InvalidOperationException("PowerPoint adapter 必須在 UI thread 建立。");
        application = TryGetActiveApplication() ?? CreateApplication();
        application.PresentationOpen += OnPresentationOpen;
        application.PresentationClose += OnPresentationClose;
        application.SlideShowBegin += OnSlideShowBegin;
        application.SlideShowEnd += OnSlideShowEnd;
        application.SlideShowNextSlide += OnSlideShowNextSlide;

        foreach (PowerPoint.Presentation existing in application.Presentations)
        {
            Attach(existing);
        }
    }

    private static PowerPoint.Application CreateApplication()
    {
        var type = Type.GetTypeFromProgID("PowerPoint.Application")
            ?? throw new InvalidOperationException("找不到 Microsoft PowerPoint。");
        return (PowerPoint.Application)Activator.CreateInstance(type)!;
    }

    private static PowerPoint.Application? TryGetActiveApplication()
    {
        try
        {
            var type = Type.GetTypeFromProgID("PowerPoint.Application");
            if (type is null)
            {
                return null;
            }

            var classId = type.GUID;
            return GetActiveObject(ref classId, IntPtr.Zero, out var activeObject) == 0
                ? activeObject as PowerPoint.Application
                : null;
        }
        catch (COMException exception)
        {
            LogComException(exception);
            return null;
        }
    }

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(
        ref Guid classId,
        IntPtr reserved,
        [MarshalAs(UnmanagedType.Interface)] out object activeObject);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint sourceThreadId, uint targetThreadId, bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void keybd_event(
        byte virtualKey,
        byte scanCode,
        uint flags,
        UIntPtr extraInfo);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private const int SwRestore = 9;
    private const int RestoreDelayMilliseconds = 100;
    private const int ShiftVirtualKey = 0x10;
    private const int F5VirtualKey = 0x74;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint KeyEventKeyUp = 0x0002;
    private const int ShiftActivationDelayMilliseconds = 50;

    private void SubscribeToApplication(PowerPoint.Application target)
    {
        target.PresentationOpen += OnPresentationOpen;
        target.PresentationClose += OnPresentationClose;
        target.SlideShowBegin += OnSlideShowBegin;
        target.SlideShowEnd += OnSlideShowEnd;
        target.SlideShowNextSlide += OnSlideShowNextSlide;
    }

    private void UnsubscribeFromApplication(PowerPoint.Application target)
    {
        try
        {
            target.PresentationOpen -= OnPresentationOpen;
            target.PresentationClose -= OnPresentationClose;
            target.SlideShowBegin -= OnSlideShowBegin;
            target.SlideShowEnd -= OnSlideShowEnd;
            target.SlideShowNextSlide -= OnSlideShowNextSlide;
        }
        catch (COMException exception)
        {
            LogComException(exception);
        }
    }

    private void AttachOpenPresentations(PowerPoint.Application target)
    {
        foreach (PowerPoint.Presentation existing in target.Presentations)
        {
            Attach(existing);
        }
    }

    private bool TryReconnectPowerPoint()
    {
        try
        {
            LogDiagnostic("PowerPoint RPC 斷線，重新取得 application begin");
            var reconnected = TryGetActiveApplication();
            if (reconnected is null)
            {
                LogDiagnostic("PowerPoint RPC 斷線，重新取得 application failed");
                return false;
            }

            UnsubscribeFromApplication(application);
            application = reconnected;
            SubscribeToApplication(application);
            presentations.Clear();
            presentation = null;
            selectedPresentationId = null;
            AttachOpenPresentations(application);
            LogDiagnostic("PowerPoint RPC 斷線，重新取得 application succeeded");
            return true;
        }
        catch (COMException exception)
        {
            LogComException(exception);
            return false;
        }
    }
    private void OnPresentationOpen(PowerPoint.Presentation opened)
    {
        PostToUi(() => Attach(opened));
    }

    private void OnPresentationClose(PowerPoint.Presentation closed)
    {
        PostToUi(() =>
        {
            var closedId = GetPresentationId(closed);
            if (closedId is not null)
            {
                presentations.Remove(closedId);
            }

            if (ReferenceEquals(presentation, closed)
                || (closedId is not null && string.Equals(selectedPresentationId, closedId, StringComparison.OrdinalIgnoreCase)))
            {
                presentation = presentations.Values.FirstOrDefault();
                selectedPresentationId = presentation is null ? null : GetPresentationId(presentation);
                UpdateSelectedPresentationState();
                RefreshActualState();
            }

            PresentationsChanged?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void Attach(PowerPoint.Presentation attached)
    {
        var id = GetPresentationId(attached);
        if (id is null)
        {
            return;
        }

        presentations[id] = attached;
        if (presentation is null || selectedPresentationId is null)
        {
            presentation = attached;
            selectedPresentationId = id;
            UpdateSelectedPresentationState();
        }

        PresentationsChanged?.Invoke(this, EventArgs.Empty);
        RefreshActualState();
    }

    public bool SelectPresentation(string presentationId)
    {
        if (!presentations.TryGetValue(presentationId, out var selected))
        {
            ErrorOccurred?.Invoke(this, "找不到選定的簡報，請重新整理清單");
            return false;
        }

        presentation = selected;
        selectedPresentationId = presentationId;
        UpdateSelectedPresentationState();
        PresentationsChanged?.Invoke(this, EventArgs.Empty);
        RefreshActualState();
        return true;
    }

    private void UpdateSelectedPresentationState()
    {
        try
        {
            slideCount = presentation?.Slides.Count ?? 0;
        }
        catch (COMException exception)
        {
            ReportComFailure("選取簡報", exception);
            slideCount = 0;
        }
    }

    private static string? GetPresentationId(PowerPoint.Presentation target)
    {
        try
        {
            return string.IsNullOrWhiteSpace(target.FullName) ? target.Name : target.FullName;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string GetPresentationName(PowerPoint.Presentation target)
    {
        try { return target.Name; }
        catch (COMException) { return "（無法讀取檔名）"; }
    }

    private void OnSlideShowNextSlide(PowerPoint.SlideShowWindow window)
    {
        PostToUi(() =>
        {
            try
            {
                if (!IsSelectedPresentation(window.Presentation))
                {
                    return;
                }

                CurrentShowPosition = window.View.CurrentShowPosition;
                currentNotes = ReadNotes(CurrentShowPosition);
                RefreshCurrentVideos();
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (COMException exception)
            {
                ReportComFailure("SlideShowNextSlide", exception);
            }
        });
    }

    private void OnSlideShowBegin(PowerPoint.SlideShowWindow window)
    {
        LogDiagnostic("SlideShowBegin 觸發");
    }

    private void OnSlideShowEnd(PowerPoint.Presentation endedPresentation)
    {
        LogDiagnostic("SlideShowEnd 觸發");
        PostToUi(() =>
        {
            if (IsSelectedPresentation(endedPresentation))
            {
                CurrentShowPosition = 0;
                currentNotes = string.Empty;
                currentVideos = [];
                playingVideoId = null;
                videoWindowHandle = IntPtr.Zero;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private bool IsSelectedPresentation(PowerPoint.Presentation target)
    {
        var targetId = GetPresentationId(target);
        return targetId is not null
            && string.Equals(targetId, selectedPresentationId, StringComparison.OrdinalIgnoreCase);
    }
    public void Next() => InvokeView("Next", view => view.Next());
    public void Previous() => InvokeView("Previous", view => view.Previous());

    public void GotoSlide(int slide)
    {
        if (slide > 0)
        {
            InvokeView("GotoSlide", view => view.GotoSlide(slide));
        }
    }

    public void ActivateWindow()
    {
        try
        {
            ActivateSelectedPresentationWindow(out _, bringToForeground: true);
            if (TryGetSlideShowWindow() is { } window)
            {
                // SlideShowWindow is owned by PowerPoint's running show. Do not
                // FinalRelease it at the end of this operation.
                window.Activate();
            }

            RefreshActualState();
        }
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException)
        {
            ReportComFailure("ActivateWindow", exception);
        }
    }

    public void StartPresentation(bool fromCurrentSlide)
    {
        if (presentation is null)
        {
            LogDiagnostic("未開啟簡報，開始放映命令被忽略");
            return;
        }

        LogDiagnostic(
            $"StartPresentation path=PostMessage fromCurrent={fromCurrentSlide}");

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var activationResult = ActivateSelectedPresentationWindow(
                    out var targetWindowHandle,
                    bringToForeground: false);
                if (activationResult == WindowActivationResult.NotFound)
                {
                    ErrorOccurred?.Invoke(this, "找不到選定簡報的文件視窗，請先開啟該簡報");
                    return;
                }

                if (targetWindowHandle == IntPtr.Zero)
                {
                    LogDiagnostic(
                        $"StartPresentation attempt={attempt} document HWND missing");
                    continue;
                }

                LogDiagnostic(
                    $"StartPresentation attempt={attempt} document HWND="
                    + targetWindowHandle);
                if (!PostStartPresentationKey(targetWindowHandle, fromCurrentSlide))
                {
                    LogDiagnostic(
                        $"StartPresentation attempt={attempt} PostMessage failed");
                    continue;
                }

                LogDiagnostic($"StartPresentation attempt={attempt} F5 posted");
                Thread.Sleep(400);

                if (IsSlideShowWindowAlive())
                {
                    LogDiagnostic($"StartPresentation attempt={attempt} window alive");
                    RefreshActualState();
                    return;
                }

                LogDiagnostic($"StartPresentation attempt={attempt} window missing");
            }
            catch (COMException exception) when (IsPowerPointUnavailable(exception))
            {
                LogDiagnostic($"StartPresentation attempt={attempt} PowerPoint RPC disconnected");
                if (!TryReconnectPowerPoint())
                {
                    ReportComFailure("StartPresentation", exception);
                    return;
                }
            }
            catch (Exception exception) when (
                exception is COMException or InvalidComObjectException)
            {
                ReportComFailure("StartPresentation", exception);
                return;
            }
        }

        LogDiagnostic("StartPresentation PostMessage 重試耗盡；不使用 COM 放映 fallback");
        ErrorOccurred?.Invoke(
            this,
            "開始簡報失敗，請手動在電腦按 F5");
    }

    private static bool PostStartPresentationKey(IntPtr windowHandle, bool fromCurrentSlide)
    {
        var shiftDown = !fromCurrentSlide
            || PostMessage(windowHandle, WmKeyDown, (IntPtr)ShiftVirtualKey, IntPtr.Zero);
        if (!shiftDown)
        {
            return false;
        }

        var f5Down = PostMessage(windowHandle, WmKeyDown, (IntPtr)F5VirtualKey, IntPtr.Zero);
        var f5Up = f5Down
            && PostMessage(windowHandle, WmKeyUp, (IntPtr)F5VirtualKey, IntPtr.Zero);
        var shiftUp = !fromCurrentSlide
            || PostMessage(windowHandle, WmKeyUp, (IntPtr)ShiftVirtualKey, IntPtr.Zero);
        return f5Up && shiftUp;
    }
    private bool IsSlideShowWindowAlive()
    {
        return TryGetSlideShowWindow() is not null;
    }

    private enum WindowActivationResult
    {
        Success,
        NotFound
    }

    private WindowActivationResult ActivateSelectedPresentationWindow(
        out IntPtr targetWindowHandle,
        bool bringToForeground)
    {
        targetWindowHandle = IntPtr.Zero;
        if (presentation is null)
        {
            return WindowActivationResult.NotFound;
        }

        var selectedId = selectedPresentationId;
        if (selectedId is null)
        {
            return WindowActivationResult.NotFound;
        }

        if (bringToForeground)
        {
            application.Activate();
        }
        foreach (PowerPoint.DocumentWindow window in application.Windows)
        {
            try
            {
                var windowPresentation = window.Presentation;
                if (string.Equals(GetPresentationId(windowPresentation), selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    targetWindowHandle = new IntPtr(window.HWND);
                    if (!bringToForeground)
                    {
                        return WindowActivationResult.Success;
                    }

                    window.Activate();
                    return BringWindowToForeground(targetWindowHandle)
                        ? WindowActivationResult.Success
                        : WindowActivationResult.NotFound;
                }
            }
            catch (COMException exception)
            {
                LogComException(exception);
            }
        }

        return WindowActivationResult.NotFound;
    }

    private bool BringWindowToForeground(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            LogDiagnostic("BringWindowToForeground step=validate-hwnd result=failed hwnd=0");
            return false;
        }

        if (IsIconic(windowHandle))
        {
            var restored = ShowWindow(windowHandle, SwRestore);
            LogDiagnostic(
                $"BringWindowToForeground step=restore result={(restored ? "success" : "failed")} "
                + $"hwnd={windowHandle}");
            Thread.Sleep(RestoreDelayMilliseconds);
        }
        else
        {
            LogDiagnostic(
                "BringWindowToForeground step=restore result=not-needed "
                + $"hwnd={windowHandle}");
        }

        keybd_event(ShiftVirtualKey, 0, 0, UIntPtr.Zero);
        keybd_event(ShiftVirtualKey, 0, KeyEventKeyUp, UIntPtr.Zero);
        LogDiagnostic(
            "BringWindowToForeground step=shift-foreground-pierce result=success "
            + $"hwnd={windowHandle}");
        Thread.Sleep(ShiftActivationDelayMilliseconds);

        var targetThreadId = GetWindowThreadProcessId(windowHandle, IntPtr.Zero);
        var currentThreadId = GetCurrentThreadId();
        LogDiagnostic(
            "BringWindowToForeground step=get-window-thread "
            + $"result={(targetThreadId == 0 ? "failed" : "success")} "
            + $"hwnd={windowHandle} targetThreadId={targetThreadId} "
            + $"currentThreadId={currentThreadId}");
        if (targetThreadId == 0)
        {
            return false;
        }

        var needsAttach = targetThreadId != currentThreadId;
        var attached = needsAttach
            && AttachThreadInput(currentThreadId, targetThreadId, true);
        LogDiagnostic(
            "BringWindowToForeground step=attach-thread-input "
            + $"result={(needsAttach ? (attached ? "success" : "failed") : "not-needed")} "
            + $"sourceThreadId={currentThreadId} targetThreadId={targetThreadId}");
        try
        {
            var setForeground = SetForegroundWindow(windowHandle);
            var actualForeground = GetForegroundWindow();
            var verified = actualForeground == windowHandle;
            LogDiagnostic(
                "BringWindowToForeground step=set-foreground "
                + $"result={(setForeground ? "success" : "failed")} "
                + $"hwnd={windowHandle}");
            LogDiagnostic(
                "BringWindowToForeground step=verify-foreground "
                + $"result={(verified ? "success" : "failed")} "
                + $"actualHwnd={actualForeground} targetHwnd={windowHandle}");
            return setForeground && verified;
        }
        finally
        {
            if (attached)
            {
                var detached = AttachThreadInput(currentThreadId, targetThreadId, false);
                LogDiagnostic(
                    "BringWindowToForeground step=detach-thread-input "
                    + $"result={(detached ? "success" : "failed")} "
                    + $"sourceThreadId={currentThreadId} targetThreadId={targetThreadId}");
            }
        }
    }

    private void InvokeView(string operation, Action<dynamic> action)
    {
        try
        {
            if (TryGetSlideShowWindow() is not { } window)
            {
                LogDiagnostic("未在放映模式，命令被忽略");
                return;
            }

            // The window and view belong to the active slideshow and must remain
            // valid after this operation returns.
            dynamic view = window.View;
            action(view);
            CurrentShowPosition = (int)view.CurrentShowPosition;
            currentNotes = ReadNotes(CurrentShowPosition);
            RefreshCurrentVideos();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException)
        {
            ReportComFailure(operation, exception);
        }
    }

    private void RefreshActualState()
    {
        try
        {
            if (TryGetSlideShowWindow() is not { } window)
            {
                CurrentShowPosition = 0;
                currentNotes = string.Empty;
                currentVideos = [];
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            dynamic view = window.View;
            CurrentShowPosition = (int)view.CurrentShowPosition;
            currentNotes = ReadNotes(CurrentShowPosition);
            RefreshCurrentVideos();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException)
        {
            ReportComFailure("RefreshActualState", exception);
        }
    }

    private PowerPoint.SlideShowWindow? TryGetSlideShowWindow()
    {
        try
        {
            return presentation?.SlideShowWindow;
        }
        catch (COMException exception) when (IsNoSlideShowView(exception))
        {
            return null;
        }
    }

    private static bool IsNoSlideShowView(COMException exception) =>
        exception.Message.Contains("There is currently no slide show view", StringComparison.OrdinalIgnoreCase);

    private string ReadNotes(int position)
    {
        if (presentation is null || position < 1 || position > SlideCount)
        {
            return string.Empty;
        }

        using var tracker = new COMReferenceTracker();
        try
        {
            var slide = tracker.Track(presentation.Slides[position]);
            var notesPage = tracker.Track(slide.NotesPage);
            var notes = new List<string>();

            for (var index = 1; index <= notesPage.Shapes.Count; index++)
            {
                dynamic shape = tracker.Track((object)notesPage.Shapes[index]);
                if (IsSlideNumberPlaceholder(shape, tracker))
                {
                    continue;
                }

                if ((int)shape.HasTextFrame != -1
                    || (int)shape.TextFrame.HasText != -1)
                {
                    continue;
                }

                dynamic textFrame = tracker.Track((object)shape.TextFrame);
                dynamic textRange = tracker.Track((object)textFrame.TextRange);
                var paragraphs = new List<string>();
                var paragraphCount = (int)textRange.Paragraphs().Count;
                for (var paragraphIndex = 1;
                     paragraphIndex <= paragraphCount;
                     paragraphIndex++)
                {
                    dynamic paragraph = tracker.Track(
                        (object)textRange.Paragraphs(paragraphIndex, 1));
                    var text = ((string)paragraph.Text)
                        .Replace("\v", "\n")
                        .Replace("\r", "\n")
                        .TrimEnd('\n');
                    paragraphs.Add(TryReadBulletPrefix(paragraph) + text);
                }

                notes.Add(string.Join(Environment.NewLine, paragraphs));
            }

            return string.Join(Environment.NewLine, notes);
        }
        catch (COMException exception)
        {
            ReportComFailure("ReadNotes", exception);
            return string.Empty;
        }
    }

    private static string TryReadBulletPrefix(dynamic paragraph)
    {
        try
        {
            dynamic bullet = paragraph.ParagraphFormat.Bullet;
            return NotesTextBuilder.BuildParagraphPrefix(
                (int)bullet.Type,
                (int)bullet.Visible,
                (int)bullet.Character,
                (int)bullet.Number);
        }
        catch (COMException)
        {
            return string.Empty;
        }
    }

    private void RefreshCurrentVideos()
    {
        if (playingVideoId is not null && !IsWindow(videoWindowHandle))
        {
            playingVideoId = null;
            videoWindowHandle = IntPtr.Zero;
        }

        var directory = GetPresentationDirectory();
        var videos = directory is null
            ? []
            : OpenDesignHtmlParser.ExtractVideos(currentNotes, directory, LogDiagnostic);
        currentVideos = videos.Select(video => video with
        {
            Playing = string.Equals(
                video.Id,
                playingVideoId,
                StringComparison.OrdinalIgnoreCase)
        }).ToArray();
    }

    private string? GetPresentationDirectory()
    {
        try
        {
            return presentation is null
                ? null
                : Path.GetDirectoryName(Path.GetFullPath(presentation.FullName));
        }
        catch (COMException exception)
        {
            ReportComFailure("取得簡報資料夾", exception);
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private void ReportVideoError(string message)
    {
        ErrorOccurred?.Invoke(this, message);
        LogDiagnostic($"Video error={TruncateForLog(message)}");
    }

    private static bool IsSlideNumberPlaceholder(dynamic shape, COMReferenceTracker tracker)
    {
        try
        {
            if ((int)shape.Type != 14)
            {
                return false;
            }

            dynamic placeholderFormat = tracker.Track((object)shape.PlaceholderFormat);
            return (int)placeholderFormat.Type == 13;
        }
        catch (COMException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            if (application is not null)
            {
                application.SlideShowNextSlide -= OnSlideShowNextSlide;
                application.SlideShowBegin -= OnSlideShowBegin;
                application.SlideShowEnd -= OnSlideShowEnd;
                application.PresentationOpen -= OnPresentationOpen;
                application.PresentationClose -= OnPresentationClose;
            }
        }
        catch (ArgumentNullException exception)
        {
            LogComException(exception);
        }
        catch (COMException exception)
        {
            LogComException(exception);
        }

    }
    private static void LogDiagnostic(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "presenter-console.log");
            var logMessage = $"[{DateTime.Now:O}] PowerPoint adapter diagnostic: "
                + message
                + Environment.NewLine;
            File.AppendAllText(logPath, logMessage);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string TruncateForLog(string value, int maxLength = 160)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ');
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
    }

    private void ReportComFailure(string operation, Exception exception)
    {
        LogComException(exception);
        if (IsPowerPointUnavailable(exception))
        {
            LogDiagnostic($"{operation} 發生 PowerPoint RPC 斷線");
            if (TryReconnectPowerPoint())
            {
                ErrorOccurred?.Invoke(this, "PowerPoint 連線已恢復，請重試");
                return;
            }

            ErrorOccurred?.Invoke(this, "PowerPoint 已關閉，請重新開啟");
            return;
        }

        ErrorOccurred?.Invoke(this, $"{operation} 失敗：{exception.Message}");
    }
    private void PostToUi(Action action)
    {
        uiContext.Post(_ =>
        {
            try
            {
                action();
            }
            catch (COMException exception)
            {
                ReportComFailure("COM 事件", exception);
            }
        }, null);
    }

    private static bool IsPowerPointUnavailable(Exception exception) =>
        exception.HResult == unchecked((int)0x800706BA);

    private static void LogComException(Exception exception)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "presenter-console.log");
            var message = $"[{DateTime.Now:O}] PowerPoint adapter COM failure: "
                + $"{exception.GetType().FullName}: {exception.Message}"
                + Environment.NewLine;
            File.AppendAllText(logPath, message);
        }
        catch (IOException)
        {
            // Logging must not prevent presentation control from continuing.
        }
        catch (UnauthorizedAccessException)
        {
            // Logging must not prevent presentation control from continuing.
        }
    }
}
