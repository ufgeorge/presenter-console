using System.Collections.Concurrent;
using PresenterConsole.Contracts;

namespace PresenterConsole.Sync;

public sealed class SyncEngine : IDisposable
{
    private const int MaxRememberedCommands = 1000;
    private readonly ConcurrentDictionary<string, byte> processed = new();
    private readonly Timer heartbeatTimer;
    private long lastSequence;

    public event EventHandler<AgentCommand>? CommandAccepted;
    public event EventHandler? Heartbeat;

    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(1.5);
    public long LastSequence => Interlocked.Read(ref lastSequence);

    public SyncEngine()
    {
        heartbeatTimer = new Timer(
            _ => Heartbeat?.Invoke(this, EventArgs.Empty),
            null,
            HeartbeatInterval,
            HeartbeatInterval);
    }

    public bool TryAccept(AgentCommand command)
    {
        if (command.Sequence <= Interlocked.Read(ref lastSequence))
        {
            return false;
        }

        if (!processed.TryAdd(command.CommandId, 0))
        {
            return false;
        }

        while (processed.Count > MaxRememberedCommands && processed.TryRemove(processed.Keys.First(), out _))
        {
        }

        Interlocked.Exchange(ref lastSequence, command.Sequence);
        CommandAccepted?.Invoke(this, command);
        return true;
    }

    public void ResetForSync() => processed.Clear();

    public void Dispose() => heartbeatTimer.Dispose();
}