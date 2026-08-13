using System.Text.Json;

namespace KeyGate.Client.Services;

public class AppSettings
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";
    public string DeviceNamePrefix { get; set; } = "Kiosk";
    public int IdleTimeoutMinutes { get; set; } = 5;
    public int ConfigRefreshMinutes { get; set; } = 10;

    private static AppSettings? _current;

    public static AppSettings Current => _current ??= Load();

    private static AppSettings Load()
    {
        var json = LoadJsonFromPackage() ?? LoadJsonFromFileSystem();
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("AppSettings", out var section))
            {
                return section.Deserialize<AppSettings>() ?? new AppSettings();
            }
        }
        catch (JsonException)
        {
        }

        return new AppSettings();
    }

    private static string? LoadJsonFromPackage()
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    private static string? LoadJsonFromFileSystem()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
