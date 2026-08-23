using System.Net;
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
        var sections = SlideSectionRegex().Matches(html);
        if (slidePosition > sections.Count)
        {
            return string.Empty;
        }

        var section = sections[slidePosition - 1].Value;
        var notesMatch = SpeakerNotesRegex().Match(section);
        return notesMatch.Success ? CleanText(notesMatch.Groups[1].Value) : string.Empty;
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

    [GeneratedRegex("<br\\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();
}
