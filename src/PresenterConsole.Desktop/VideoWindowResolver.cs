using System.ComponentModel;
using System.Diagnostics;

namespace PresenterConsole.Desktop;

public sealed record VideoProcessSnapshot(
    string ProcessName,
    IntPtr MainWindowHandle,
    bool HasExited);

public sealed record VideoWindowResolution(IntPtr Handle, string Source);

public static class VideoWindowResolver
{
    public static VideoWindowResolution? Resolve(
        VideoProcessSnapshot? launchedProcess,
        string? targetProcessName,
        IntPtr trackedWindowHandle,
        Func<IntPtr, bool> isWindow,
        IEnumerable<VideoProcessSnapshot> existingProcesses,
        Action<string> log)
    {
        if (launchedProcess is { MainWindowHandle: not 0 } launched
            && IsUsable(launched.MainWindowHandle, isWindow))
        {
            log($"source=launched-process hwnd={launched.MainWindowHandle}");
            return new VideoWindowResolution(
                launched.MainWindowHandle,
                "launched-process");
        }

        if (launchedProcess is { HasExited: true })
        {
            log("source=launched-process result=exited");
        }
        else if (launchedProcess is not null)
        {
            log("source=launched-process result=no-window");
        }

        if (trackedWindowHandle != IntPtr.Zero
            && IsUsable(trackedWindowHandle, isWindow))
        {
            log($"source=tracked-window hwnd={trackedWindowHandle}");
            return new VideoWindowResolution(trackedWindowHandle, "tracked-window");
        }

        if (!string.IsNullOrEmpty(targetProcessName))
        {
            var existing = existingProcesses.FirstOrDefault(process =>
                string.Equals(
                    process.ProcessName,
                    targetProcessName,
                    StringComparison.OrdinalIgnoreCase)
                && process.MainWindowHandle != IntPtr.Zero
                && IsUsable(process.MainWindowHandle, isWindow));
            if (existing is not null)
            {
                log($"source=same-process-existing-window hwnd={existing.MainWindowHandle}");
                return new VideoWindowResolution(
                    existing.MainWindowHandle,
                    "same-process-existing-window");
            }
        }

        log("source=none result=no-valid-window");
        return null;
    }

    public static string? TryGetProcessName(Process process, Action<string> log)
    {
        try
        {
            process.Refresh();
            return process.ProcessName;
        }
        catch (Exception exception) when (IsProcessQueryFailure(exception))
        {
            log($"process-name-query failed error={exception.GetType().Name}");
            return null;
        }
    }

    public static VideoProcessSnapshot? TrySnapshot(Process process, Action<string> log)
    {
        try
        {
            process.Refresh();
            return new VideoProcessSnapshot(
                process.ProcessName,
                process.MainWindowHandle,
                process.HasExited);
        }
        catch (Exception exception) when (IsProcessQueryFailure(exception))
        {
            log($"process-query failed error={exception.GetType().Name}");
            return null;
        }
    }

    public static IReadOnlyList<VideoProcessSnapshot> GetExistingProcesses(
        string processName,
        Action<string> log)
    {
        var snapshots = new List<VideoProcessSnapshot>();
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception exception) when (IsProcessQueryFailure(exception))
        {
            log($"process-enumeration failed error={exception.GetType().Name}");
            return snapshots;
        }

        foreach (var process in processes)
        {
            try
            {
                var snapshot = TrySnapshot(process, log);
                if (snapshot is not null)
                {
                    snapshots.Add(snapshot);
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        return snapshots;
    }

    private static bool IsUsable(IntPtr handle, Func<IntPtr, bool> isWindow)
    {
        try
        {
            return isWindow(handle);
        }
        catch (Exception exception) when (IsProcessQueryFailure(exception))
        {
            return false;
        }
    }

    private static bool IsProcessQueryFailure(Exception exception)
    {
        return exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException
            or System.Security.SecurityException;
    }
}
