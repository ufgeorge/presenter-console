using PresenterConsole.Desktop;
using System.Net;
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

    [Fact]
    public void ScannerUsesDaemonProjectNameWhenHtmlParentMatchesProjectId()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opendesign-scanner-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(root, "project-123");
        Directory.CreateDirectory(projectDirectory);

        try
        {
            foreach (var fileName in new[]
                     {
                         "sample-deck.html",
                         "sample-deck-speaker-private.html",
                         "sample-deck.html.artifact.json"
                     })
            {
                File.Copy(Path.Combine(FixtureDirectory, fileName), Path.Combine(projectDirectory, fileName));
            }

            using var client = new HttpClient(new StubHttpMessageHandler(
                "{\"projects\":[{\"id\":\"project-123\",\"name\":\"AI-agent-ppt-1 現場版\"}] }"));
            var project = Assert.Single(new OpenDesignProjectScanner(client).Scan(root));

            Assert.Equal("AI-agent-ppt-1 現場版", project.DisplayName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ScannerFallsBackWhenDaemonDoesNotRespondWithProjects()
    {
        using var client = new HttpClient(new StubHttpMessageHandler("{\"projects\":[]}"));

        var projects = new OpenDesignProjectScanner(client).Scan(FixtureDirectory);

        Assert.Equal("sample-deck", Assert.Single(projects).DisplayName);
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });
        }
    }
}
