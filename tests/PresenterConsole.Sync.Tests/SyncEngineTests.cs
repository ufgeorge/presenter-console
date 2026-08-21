using System.Text.Json;
using PresenterConsole.Contracts;
using PresenterConsole.Sync;
using Xunit;

namespace PresenterConsole.Sync.Tests;

public sealed class SyncEngineTests
{
    [Fact]
    public void DuplicateIdIgnored()
    {
        using var engine = new SyncEngine();
        var accepted = 0;
        engine.CommandAccepted += (_, _) => accepted++;
        var command = new AgentCommand("x", 1, CommandType.Next);

        Assert.True(engine.TryAccept(command));
        Assert.False(engine.TryAccept(command));
        Assert.Equal(1, accepted);
    }

    [Fact]
    public void OlderSequenceIgnored()
    {
        using var engine = new SyncEngine();

        Assert.True(engine.TryAccept(new AgentCommand("a", 2, CommandType.Next)));
        Assert.False(engine.TryAccept(new AgentCommand("b", 1, CommandType.Next)));
    }

    [Fact]
    public void MobilePayloadRoundTripsWithCamelCase()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        var message = new WireMessage(
            MessageType.Command,
            new AgentCommand("abc123", 1, CommandType.Previous));

        var json = JsonSerializer.Serialize(message, options);
        var roundTrip = JsonSerializer.Deserialize<WireMessage>(json, options);

        Assert.Equal(CommandType.Previous, roundTrip?.Command?.Type);
        Assert.Contains("commandId", json);
    }
}