using System.Net;
using PresenterConsole.Desktop;
using Xunit;

namespace PresenterConsole.Sync.Tests;

public sealed class AudienceQuestionStoreTests
{
    private static readonly IPAddress Address = IPAddress.Parse("192.168.1.20");
    private static readonly DateTimeOffset Start =
        new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddsTrimmedQuestionAndStoresUtcTimestamp()
    {
        var store = new AudienceQuestionStore();

        var added = store.TryAdd(
            "  Can you repeat that?  ", Address, Start,
            out var question, out var error, out var rateLimited);

        Assert.True(added);
        Assert.Empty(error);
        Assert.False(rateLimited);
        Assert.NotNull(question);
        Assert.Equal("Can you repeat that?", question.Text);
        Assert.Equal(Start.UtcDateTime, question.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsEmptyQuestion(string? text)
    {
        var store = new AudienceQuestionStore();

        var added = store.TryAdd(
            text, Address, Start, out _, out var error, out var rateLimited);

        Assert.False(added);
        Assert.Contains("空白", error);
        Assert.False(rateLimited);
    }

    [Fact]
    public void RejectsQuestionLongerThanTwoHundredCharacters()
    {
        var store = new AudienceQuestionStore();

        var added = store.TryAdd(
            new string('x', 201), Address, Start,
            out _, out var error, out var rateLimited);

        Assert.False(added);
        Assert.Contains("200", error);
        Assert.False(rateLimited);
    }

    [Fact]
    public void RateLimitsSameAddressForTenSeconds()
    {
        var store = new AudienceQuestionStore();
        store.TryAdd("First", Address, Start, out _, out _, out _);

        var added = store.TryAdd(
            "Second", Address, Start.AddSeconds(9),
            out _, out var error, out var rateLimited);

        Assert.False(added);
        Assert.Equal("請稍候 10 秒再提問", error);
        Assert.True(rateLimited);
    }

    [Fact]
    public void RemovesQuestionById()
    {
        var store = new AudienceQuestionStore();
        store.TryAdd("To remove", Address, Start, out var question, out _, out _);

        Assert.True(store.Remove(question!.Id));
        Assert.Empty(store.Questions);
        Assert.False(store.Remove(question.Id));
    }
}
