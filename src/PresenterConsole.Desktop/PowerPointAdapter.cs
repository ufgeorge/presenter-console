using System.Runtime.InteropServices;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace PresenterConsole.Desktop;

public sealed class PowerPointAdapter : IPresentationAdapter
{
    private readonly PowerPoint.Application application;
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
        catch (COMException exception)
        {
            ReportComFailure("ActivateWindow", exception);
        }
    }

    public void StartPresentation(bool fromCurrentSlide)
    {
        try
        {
            if (presentation is null)
            {
                LogDiagnostic("未開啟簡報，開始放映命令被忽略");
                return;
            }

            using var tracker = new COMReferenceTracker();
            LogDiagnostic("StartPresentation step=SlideShowSettings 取得 begin");
            dynamic settings = tracker.Track((object)presentation.SlideShowSettings);
            LogDiagnostic("StartPresentation step=SlideShowSettings 取得 succeeded");

            LogDiagnostic("StartPresentation step=StartingSlide 設定 begin");
            if (fromCurrentSlide)
            {
                dynamic activeWindow = tracker.Track((object)application.ActiveWindow);
                dynamic view = tracker.Track((object)activeWindow.View);
                dynamic slide = tracker.Track((object)view.Slide);
                settings.StartingSlide = (int)slide.SlideIndex;
            }
            else
            {
                settings.StartingSlide = 1;
            }
            LogDiagnostic("StartPresentation step=StartingSlide 設定 succeeded");

            LogDiagnostic("StartPresentation step=Run() begin");
            // Run() returns the live slideshow window. It must not be tracked by
            // the per-operation tracker, otherwise Dispose() closes the show.
            dynamic window = settings.Run();
            LogDiagnostic("StartPresentation step=Run() succeeded");

            LogDiagnostic("StartPresentation step=CurrentShowPosition 讀取 begin");
            dynamic showView = window.View;
            CurrentShowPosition = (int)showView.CurrentShowPosition;
            LogDiagnostic("StartPresentation step=CurrentShowPosition 讀取 succeeded");
            currentNotes = ReadNotes(CurrentShowPosition);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (COMException exception)
        {
            ReportComFailure("StartPresentation", exception);
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
        catch (COMException exception)
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
        catch (COMException exception)
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
                if ((int)shape.HasTextFrame != -1
                    || (int)shape.TextFrame.HasText != -1)
                {
                    continue;
                }

                dynamic textFrame = tracker.Track((object)shape.TextFrame);
                dynamic textRange = tracker.Track((object)textFrame.TextRange);
                notes.Add(textRange.Text);
            }

            return string.Join(Environment.NewLine, notes);
        }
        catch (COMException exception)
        {
            ReportComFailure("ReadNotes", exception);
            return string.Empty;
        }
    }

    public void Dispose()
    {
        try
        {
            application.SlideShowNextSlide -= OnSlideShowNextSlide;
            application.PresentationOpen -= OnPresentationOpen;
            application.PresentationClose -= OnPresentationClose;
        }
        catch (COMException exception)
        {
            LogComException(exception);
        }

        if (Marshal.IsComObject(presentation!))
        {
            Marshal.FinalReleaseComObject(presentation!);
        }

        if (Marshal.IsComObject(application))
        {
            Marshal.FinalReleaseComObject(application);
        }
    }
    private static void LogDiagnostic(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "presenter-console.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] PowerPoint adapter diagnostic: {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void ReportComFailure(string operation, COMException exception)
    {
        LogComException(exception);
        ErrorOccurred?.Invoke(this, IsPowerPointUnavailable(exception)
            ? "PowerPoint 已關閉，請重新開啟"
            : $"{operation} 失敗：{exception.Message}");
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

    private static bool IsPowerPointUnavailable(COMException exception) =>
        exception.HResult == unchecked((int)0x800706BA);

    private static void LogComException(Exception exception)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "presenter-console.log");
            var message = $"[{DateTime.Now:O}] PowerPoint adapter COM failure: {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}";
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