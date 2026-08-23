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

    public IReadOnlyList<OpenDesignProject> Scan(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        var projects = new List<OpenDesignProject>();
        try
        {
            foreach (var artifactPath in Directory.EnumerateFiles(
                rootDirectory,
                "*.html.artifact.json",
                SearchOption.AllDirectories))
            {
                var project = TryReadProject(artifactPath);
                if (project is not null)
                {
                    projects.Add(project);
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
            var displayName = GetFirstString(root, DisplayNameKeys)
                ?? Path.GetFileNameWithoutExtension(htmlPath);

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
