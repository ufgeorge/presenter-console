using PresenterConsole.Desktop;
using Xunit;

namespace PresenterConsole.Sync.Tests;

public sealed class OpenDesignScannerTests
{
    private static string FixtureDirectory => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures");

    [Fact]
    public void ScannerFindsDeckAndIgnoresOtherArtifactKinds()
    {
        var projects = new OpenDesignProjectScanner().Scan(FixtureDirectory);

        var project = Assert.Single(projects);
        Assert.Equal("sample-deck", project.DisplayName);
        Assert.Equal(2, project.PageCount);
        Assert.True(File.Exists(project.HtmlPath));
        Assert.True(File.Exists(project.SpeakerPrivatePath));
        Assert.Equal(string.Empty, project.PreviewPath);
    }

    [Fact]
    public void ParserCountsSlidesAndConvertsSpeakerNotes()
    {
        var path = Path.Combine(FixtureDirectory, "sample-deck-speaker-private.html");

        Assert.Equal(2, OpenDesignHtmlParser.CountSlides(path));
        Assert.Equal(
            "開場\n介紹 & 目標。",
            OpenDesignHtmlParser.ReadNotes(path, 1));
        Assert.Equal("第二頁\n確認結果", OpenDesignHtmlParser.ReadNotes(path, 2));
        Assert.Equal(string.Empty, OpenDesignHtmlParser.ReadNotes(path, 3));
    }
}
