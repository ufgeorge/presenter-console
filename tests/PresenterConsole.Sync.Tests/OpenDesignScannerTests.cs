using PresenterConsole.Desktop;
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
        var projects = new OpenDesignProjectScanner(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"))
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
                File.Copy(Path.Combine(FixtureDirectory, fileName), Path.Combine(projectDirectory, fileName));
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
        var projects = new OpenDesignProjectScanner(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"))
            .Scan(FixtureDirectory);

        Assert.Equal("sample-deck", Assert.Single(projects).DisplayName);
    }

    [Fact]
    public void ScannerFallsBackAndReportsDatabaseQueryFailure()
    {
        var appData = Path.Combine(Path.GetTempPath(), $"opendesign-appdata-{Guid.NewGuid():N}");
        var databaseDirectory = Path.Combine(appData, "Open Design", "namespaces", "namespace-1", "data");
        Directory.CreateDirectory(databaseDirectory);
        File.WriteAllText(Path.Combine(databaseDirectory, "app.sqlite"), "not a sqlite database");
        var diagnostics = new List<string>();

        try
        {
            var projects = new OpenDesignProjectScanner(appData, diagnostics.Add).Scan(FixtureDirectory);

            Assert.Equal("sample-deck", Assert.Single(projects).DisplayName);
            Assert.Contains(diagnostics, message => message.Contains("query failed", StringComparison.OrdinalIgnoreCase));
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
        var databaseDirectory = Path.Combine(appData, "Open Design", "namespaces", "namespace-1", "data");
        Directory.CreateDirectory(databaseDirectory);
        var databasePath = Path.Combine(databaseDirectory, "app.sqlite");
        using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE projects (id TEXT PRIMARY KEY, name TEXT NOT NULL); INSERT INTO projects (id, name) VALUES ($id, $name);";
            command.Parameters.AddWithValue("$id", projectId);
            command.Parameters.AddWithValue("$name", projectName);
            command.ExecuteNonQuery();
        }

        return appData;
    }
}
