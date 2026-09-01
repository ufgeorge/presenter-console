using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using PresenterConsole.Contracts;
using PresenterConsole.Sync;

namespace PresenterConsole.Desktop;

public sealed class AgentServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private IPresentationAdapter presentation;
    private readonly SyncEngine sync;
    private readonly SynchronizationContext uiContext;
    private readonly List<WebSocket> sockets = [];
    private readonly AudienceQuestionStore questionStore = new();
    private readonly string pairingToken = Convert.ToHexString(
        RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private readonly DateTimeOffset tokenExpiresAt = DateTimeOffset.UtcNow.AddHours(2);
    private WebApplication? application;

    public AgentServer(IPresentationAdapter presentation, SyncEngine sync)
    {
        this.presentation = presentation;
        this.sync = sync;
        uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        sync.CommandAccepted += OnCommandAccepted;
        SubscribeToPresentation(presentation);
    }

    public void ReplacePresentation(IPresentationAdapter replacement)
    {
        if (ReferenceEquals(presentation, replacement))
        {
            return;
        }

        UnsubscribeFromPresentation(presentation);
        presentation = replacement;
        SubscribeToPresentation(presentation);
        BroadcastState();
    }

    public string PairingUrl => $"http://{GetLanAddress()}:5217/?token={pairingToken}";

    public string AskUrl => $"http://{GetLanAddress()}:5217/ask";

    public IReadOnlyList<AudienceQuestion> Questions
    {
        get
        {
            return questionStore.Questions;
        }
    }

    public event EventHandler? QuestionsChanged;
    public event EventHandler? AgentWindowRequested;
    public event EventHandler? AgentWindowClosedRequested;

    public void DeleteQuestionFromAgent(string questionId) => DeleteQuestion(questionId);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://0.0.0.0:5217");
        application = builder.Build();
        application.UseDefaultFiles();
        application.UseStaticFiles();
        application.UseWebSockets();
        application.Map("/ws", HandleWebSocketAsync);
        application.MapGet("/ask", async context =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "wwwroot", "ask.html");
            await context.Response.SendFileAsync(path);
        });
        application.MapPost("/api/ask", HandleAskAsync);
        await application.StartAsync(cancellationToken);
    }

    private void OnCommandAccepted(object? sender, AgentCommand command)
    {
        uiContext.Post(_ =>
        {
            try
            {
                switch (command.Type)
                {
                    case CommandType.Next:
                        presentation.Next();
                        break;
                    case CommandType.Previous:
                        presentation.Previous();
                        break;
                    case CommandType.GotoSlide when command.Slide is int slide:
                        presentation.GotoSlide(slide);
                        break;
                    case CommandType.ActivatePowerPoint:
                        AgentWindowClosedRequested?.Invoke(this, EventArgs.Empty);
                        presentation.ActivateWindow();
                        break;
                    case CommandType.DeleteQuestion when command.QuestionId is { } questionId:
                        DeleteQuestion(questionId);
                        break;
                    case CommandType.ActivateAgentWindow:
                        AgentWindowRequested?.Invoke(this, EventArgs.Empty);
                        break;
                    case CommandType.StartPresentation:
                        presentation.StartPresentation(fromCurrentSlide: false);
                        break;
                    case CommandType.StartPresentationFromCurrent:
                        presentation.StartPresentation(fromCurrentSlide: true);
                        break;
                    case CommandType.SelectPresentation
                        when command.PresentationId is { } presentationId:
                        if (!presentation.SelectPresentation(presentationId))
                        {
                            BroadcastError("找不到選定的簡報，請重新整理清單");
                        }
                        break;
                    case CommandType.PlayVideo when command.VideoId is { } videoId:
                        presentation.PlayVideo(videoId);
                        break;
                    case CommandType.PauseResumeVideo:
                        presentation.PauseResumeVideo();
                        break;
                }
            }
            catch (Exception exception)
            {
                WriteDiagnosticLog(
                    $"命令處理失敗 type={command.Type} error={exception.Message}");
                BroadcastError($"操作失敗：{exception.Message}");
            }
        }, null);
    }

    private void SubscribeToPresentation(IPresentationAdapter adapter)
    {
        adapter.StateChanged += OnPresentationStateChanged;
        adapter.ErrorOccurred += OnPresentationError;
        adapter.PresentationsChanged += OnPresentationsChanged;
    }

    private void UnsubscribeFromPresentation(IPresentationAdapter adapter)
    {
        adapter.StateChanged -= OnPresentationStateChanged;
        adapter.ErrorOccurred -= OnPresentationError;
        adapter.PresentationsChanged -= OnPresentationsChanged;
    }

    private void OnPresentationStateChanged(object? sender, EventArgs e) => BroadcastState();

    private void OnPresentationError(object? sender, string error) => BroadcastError(error);

    private void OnPresentationsChanged(object? sender, EventArgs e) => BroadcastState();

    private async Task HandleWebSocketAsync(HttpContext context)
    {
        var token = context.Request.Query["token"].ToString();
        if (!IsValidPairingToken(token) || DateTimeOffset.UtcNow >= tokenExpiresAt)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        WriteDiagnosticLog("WS client connected");
        lock (sockets)
        {
            sockets.Add(socket);
        }

        await SendSafelyAsync(socket, new WireMessage(MessageType.State, State: State()));
        await SendSafelyAsync(socket, QuestionsMessage());
        var buffer = new byte[8192];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var message = JsonSerializer.Deserialize<WireMessage>(
                    buffer.AsSpan(0, result.Count),
                    JsonOptions);
                if (message?.Command is not { } command)
                {
                    continue;
                }

                WriteDiagnosticLog($"WS command: type={command.Type} seq={command.Sequence}");

                if (command.Type == CommandType.SyncRequest)
                {
                    await SendSafelyAsync(
                        socket,
                        new WireMessage(MessageType.State, State: State()));
                }
                else if (command.Type == CommandType.Ping)
                {
                    await SendSafelyAsync(
                        socket,
                        new WireMessage(MessageType.Pong, State: State()));
                }
                else
                {
                    if (!sync.TryAccept(command))
                    {
                        await SendSafelyAsync(
                            socket,
                            new WireMessage(
                                MessageType.Error,
                                Error: Localization.CommandRejected));
                    }
                }
            }
        }
        finally
        {
            WriteDiagnosticLog("WS client disconnected");
            RemoveSocket(socket);
        }
    }

    private async Task HandleAskAsync(HttpContext context)
    {
        var request = await context.Request.ReadFromJsonAsync<AskRequest>(JsonOptions);
        var text = request?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
        {
            await WriteJsonErrorAsync(context, StatusCodes.Status400BadRequest,
                "問題不可為空白，且最多 200 字");
            return;
        }

        var address = context.Connection.RemoteIpAddress ?? IPAddress.None;
        if (!questionStore.TryAdd(
                text, address, DateTimeOffset.UtcNow, out var question,
                out var error, out var rateLimited))
        {
            var statusCode = rateLimited
                ? StatusCodes.Status429TooManyRequests
                : StatusCodes.Status400BadRequest;
            await WriteJsonErrorAsync(context, statusCode, error);
            return;
        }

        QuestionsChanged?.Invoke(this, EventArgs.Empty);
        BroadcastQuestions();
        await context.Response.WriteAsJsonAsync(question, JsonOptions);
    }

    private static async Task WriteJsonErrorAsync(
        HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message }, JsonOptions);
    }

    private void DeleteQuestion(string questionId)
    {
        if (questionStore.Remove(questionId))
        {
            QuestionsChanged?.Invoke(this, EventArgs.Empty);
            BroadcastQuestions();
        }
    }

    private WireMessage QuestionsMessage() => new(
        MessageType.Questions, Questions: Questions);

    private void BroadcastQuestions()
    {
        WebSocket[] clients;
        lock (sockets)
        {
            clients = sockets.ToArray();
        }

        var message = QuestionsMessage();
        foreach (var client in clients)
        {
            _ = SendSafelyAsync(client, message);
        }
    }

    private PresentationState State() => new(
        presentation.CurrentShowPosition,
        presentation.SlideCount,
        presentation.CurrentNotes,
        true,
        sync.LastSequence,
        presentation.Presentations,
        presentation.SelectedPresentationId,
        presentation.Videos);

    private void BroadcastState()
    {
        WebSocket[] clients;
        lock (sockets)
        {
            clients = sockets.ToArray();
        }

        var message = new WireMessage(MessageType.State, State: State());
        foreach (var client in clients)
        {
            _ = SendSafelyAsync(client, message);
        }
    }

    private void BroadcastError(string error)
    {
        WebSocket[] clients;
        lock (sockets)
        {
            clients = sockets.ToArray();
        }

        var message = new WireMessage(MessageType.Error, Error: error);
        foreach (var client in clients)
        {
            _ = SendSafelyAsync(client, message);
        }
    }
    private bool IsValidPairingToken(string token)
    {
        if (token.Length != pairingToken.Length)
        {
            return false;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(token),
                Convert.FromHexString(pairingToken));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task SendSafelyAsync(WebSocket socket, WireMessage message)
    {
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(
                    JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
            RemoveSocket(socket);
        }
        catch (InvalidOperationException)
        {
            RemoveSocket(socket);
        }
    }

    private void RemoveSocket(WebSocket socket)
    {
        lock (sockets)
        {
            sockets.Remove(socket);
        }
    }

    private static void WriteDiagnosticLog(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "presenter-console.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string GetLanAddress()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(address.Address))
                {
                    return address.Address.ToString();
                }
            }
        }

        return "127.0.0.1";
    }

    private sealed record AskRequest(string? Text);

    public async ValueTask DisposeAsync()
    {
        UnsubscribeFromPresentation(presentation);
        sync.CommandAccepted -= OnCommandAccepted;
        if (application is not null)
        {
            await application.StopAsync();
        }
    }
}
