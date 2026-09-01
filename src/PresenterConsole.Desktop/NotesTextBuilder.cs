namespace PresenterConsole.Desktop;

public static class NotesTextBuilder
{
    public static string BuildParagraphPrefix(
        int bulletType,
        int visible,
        int character,
        int number)
    {
        if (visible != -1)
        {
            return string.Empty;
        }

        return bulletType switch
        {
            1 => $"{(char)character} ",
            2 => $"{number}. ",
            _ => string.Empty,
        };
    }
}
