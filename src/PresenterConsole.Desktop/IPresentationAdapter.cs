namespace PresenterConsole.Desktop;

public interface IPresentationAdapter : IDisposable
{
    int CurrentShowPosition { get; }
    int SlideCount { get; }
    string CurrentNotes { get; }

    event EventHandler? StateChanged;

    void Next();
    void Previous();
    void GotoSlide(int slide);
    void ActivateWindow();
    void StartPresentation(bool fromCurrentSlide);
}