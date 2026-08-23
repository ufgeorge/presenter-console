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
    SelectPresentation
}

public enum MessageType
{
    Hello,
    Command,
    State,
    Heartbeat,
    SyncRequest,
    Error,
    Pong
}

public sealed record AgentCommand(
    string CommandId,
    long Sequence,
    CommandType Type,
    int? Slide = null,
    string? PresentationId = null);

public sealed record PresentationInfo(string Id, string Name, string FullName);

public sealed record PresentationState(
    int CurrentShowPosition,
    int SlideCount,
    string Notes,
    bool Connected,
    long Sequence,
    IReadOnlyList<PresentationInfo>? Presentations = null,
    string? SelectedPresentationId = null);

public sealed record WireMessage(
    MessageType Type,
    AgentCommand? Command = null,
    PresentationState? State = null,
    string? PairingToken = null,
    string? Error = null);
