namespace PresenterConsole.Contracts;

public enum CommandType
{
    Next,
    Previous,
    GotoSlide,
    SyncRequest,
    ActivatePowerPoint,
    Ping,
    StartPresentation,
    StartPresentationFromCurrent,
    SelectPresentation,
    ActivateAgentWindow,
    DeleteQuestion,
    PlayVideo,
    PauseResumeVideo
}

public enum MessageType
{
    Hello,
    Command,
    State,
    Heartbeat,
    SyncRequest,
    Error,
    Pong,
    Questions
}

public sealed record AgentCommand(
    string CommandId,
    long Sequence,
    CommandType Type,
    int? Slide = null,
    string? PresentationId = null,
    string? QuestionId = null,
    string? VideoId = null);

public sealed record PresentationInfo(string Id, string Name, string FullName);
public sealed record VideoInfo(string Id, string Name, bool Playing);

public sealed record PresentationState(
    int CurrentShowPosition,
    int SlideCount,
    string Notes,
    bool Connected,
    long Sequence,
    IReadOnlyList<PresentationInfo>? Presentations = null,
    string? SelectedPresentationId = null,
    IReadOnlyList<VideoInfo>? Videos = null);

public sealed record AudienceQuestion(string Id, string Text, DateTime CreatedAt);

public sealed record WireMessage(
    MessageType Type,
    AgentCommand? Command = null,
    PresentationState? State = null,
    string? PairingToken = null,
    string? Error = null,
    IReadOnlyList<AudienceQuestion>? Questions = null);
