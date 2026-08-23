using PresenterConsole.Contracts;

namespace PresenterConsole.Desktop;

public sealed class UnavailablePresentationAdapter : IPresentationAdapter
{
    event EventHandler? IPresentationAdapter.StateChanged
    {
        add { }
        remove { }
    }

    event EventHandler<string>? IPresentationAdapter.ErrorOccurred
    {
        add { }
        remove { }
    }

    event EventHandler? IPresentationAdapter.PresentationsChanged
    {
        add { }
        remove { }
    }

    public int CurrentShowPosition => 0;
    public int SlideCount => 0;
    public string CurrentNotes => string.Empty;
    public IReadOnlyList<PresentationInfo> Presentations => [];
    public string? SelectedPresentationId => null;

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

    public void StartPresentation(bool fromCurrentSlide)
    {
    }

    public bool SelectPresentation(string presentationId) => false;

    public void Dispose()
    {
    }
}
