using System.Text.Json;

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
    private const string DaemonProjectsUri = "http://127.0.0.1:7456/api/projects";
    private static readonly HttpClient SharedDaemonClient = new();

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

    private readonly HttpClient daemonClient;

    public OpenDesignProjectScanner(HttpClient? daemonClient = null)
    {
        this.daemonClient = daemonClient ?? SharedDaemonClient;
    }

    public IReadOnlyList<OpenDesignProject> Scan(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        var daemonNames = ReadDaemonProjectNames();
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
                    projects.Add(ApplyDaemonDisplayName(project, daemonNames));
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

    private Dictionary<string, string> ReadDaemonProjectNames()
    {
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var response = daemonClient
                .GetAsync(DaemonProjectsUri, cancellation.Token)
                .GetAwaiter()
                .GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            using var document = JsonDocument.Parse(
                response.Content.ReadAsStringAsync(cancellation.Token).GetAwaiter().GetResult());
            return ParseDaemonProjectNames(document.RootElement);
        }
        catch (HttpRequestException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, string> ParseDaemonProjectNames(JsonElement root)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<JsonElement> entries = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().ToArray()
            : FindProjectArray(root);

        foreach (var entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = GetFirstString(entry, ["id", "projectId"]);
            var name = GetFirstString(entry, ["name"]);
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
            {
                names[id] = name;
            }
        }

        return names;
    }

    private static IEnumerable<JsonElement> FindProjectArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array
                    && (string.Equals(property.Name, "projects", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(property.Name, "data", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(property.Name, "results", StringComparison.OrdinalIgnoreCase)))
                {
                    return property.Value.EnumerateArray();
                }
            }
        }

        return [];
    }

    private static OpenDesignProject ApplyDaemonDisplayName(
        OpenDesignProject project,
        IReadOnlyDictionary<string, string> daemonNames)
    {
        var projectId = Path.GetFileName(Path.GetDirectoryName(project.HtmlPath));
        return projectId is not null
            && daemonNames.TryGetValue(projectId, out var daemonName)
            ? project with { DisplayName = CleanDisplayName(daemonName) }
            : project;
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
