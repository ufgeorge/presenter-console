using System.Runtime.InteropServices;

namespace PresenterConsole.Desktop;

public sealed class COMReferenceTracker : IDisposable
{
    private readonly List<object> references = [];

    public T Track<T>(T value) where T : class
    {
        references.Add(value);
        return value;
    }

    public void Dispose()
    {
        for (var index = references.Count - 1; index >= 0; index--)
        {
            if (Marshal.IsComObject(references[index]))
            {
                Marshal.FinalReleaseComObject(references[index]);
            }
        }

        references.Clear();
    }
}