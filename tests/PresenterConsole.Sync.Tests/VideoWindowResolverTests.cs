using PresenterConsole.Desktop;
using Xunit;

namespace PresenterConsole.Sync.Tests;

public sealed class VideoWindowResolverTests
{
    private static readonly Func<IntPtr, bool> ValidWindow = handle => handle == new IntPtr(2)
        || handle == new IntPtr(3)
        || handle == new IntPtr(4);

    [Fact]
    public void PrefersLaunchedProcessWindow()
    {
        var result = Resolve(
            new VideoProcessSnapshot("vlc", new IntPtr(2), false),
            "vlc",
            new IntPtr(3),
            [new VideoProcessSnapshot("vlc", new IntPtr(4), false)]);

        Assert.Equal(new IntPtr(2), result!.Handle);
        Assert.Equal("launched-process", result.Source);
    }

    [Fact]
    public void UsesTrackedWindowWhenLaunchedProcessHasNoWindow()
    {
        var result = Resolve(
            new VideoProcessSnapshot("vlc", IntPtr.Zero, false),
            "vlc",
            new IntPtr(3),
            [new VideoProcessSnapshot("vlc", new IntPtr(4), false)]);

        Assert.Equal(new IntPtr(3), result!.Handle);
        Assert.Equal("tracked-window", result.Source);
    }

    [Fact]
    public void UsesExistingSameProcessWindowAfterHandoff()
    {
        var result = Resolve(
            new VideoProcessSnapshot("vlc", IntPtr.Zero, true),
            "vlc",
            IntPtr.Zero,
            [new VideoProcessSnapshot("vlc", new IntPtr(4), false)]);

        Assert.Equal(new IntPtr(4), result!.Handle);
        Assert.Equal("same-process-existing-window", result.Source);
    }

    [Fact]
    public void IgnoresStaleTrackedWindow()
    {
        var result = Resolve(
            new VideoProcessSnapshot("vlc", IntPtr.Zero, false),
            "vlc",
            new IntPtr(99),
            [new VideoProcessSnapshot("vlc", new IntPtr(4), false)]);

        Assert.Equal(new IntPtr(4), result!.Handle);
    }

    [Fact]
    public void ReturnsFailureWhenAllCandidatesAreInvalid()
    {
        var result = Resolve(
            new VideoProcessSnapshot("vlc", IntPtr.Zero, true),
            "vlc",
            new IntPtr(99),
            [new VideoProcessSnapshot("vlc", new IntPtr(98), false)]);

        Assert.Null(result);
    }

    [Fact]
    public void UsesTargetProcessNameWhenLaunchedSnapshotIsUnavailable()
    {
        var result = Resolve(
            null,
            "vlc",
            IntPtr.Zero,
            [new VideoProcessSnapshot("vlc", new IntPtr(4), false)]);

        Assert.Equal(new IntPtr(4), result!.Handle);
        Assert.Equal("same-process-existing-window", result.Source);
    }

    private static VideoWindowResolution? Resolve(
        VideoProcessSnapshot? launched,
        string targetProcessName,
        IntPtr tracked,
        IEnumerable<VideoProcessSnapshot> existing)
    {
        return VideoWindowResolver.Resolve(
            launched,
            targetProcessName,
            tracked,
            ValidWindow,
            existing,
            _ => { });
    }
}
