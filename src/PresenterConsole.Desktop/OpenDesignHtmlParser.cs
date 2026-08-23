using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    private static string CleanText(string html)
    {
        var withLineBreaks = BreakTagRegex().Replace(html, "\n");
        var withoutTags = HtmlTagRegex().Replace(withLineBreaks, string.Empty);
        return WebUtility.HtmlDecode(withoutTags)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
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
}
