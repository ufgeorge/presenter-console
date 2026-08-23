using System.Text.Json;

namespace PresenterConsole.Desktop;

public sealed class OpenDesignSettings
{
    public List<string> ProjectRoots { get; set; } = [];
    public string LastAdapter { get; set; } = "PowerPoint";

    public static string FilePath => Path.Combine(
        AppContext.BaseDirectory,
        "opendesign.projects.json");

    public static OpenDesignSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<OpenDesignSettings>(
                    File.ReadAllText(FilePath)) ?? new OpenDesignSettings();
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        return new OpenDesignSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                this,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
