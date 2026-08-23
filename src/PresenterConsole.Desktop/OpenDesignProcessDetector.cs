using System.Diagnostics;

namespace PresenterConsole.Desktop;

public static class OpenDesignProcessDetector
{
    public static bool IsRunning()
    {
        foreach (var processName in new[] { "OpenDesign", "electron" })
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (string.Equals(processName, "OpenDesign", StringComparison.OrdinalIgnoreCase)
                        || process.MainWindowTitle.Contains(
                            "OpenDesign",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
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
