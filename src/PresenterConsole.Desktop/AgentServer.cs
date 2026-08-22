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

    private readonly IPresentationAdapter presentation;
    private readonly SyncEngine sync;
    private readonly SynchronizationContext uiContext;
    private readonly List<WebSocket> sockets = [];
    private readonly string pairingToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private readonly DateTimeOffset tokenExpiresAt = DateTimeOffset.UtcNow.AddHours(2);
    private WebApplication? application;

    public AgentServer(IPresentationAdapter presentation, SyncEngine sync)
    {
        this.presentation = presentation;
        this.sync = sync;
        uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        sync.CommandAccepted += OnCommandAccepted;
        presentation.StateChanged += (_, _) => BroadcastState();
    }

    public string PairingUrl => $"http://{GetLanAddress()}:5217/?token={pairingToken}";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://0.0.0.0:5217");
        application = builder.Build();
        application.UseDefaultFiles();
        application.UseStaticFiles();
        application.UseWebSockets();
        application.Map("/ws", HandleWebSocketAsync);
        await application.StartAsync(cancellationToken);
    }

    private void OnCommandAccepted(object? sender, AgentCommand command)
    {
        uiContext.Post(_ =>
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
                    presentation.ActivateWindow();
                    break;
            }
        }, null);
    }

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
        lock (sockets)
        {
            sockets.Add(socket);
        }

        await SendSafelyAsync(socket, new WireMessage(MessageType.State, State: State()));
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

                if (command.Type == CommandType.SyncRequest)
                {
                    await SendSafelyAsync(socket, new WireMessage(MessageType.State, State: State()));
                }
                else if (command.Type == CommandType.Ping)
                {
                    await SendSafelyAsync(socket, new WireMessage(MessageType.Pong, State: State()));
                }
                else
                {
                    if (!sync.TryAccept(command))
                    {
                        await SendSafelyAsync(
                            socket,
                            new WireMessage(
                                MessageType.Error,
                                Error: "命令被拒絕（sequence 過舊或命令重複），請重整頁面後重試"));
                    }
                }
            }
        }
        finally
        {
            RemoveSocket(socket);
        }
    }

    private PresentationState State() => new(
        presentation.CurrentShowPosition,
        presentation.SlideCount,
        presentation.CurrentNotes,
        true,
        sync.LastSequence);

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

    public async ValueTask DisposeAsync()
    {
        if (application is not null)
        {
            await application.StopAsync();
        }
    }
}