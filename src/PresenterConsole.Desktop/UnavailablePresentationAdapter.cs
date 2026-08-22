namespace PresenterConsole.Desktop;

public sealed class UnavailablePresentationAdapter : IPresentationAdapter
{
    event EventHandler? IPresentationAdapter.StateChanged
    {
        add { }
        remove { }
    }

    public int CurrentShowPosition => 0;
    public int SlideCount => 0;
    public string CurrentNotes => string.Empty;

    public void Next()
    {
    }

    public void Previous()
    {
    }

    public void GotoSlide(int slide)
    {
    }

    public void ActivateWindow()
    {
    }

    public void Dispose()
    {
    }
}