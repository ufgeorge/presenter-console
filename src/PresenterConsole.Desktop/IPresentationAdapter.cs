using PresenterConsole.Contracts;

namespace PresenterConsole.Desktop;

public interface IPresentationAdapter : IDisposable
{
    int CurrentShowPosition { get; }
    int SlideCount { get; }
    string CurrentNotes { get; }
    IReadOnlyList<PresentationInfo> Presentations { get; }
    string? SelectedPresentationId { get; }
    IReadOnlyList<VideoInfo> Videos { get; }

    event EventHandler? StateChanged;
    event EventHandler<string>? ErrorOccurred;
    event EventHandler? PresentationsChanged;

    void Next();
    void Previous();
    void GotoSlide(int slide);
    void ActivateWindow();
    void StartPresentation(bool fromCurrentSlide);
    bool SelectPresentation(string presentationId);
    void PlayVideo(string videoId);
    void PauseResumeVideo();
}
