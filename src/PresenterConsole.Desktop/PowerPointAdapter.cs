using System.Runtime.InteropServices;
using System.Windows.Forms;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace PresenterConsole.Desktop;

public sealed class PowerPointAdapter : IPresentationAdapter
{
    private PowerPoint.Application application;
    private readonly SynchronizationContext uiContext;
    private PowerPoint.Presentation? presentation;
    private int slideCount;
    private string currentNotes = string.Empty;

    public event EventHandler? StateChanged;
    public event EventHandler<string>? ErrorOccurred;

    public int CurrentShowPosition { get; private set; }
    public int SlideCount => slideCount;
    public string CurrentNotes => currentNotes;

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
            if (ReferenceEquals(presentation, closed))
            {
                presentation = null;
                slideCount = 0;
                currentNotes = string.Empty;
                CurrentShowPosition = 0;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void Attach(PowerPoint.Presentation attached)
    {
        presentation = attached;
        slideCount = attached.Slides.Count;
        RefreshActualState();
    }

    private void OnSlideShowNextSlide(PowerPoint.SlideShowWindow window)
    {
        PostToUi(() =>
        {
            try
            {
                CurrentShowPosition = window.View.CurrentShowPosition;
                currentNotes = ReadNotes(CurrentShowPosition);
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
            if (ReferenceEquals(presentation, endedPresentation))
            {
                CurrentShowPosition = 0;
                currentNotes = string.Empty;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        });
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
            application.Activate();
            if (presentation?.SlideShowWindow is { } window)
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

        var key = fromCurrentSlide ? "+{F5}" : "{F5}";
        LogDiagnostic($"StartPresentation path=SendKeys key={key}");

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                application.Activate();
                LogDiagnostic(
                    $"StartPresentation attempt={attempt} Activate succeeded; "
                    + "waiting for focus");
                Thread.Sleep(300);
                SendKeys.SendWait(key);
                LogDiagnostic($"StartPresentation attempt={attempt} F5 sent");
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
            catch (InvalidOperationException exception)
            {
                LogDiagnostic(
                    $"StartPresentation attempt={attempt} SendKeys 失敗："
                    + exception.Message);
            }
        }

        LogDiagnostic("StartPresentation SendKeys 重試耗盡；不使用 COM 放映 fallback");
        ErrorOccurred?.Invoke(this, "開始簡報失敗，請手動在電腦按 F5");
    }
    private bool IsSlideShowWindowAlive()
    {
        try
        {
            return presentation?.SlideShowWindow is not null;
        }
        catch (COMException exception)
        {
            LogComException(exception);
            return false;
        }
    }

    private void InvokeView(string operation, Action<dynamic> action)
    {
        try
        {
            if (presentation?.SlideShowWindow is not { } window)
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
            if (presentation?.SlideShowWindow is not { } window)
            {
                CurrentShowPosition = 0;
                currentNotes = string.Empty;
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            dynamic view = window.View;
            CurrentShowPosition = (int)view.CurrentShowPosition;
            currentNotes = ReadNotes(CurrentShowPosition);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException)
        {
            ReportComFailure("RefreshActualState", exception);
        }
    }

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
                var normalizedText = ((string)textRange.Text)
                    .Replace("\v", "\n")
                    .Replace("\r", "\n")
                    .TrimEnd('\n');
                notes.Add(normalizedText);
            }

            return string.Join(Environment.NewLine, notes);
        }
        catch (COMException exception)
        {
            ReportComFailure("ReadNotes", exception);
            return string.Empty;
        }
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
