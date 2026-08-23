using System.Diagnostics;

namespace PresenterConsole.Desktop;

public static class OpenDesignProcessDetector
{
    private static readonly string[] ProcessNameCandidates =
    [
        "OpenDesign",
        "opendesign",
        "OpenDesignApp",
        "open-design",
        "electron"
    ];

    public static bool IsRunning()
    {
        foreach (var processName in ProcessNameCandidates)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!string.Equals(processName, "electron", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (string.Equals(processName, "electron", StringComparison.OrdinalIgnoreCase)
                        && (process.MainWindowTitle.Contains(
                                "OpenDesign",
                                StringComparison.OrdinalIgnoreCase)
                            || process.MainWindowTitle.Contains(
                                "Open Design",
                                StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return false;
    }
}
