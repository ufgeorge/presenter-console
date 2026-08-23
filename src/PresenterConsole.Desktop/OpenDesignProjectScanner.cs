using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace PresenterConsole.Desktop;

public sealed record OpenDesignProject(
    string DisplayName,
    string HtmlPath,
    string SpeakerPrivatePath,
    string PreviewPath,
    int PageCount,
    string ArtifactPath);

public sealed class OpenDesignProjectScanner
{
    private static readonly string[] DisplayNameKeys =
    [
        "displayName",
        "name",
        "title"
    ];

    private static readonly string[] HtmlPathKeys =
    [
        "htmlPath",
        "html",
        "path"
    ];

    private static readonly string[] SpeakerPrivatePathKeys =
    [
        "speakerPrivatePath",
        "speakerPrivate",
        "speakerNotesPath"
    ];

    private static readonly string[] PreviewPathKeys =
    [
        "previewPath",
        "preview",
        "thumbnailPath"
    ];

    private readonly string applicationDataDirectory;
    private readonly Action<string> diagnosticLogger;

    public OpenDesignProjectScanner(
        string? applicationDataDirectory = null,
        Action<string>? diagnosticLogger = null)
    {
        this.applicationDataDirectory = applicationDataDirectory
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        this.diagnosticLogger = diagnosticLogger ?? LogDiagnostic;
    }

    public IReadOnlyList<OpenDesignProject> Scan(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        var projectNames = ReadProjectNames();
        var projects = new List<OpenDesignProject>();
        try
        {
            foreach (var artifactPath in Directory.EnumerateFiles(
                rootDirectory,
                "*.html.artifact.json",
                SearchOption.AllDirectories))
            {
                if (IsCompanionArtifact(artifactPath))
                {
                    continue;
                }

                var project = TryReadProject(artifactPath);
                if (project is not null)
                {
                    projects.Add(ApplyDatabaseDisplayName(project, projectNames));
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return projects
            .OrderBy(project => project.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private Dictionary<string, string> ReadProjectNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var namespacesDirectory = Path.Combine(applicationDataDirectory, "Open Design", "namespaces");
        if (!Directory.Exists(namespacesDirectory))
        {
            diagnosticLogger($"OpenDesign project database directory not found: {namespacesDirectory}");
            return names;
        }

        string[] databasePaths;
        try
        {
            databasePaths = Directory.EnumerateDirectories(namespacesDirectory)
                .Select(namespaceDirectory => Path.Combine(namespaceDirectory, "data", "app.sqlite"))
                .Where(File.Exists)
                .ToArray();
        }
        catch (IOException exception)
        {
            diagnosticLogger($"OpenDesign project database discovery failed: {exception.Message}");
            return names;
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnosticLogger($"OpenDesign project database discovery denied: {exception.Message}");
            return names;
        }

        if (databasePaths.Length == 0)
        {
            diagnosticLogger($"OpenDesign project database file not found under: {namespacesDirectory}");
            return names;
        }

        foreach (var databasePath in databasePaths)
        {
            ReadProjectNames(databasePath, names);
        }
        return names;
    }

    private void ReadProjectNames(string databasePath, IDictionary<string, string> names)
    {
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            }.ToString();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, name FROM projects";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                {
                    var id = reader.GetString(0);
                    var name = reader.GetString(1);
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                    {
                        names[id] = name;
                    }
                }
            }
        }
        catch (SqliteException exception)
        {
            diagnosticLogger($"OpenDesign project database query failed ({databasePath}): {exception.Message}");
        }
        catch (IOException exception)
        {
            diagnosticLogger($"OpenDesign project database read failed ({databasePath}): {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnosticLogger($"OpenDesign project database access denied ({databasePath}): {exception.Message}");
        }
    }

    private static OpenDesignProject ApplyDatabaseDisplayName(
        OpenDesignProject project,
        IReadOnlyDictionary<string, string> projectNames)
    {
        var projectId = Path.GetFileName(Path.GetDirectoryName(project.HtmlPath));
        return projectId is not null
            && projectNames.TryGetValue(projectId, out var projectName)
            ? project with { DisplayName = CleanDisplayName(projectName) }
            : project;
    }

    private static void LogDiagnostic(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "presenter-console.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] OpenDesign scanner diagnostic: {message}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static OpenDesignProject? TryReadProject(string artifactPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(artifactPath));
            var root = document.RootElement;
            if (!string.Equals(GetString(root, "kind"), "deck", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var artifactDirectory = Path.GetDirectoryName(artifactPath) ?? string.Empty;
            var htmlPath = ResolvePath(
                artifactDirectory,
                GetFirstString(root, HtmlPathKeys))
                ?? DeriveHtmlPath(artifactPath);
            if (htmlPath is null || !File.Exists(htmlPath))
            {
                return null;
            }

            var speakerPrivatePath = ResolvePath(
                artifactDirectory,
                GetFirstString(root, SpeakerPrivatePathKeys))
                ?? FindCompanion(
                    artifactDirectory,
                    Path.GetFileNameWithoutExtension(htmlPath),
                    "-speaker-private.html");
            var previewPath = ResolvePath(
                artifactDirectory,
                GetFirstString(root, PreviewPathKeys))
                ?? FindCompanion(
                    artifactDirectory,
                    Path.GetFileNameWithoutExtension(htmlPath),
                    "-preview.png");

            var pageCount = speakerPrivatePath is not null
                ? OpenDesignHtmlParser.CountSlides(speakerPrivatePath)
                : OpenDesignHtmlParser.CountSlides(htmlPath);
            var displayName = CleanDisplayName(
                GetFirstString(root, DisplayNameKeys)
                ?? Path.GetFileNameWithoutExtension(htmlPath));

            return new OpenDesignProject(
                displayName,
                htmlPath,
                speakerPrivatePath ?? string.Empty,
                previewPath ?? string.Empty,
                pageCount,
                artifactPath);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? DeriveHtmlPath(string artifactPath)
    {
        var path = artifactPath[..^".artifact.json".Length];
        return File.Exists(path) ? path : null;
    }

    private static bool IsCompanionArtifact(string artifactPath)
    {
        const string artifactSuffix = ".artifact.json";
        var artifactName = Path.GetFileName(artifactPath);
        if (!artifactName.EndsWith(artifactSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var htmlName = artifactName[..^artifactSuffix.Length];
        var stem = Path.GetFileNameWithoutExtension(htmlName);
        return stem.EndsWith("-public", StringComparison.OrdinalIgnoreCase)
            || stem.EndsWith("-speaker-private", StringComparison.OrdinalIgnoreCase)
            || stem.EndsWith("-private", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanDisplayName(string displayName)
    {
        return Path.GetFileNameWithoutExtension(displayName);
    }

    private static string? FindCompanion(
        string directory,
        string htmlFileNameWithoutExtension,
        string suffix)
    {
        var candidate = Path.Combine(directory, htmlFileNameWithoutExtension + suffix);
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? ResolvePath(string directory, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var resolved = Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(directory, path));
        return File.Exists(resolved) ? resolved : null;
    }

    private static string? GetFirstString(JsonElement root, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            var value = GetString(root, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, string key)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }

                var nested = GetString(property.Value, key);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var nested = GetString(child, key);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }
}
