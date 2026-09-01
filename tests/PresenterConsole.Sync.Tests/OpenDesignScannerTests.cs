using PresenterConsole.Desktop;
using PresenterConsole.Contracts;
using Microsoft.Data.Sqlite;
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
        var missingAppData = Path.Combine(
            Path.GetTempPath(),
            $"missing-{Guid.NewGuid():N}");
        var projects = new OpenDesignProjectScanner(missingAppData)
            .Scan(FixtureDirectory);

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
    public void ParserPrefersSpeakerNotesJsonAndCountsJsonPages()
    {
        const string html = """
            <script id="speaker-notes" type="application/json">
            ["[開錄影]\n第一頁", "第二頁", "第三頁"]
            </script>
            <section class="slide"><aside class="speaker-notes">
            逐字稿：第一頁
            </aside></section>
            """;

        Assert.Equal(3, OpenDesignHtmlParser.CountSlidesFromHtml(html));
        Assert.Equal("[開錄影]\n第一頁", OpenDesignHtmlParser.ReadNotesFromHtml(html, 1));
        Assert.Equal("第三頁", OpenDesignHtmlParser.ReadNotesFromHtml(html, 3));
        Assert.Equal(string.Empty, OpenDesignHtmlParser.ReadNotesFromHtml(html, 4));
    }

    [Fact]
    public void ParserFallsBackToAsideWhenSpeakerNotesJsonIsMissing()
    {
        const string html = """
            <section class="slide">
            <aside class="speaker-notes">舊格式<br>講稿</aside>
            </section>
            """;

        Assert.Equal(1, OpenDesignHtmlParser.CountSlidesFromHtml(html));
        Assert.Equal(
            "舊格式\n講稿",
            OpenDesignHtmlParser.ReadNotesFromHtml(html, 1));
    }

    [Fact]
    public void ParserFallsBackToAsideWhenSpeakerNotesJsonIsInvalid()
    {
        const string html = """
            <script id="speaker-notes" type="application/json">not json</script>
            <section class="slide">
            <aside class="speaker-notes">損壞 JSON 的 fallback</aside>
            </section>
            """;

        var exception = Record.Exception(
            () => OpenDesignHtmlParser.ReadNotesFromHtml(html, 1));

        Assert.Null(exception);
        Assert.Equal(
            "損壞 JSON 的 fallback",
            OpenDesignHtmlParser.ReadNotesFromHtml(html, 1));
        Assert.Equal(1, OpenDesignHtmlParser.CountSlidesFromHtml(html));
    }

    [Fact]
    public void ReadVideosFindsSingleVideo()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\"><video src=\"demo.mp4\"></video></section>");

        var videos = OpenDesignHtmlParser.ReadVideos(fixture.HtmlPath, 1);

        Assert.Equal(
            new VideoInfo(fixture.VideoPath, "demo.mp4", false),
            Assert.Single(videos));
    }

    [Fact]
    public void ExtractVideosReadsTextAndDeduplicatesPaths()
    {
        using var fixture = new VideoFixture(
            "<video>demo.mp4</video> <video src=\"demo.mp4\">ignored</video>");

        var videos = OpenDesignHtmlParser.ExtractVideos(
            File.ReadAllText(fixture.HtmlPath),
            Path.GetDirectoryName(fixture.HtmlPath)!);

        var video = Assert.Single(videos);
        Assert.Equal(fixture.VideoPath, video.Id);
        Assert.False(video.Playing);
    }

    [Fact]
    public void ReadVideosPreservesMultipleVideoOrder()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\"><video src=\"first.mp4\"></video>"
            + "<video src=\"second.mp4\"></video></section>",
            "first.mp4",
            "second.mp4");

        var videos = OpenDesignHtmlParser.ReadVideos(fixture.HtmlPath, 1);

        Assert.Equal(["first.mp4", "second.mp4"], videos.Select(video => video.Name));
    }

    [Fact]
    public void ReadVideosReturnsEmptyWhenSlideHasNoVideo()
    {
        using var fixture = new VideoFixture("<section class=\"slide\">Notes</section>");

        Assert.Empty(OpenDesignHtmlParser.ReadVideos(fixture.HtmlPath, 1));
    }

    [Fact]
    public void ReadVideosResolvesRelativePathToAbsolutePath()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\"><video src=\"media/demo.mp4\"></video></section>",
            "media/demo.mp4");

        var video = Assert.Single(OpenDesignHtmlParser.ReadVideos(fixture.HtmlPath, 1));

        var expectedPath = Path.Combine(
            Path.GetDirectoryName(fixture.HtmlPath)!,
            "media",
            "demo.mp4");
        Assert.Equal(Path.GetFullPath(expectedPath), video.Id);
    }

    [Fact]
    public void ReadVideosFiltersMissingFiles()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\"><video src=\"missing.mp4\"></video></section>");

        Assert.Empty(OpenDesignHtmlParser.ReadVideos(fixture.HtmlPath, 1));
    }

    [Fact]
    public void ReadVideosFindsVideoFileNameInTagContent()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\"><video>demo.mp4</video></section>");

        var video = Assert.Single(OpenDesignHtmlParser.ReadVideos(fixture.HtmlPath, 1));

        Assert.Equal(new VideoInfo(fixture.VideoPath, "demo.mp4", false), video);
    }

    [Fact]
    public void ReadVideosTrimsVideoFileNameInTagContent()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\"><video>  demo.mp4  </video></section>");

        var video = Assert.Single(OpenDesignHtmlParser.ReadVideos(fixture.HtmlPath, 1));

        Assert.Equal("demo.mp4", video.Name);
    }

    [Fact]
    public void ReadVideosStopsUnclosedVideoContentAtLineBreak()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\"><video>demo.mp4\n後續講稿</section>");

        var video = Assert.Single(OpenDesignHtmlParser.ReadVideos(fixture.HtmlPath, 1));

        Assert.Equal("demo.mp4", video.Name);
    }

    [Fact]
    public void ReadVideosStopsClosedVideoContentBeforeFollowingText()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\"><video>demo.mp4</video>後續文字</section>");

        var video = Assert.Single(OpenDesignHtmlParser.ReadVideos(fixture.HtmlPath, 1));

        Assert.Equal("demo.mp4", video.Name);
    }

    [Fact]
    public void ReadVideosFindsVideoFileNameInSpeakerNotesJson()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\">頁面內容</section>");
        fixture.WriteNotesHtml(
            "<script id=\"speaker-notes\" type=\"application/json\">"
            + "[\"[開錄影]...<video>demo.mp4</video>...\"]</script>");

        var videos = OpenDesignHtmlParser.ReadVideos(
            fixture.HtmlPath,
            1,
            fixture.NotesHtmlPath);

        Assert.Equal("demo.mp4", Assert.Single(videos).Name);
    }

    [Fact]
    public void ReadVideosFindsUnclosedVideoTagContent()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\"><video>demo.mp4</section>");

        var video = Assert.Single(OpenDesignHtmlParser.ReadVideos(fixture.HtmlPath, 1));

        Assert.Equal("demo.mp4", video.Name);
    }

    [Fact]
    public void ReadVideosDeduplicatesVideoFoundInSectionAndNotes()
    {
        using var fixture = new VideoFixture(
            "<section class=\"slide\"><video src=\"demo.mp4\"></video></section>");
        fixture.WriteNotesHtml(
            "<script id=\"speaker-notes\" type=\"application/json\">"
            + "[\"<video>demo.mp4</video>\"]</script>");

        var videos = OpenDesignHtmlParser.ReadVideos(
            fixture.HtmlPath,
            1,
            fixture.NotesHtmlPath);

        Assert.Single(videos);
    }

    [Fact]
    public void ScannerUsesDatabaseProjectNameWhenHtmlParentMatchesProjectId()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opendesign-scanner-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(root, "project-123");
        Directory.CreateDirectory(projectDirectory);
        var appData = string.Empty;

        try
        {
            foreach (var fileName in new[]
                     {
                         "sample-deck.html",
                         "sample-deck-speaker-private.html",
                         "sample-deck.html.artifact.json"
                     })
            {
                var sourcePath = Path.Combine(FixtureDirectory, fileName);
                var destinationPath = Path.Combine(projectDirectory, fileName);
                File.Copy(sourcePath, destinationPath);
            }

            appData = CreateProjectDatabase("project-123", "AI-agent-ppt-1 現場版");
            var project = Assert.Single(new OpenDesignProjectScanner(appData).Scan(root));

            Assert.Equal("AI-agent-ppt-1 現場版", project.DisplayName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(appData))
            {
                Directory.Delete(appData, recursive: true);
            }
        }
    }

    [Fact]
    public void ScannerFallsBackWhenDatabaseDoesNotExist()
    {
        var missingAppData = Path.Combine(
            Path.GetTempPath(),
            $"missing-{Guid.NewGuid():N}");
        var projects = new OpenDesignProjectScanner(missingAppData)
            .Scan(FixtureDirectory);

        Assert.Equal("sample-deck", Assert.Single(projects).DisplayName);
    }

    [Fact]
    public void ScannerFallsBackAndReportsDatabaseQueryFailure()
    {
        var appData = Path.Combine(Path.GetTempPath(), $"opendesign-appdata-{Guid.NewGuid():N}");
        var databaseDirectory = Path.Combine(
            appData,
            "Open Design",
            "namespaces",
            "namespace-1",
            "data");
        Directory.CreateDirectory(databaseDirectory);
        File.WriteAllText(Path.Combine(databaseDirectory, "app.sqlite"), "not a sqlite database");
        var diagnostics = new List<string>();

        try
        {
            var scanner = new OpenDesignProjectScanner(appData, diagnostics.Add);
            var projects = scanner.Scan(FixtureDirectory);

            Assert.Equal("sample-deck", Assert.Single(projects).DisplayName);
            Assert.Contains(
                diagnostics,
                message => message.Contains(
                    "query failed",
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(appData, recursive: true);
        }
    }

    private static string CreateProjectDatabase(string projectId, string projectName)
    {
        var appData = Path.Combine(Path.GetTempPath(), $"opendesign-appdata-{Guid.NewGuid():N}");
        var databaseDirectory = Path.Combine(
            appData,
            "Open Design",
            "namespaces",
            "namespace-1",
            "data");
        Directory.CreateDirectory(databaseDirectory);
        var databasePath = Path.Combine(databaseDirectory, "app.sqlite");
        using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE projects ("
                + "id TEXT PRIMARY KEY, name TEXT NOT NULL);"
                + " INSERT INTO projects (id, name) VALUES ($id, $name);";
            command.Parameters.AddWithValue("$id", projectId);
            command.Parameters.AddWithValue("$name", projectName);
            command.ExecuteNonQuery();
        }

        return appData;
    }

    private sealed class VideoFixture : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(), $"read-videos-{Guid.NewGuid():N}");

        public string HtmlPath => Path.Combine(root, "deck.html");
        public string NotesHtmlPath => Path.Combine(root, "deck-speaker-private.html");
        public string VideoPath => Path.Combine(root, "demo.mp4");

        public VideoFixture(string html, params string[] videoPaths)
        {
            Directory.CreateDirectory(root);
            foreach (var videoPath in videoPaths.Length == 0
                         ? ["demo.mp4"]
                         : videoPaths)
            {
                var path = Path.Combine(root, videoPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, []);
            }

            File.WriteAllText(HtmlPath, html);
        }

        public void WriteNotesHtml(string html)
        {
            File.WriteAllText(NotesHtmlPath, html);
        }

        public void Dispose()
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
