using VRCFaceTracking.Core.Helpers;

namespace VRCFTPicoModule.Utils;

public class Localization
{
    private Dictionary<string, string>? _translations;

    private Localization() { }

    private static Localization LocInstance { get; } = new();

    public static void Initialize(string languageCode)
    {
        LocInstance.LoadLanguageAsync(languageCode).GetAwaiter().GetResult();
    }

    private async Task LoadLanguageAsync(string languageCode)
    {
        var jsonContent = await LoadResourceAsync($"VRCFTPicoModule.Assets.Locales.{languageCode}.json")
                          ?? await LoadResourceAsync("VRCFTPicoModule.Assets.Locales.en-US.json");

        if (jsonContent != null)
        {
            _translations = await Json.ToObjectAsync<Dictionary<string, string>>(jsonContent);
        }
        else
        {
            _translations = new Dictionary<string, string>();
        }
    }
    
    private async Task<string?> LoadResourceAsync(string resourceName)
    {
        await using var stream = GetType().Assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private string GetTranslation(string key)
    {
        if (_translations != null && _translations.TryGetValue(key, out var translation))
        {
            return translation;
        }
        return key;
    }
    
    public static string T(string key)
    {
        return LocInstance.GetTranslation(key);
    }
}