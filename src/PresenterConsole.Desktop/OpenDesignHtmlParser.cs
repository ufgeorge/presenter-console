using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using PresenterConsole.Contracts;

namespace PresenterConsole.Desktop;

public static partial class OpenDesignHtmlParser
{
    public static int CountSlides(string htmlPath)
    {
        if (!File.Exists(htmlPath))
        {
            return 0;
        }

        return CountSlidesFromHtml(File.ReadAllText(htmlPath));
    }

    public static int CountSlidesFromHtml(string html)
    {
        if (TryReadSpeakerNotesJson(html, out var notes))
        {
            return notes.GetArrayLength();
        }

        return SlideSectionRegex().Matches(html).Count;
    }

    public static string ReadNotes(string htmlPath, int slidePosition)
    {
        if (slidePosition < 1 || !File.Exists(htmlPath))
        {
            return string.Empty;
        }

        return ReadNotesFromHtml(File.ReadAllText(htmlPath), slidePosition);
    }

    public static string ReadNotesFromHtml(string html, int slidePosition)
    {
        if (TryReadSpeakerNotesJson(html, out var notes))
        {
            if (slidePosition > notes.GetArrayLength())
            {
                return string.Empty;
            }

            var note = notes[slidePosition - 1];
            return note.ValueKind == JsonValueKind.String
                ? CleanText(note.GetString() ?? string.Empty)
                : string.Empty;
        }

        var sections = SlideSectionRegex().Matches(html);
        if (slidePosition > sections.Count)
        {
            return string.Empty;
        }

        var section = sections[slidePosition - 1].Value;
        var notesMatch = SpeakerNotesRegex().Match(section);
        return notesMatch.Success ? CleanText(notesMatch.Groups[1].Value) : string.Empty;
    }

    public static IReadOnlyList<VideoInfo> ReadVideos(
        string htmlPath,
        int slidePosition,
        Action<string>? diagnostic = null)
    {
        return ReadVideos(htmlPath, slidePosition, null, diagnostic);
    }

    public static IReadOnlyList<VideoInfo> ReadVideos(
        string htmlPath,
        int slidePosition,
        string? notesHtmlPath,
        Action<string>? diagnostic = null)
    {
        if (slidePosition < 1 || !File.Exists(htmlPath))
        {
            return [];
        }

        var sections = SlideSectionRegex().Matches(File.ReadAllText(htmlPath));
        if (slidePosition > sections.Count)
        {
            return [];
        }

        var deckDirectory = Path.GetDirectoryName(Path.GetFullPath(htmlPath))!;
        var videos = new List<VideoInfo>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddVideosFromHtml(
            sections[slidePosition - 1].Value,
            deckDirectory,
            videos,
            seenPaths,
            diagnostic);

        if (!string.IsNullOrWhiteSpace(notesHtmlPath) && File.Exists(notesHtmlPath))
        {
            var notesHtml = File.ReadAllText(notesHtmlPath);
            var rawNotes = ReadRawNotesFromHtml(notesHtml, slidePosition);
            AddVideosFromHtml(
                rawNotes,
                deckDirectory,
                videos,
                seenPaths,
                diagnostic);
        }

        return videos;
    }

    private static void AddVideosFromHtml(
        string html,
        string deckDirectory,
        List<VideoInfo> videos,
        HashSet<string> seenPaths,
        Action<string>? diagnostic)
    {
        foreach (Match match in VideoTagRegex().Matches(html))
        {
            var src = SourceAttributeRegex().Match(match.Value).Groups[1].Value;
            var name = string.IsNullOrWhiteSpace(src)
                ? match.Groups[1].Value
                : src;
            name = WebUtility.HtmlDecode(name).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var path = Path.GetFullPath(Path.Combine(deckDirectory, name));
            if (!File.Exists(path))
            {
                diagnostic?.Invoke(
                    $"ReadVideos skipped missing file path={TruncateForLog(path)}");
                continue;
            }

            if (seenPaths.Add(path))
            {
                videos.Add(new VideoInfo(path, Path.GetFileName(name), false));
            }
        }
    }

    private static bool TryReadSpeakerNotesJson(string html, out JsonElement notes)
    {
        notes = default;
        var scriptMatch = SpeakerNotesJsonRegex().Match(html);
        if (!scriptMatch.Success)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(scriptMatch.Groups[1].Value);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            notes = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ReadRawNotesFromHtml(string html, int slidePosition)
    {
        if (slidePosition < 1)
        {
            return string.Empty;
        }

        if (TryReadSpeakerNotesJson(html, out var notes))
        {
            if (slidePosition > notes.GetArrayLength())
            {
                return string.Empty;
            }

            var note = notes[slidePosition - 1];
            return note.ValueKind == JsonValueKind.String
                ? note.GetString() ?? string.Empty
                : string.Empty;
        }

        var sections = SlideSectionRegex().Matches(html);
        if (slidePosition > sections.Count)
        {
            return string.Empty;
        }

        var notesMatch = SpeakerNotesRegex().Match(sections[slidePosition - 1].Value);
        return notesMatch.Success ? notesMatch.Groups[1].Value : string.Empty;
    }

    private static string CleanText(string html)
    {
        var withLineBreaks = BreakTagRegex().Replace(html, "\n");
        var withoutTags = HtmlTagRegex().Replace(withLineBreaks, string.Empty);
        return WebUtility.HtmlDecode(withoutTags)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private static string TruncateForLog(string value, int maxLength = 160)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    [GeneratedRegex(
        "<section\\b[^>]*\\bclass\\s*=\\s*[\\\"']"
        + "[^\\\"']*\\bslide\\b[^\\\"']*[\\\"'][^>]*>"
        + ".*?</section\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SlideSectionRegex();

    [GeneratedRegex(
        "<aside\\b[^>]*\\bclass\\s*=\\s*[\\\"']"
        + "[^\\\"']*\\bspeaker-notes\\b[^\\\"']*[\\\"'][^>]*>"
        + "(.*?)</aside\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpeakerNotesRegex();

    [GeneratedRegex(
        "<script\\b[^>]*\\bid\\s*=\\s*[\\\"']speaker-notes[\\\"'][^>]*>"
        + "(.*?)</script\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpeakerNotesJsonRegex();

    [GeneratedRegex("<br\\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(
        "<video\\b[^>]*>(.*?)((</video\\s*>)|(?=<)|$)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex VideoTagRegex();

    [GeneratedRegex(
        "\\bsrc\\s*=\\s*[\\\"']([^\\\"']+)[\\\"']",
        RegexOptions.IgnoreCase)]
    private static partial Regex SourceAttributeRegex();
}
