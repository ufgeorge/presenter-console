using System.Runtime.InteropServices;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace PresenterConsole.Desktop;

public sealed class PowerPointAdapter : IPresentationAdapter
{
    private readonly PowerPoint.Application application;
    private PowerPoint.Presentation? presentation;

    public event EventHandler? StateChanged;
    public event EventHandler<string>? ErrorOccurred;

    public int CurrentShowPosition { get; private set; }
    public int SlideCount => presentation?.Slides.Count ?? 0;
    public string CurrentNotes => ReadNotes(CurrentShowPosition);

    public PowerPointAdapter()
    {
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
        Attach(opened);
    }

    private void OnPresentationClose(PowerPoint.Presentation closed)
    {
        if (ReferenceEquals(presentation, closed))
        {
            presentation = null;
            CurrentShowPosition = 0;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Attach(PowerPoint.Presentation attached)
    {
        presentation = attached;
        RefreshActualState();
    }

    private void OnSlideShowNextSlide(PowerPoint.SlideShowWindow window)
    {
        try
        {
            CurrentShowPosition = window.View.CurrentShowPosition;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (COMException exception)
        {
            LogComException(exception);
        }
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
            using var tracker = new COMReferenceTracker();
            if (presentation?.SlideShowWindow is { } window)
            {
                tracker.Track(window).Activate();
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
            dynamic window = tracker.Track((object)settings.Run());
            LogDiagnostic("StartPresentation step=Run() succeeded");

            LogDiagnostic("StartPresentation step=CurrentShowPosition 讀取 begin");
            dynamic showView = tracker.Track((object)window.View);
            CurrentShowPosition = (int)showView.CurrentShowPosition;
            LogDiagnostic("StartPresentation step=CurrentShowPosition 讀取 succeeded");
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
            using var tracker = new COMReferenceTracker();
            if (presentation?.SlideShowWindow is not { } window)
            {
                LogDiagnostic("未在放映模式，命令被忽略");
                return;
            }

            var trackedWindow = tracker.Track(window);
            dynamic view = tracker.Track((object)trackedWindow.View);
            action(view);
            CurrentShowPosition = (int)view.CurrentShowPosition;
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
            using var tracker = new COMReferenceTracker();
            if (presentation?.SlideShowWindow is not { } window)
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            dynamic view = tracker.Track((object)tracker.Track(window).View);
            CurrentShowPosition = (int)view.CurrentShowPosition;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (COMException exception)
        {
            LogComException(exception);
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
            LogComException(exception);
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
        ErrorOccurred?.Invoke(this, $"{operation} 失敗：{exception.Message}");
    }
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