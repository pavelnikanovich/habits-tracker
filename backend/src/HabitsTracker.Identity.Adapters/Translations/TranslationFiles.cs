using System.Text.Json;
using HabitsTracker.Identity.Core;

namespace HabitsTracker.Identity.Adapters.Translations;

/// <summary>
/// The translation files of the frontend, <c>&lt;code&gt;.json</c> in one folder: each file is a language, so a new
/// file is a new language with no code change (FR-020, research R11). Read once, when the host starts; a file that
/// is not named after a locale code, or that has no <c>language.name</c>, stops the start.
/// </summary>
public sealed class TranslationFiles : ILocales
{
    private const string LanguageNameKey = "language.name";

    private readonly SortedDictionary<string, Dictionary<string, string>> texts = new(StringComparer.Ordinal);
    private readonly LocaleResponse[] locales;

    public TranslationFiles(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        foreach (var file in Directory.EnumerateFiles(path, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            var code = Path.GetFileNameWithoutExtension(file);
            if (!Locale.IsCode(code))
                throw new InvalidOperationException(
                    $"The translation file {fileName} in {path} is not named after a locale code ({Locale.Pattern}).");

            var fileTexts = Read(file, fileName);
            if (string.IsNullOrWhiteSpace(fileTexts.GetValueOrDefault(LanguageNameKey)))
                throw new InvalidOperationException($"The translation file {fileName} in {path} has no {LanguageNameKey}.");

            texts.Add(code, fileTexts);
        }

        locales = [.. texts.Select(pair => new LocaleResponse(pair.Key, pair.Value[LanguageNameKey]))];
    }

    public bool IsAvailable(string code) => texts.ContainsKey(code);

    /// <summary>The available locales, ordered by code.</summary>
    public IReadOnlyList<LocaleResponse> List() => locales;

    /// <summary>The text of a key, such as <c>language.name</c>, in the locale's file.</summary>
    public string Text(Locale locale, string key)
    {
        ArgumentNullException.ThrowIfNull(locale);
        ArgumentNullException.ThrowIfNull(key);

        if (texts.TryGetValue(locale.Code, out var fileTexts) && fileTexts.TryGetValue(key, out var text))
            return text;

        throw new KeyNotFoundException($"The translation file {locale.Code}.json has no {key}.");
    }

    // Nested objects become dotted keys, as Transloco reads them: { "language": { "name": "Polski" } } is language.name.
    private static Dictionary<string, string> Read(string file, string fileName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var fileTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        Flatten(document.RootElement, prefix: "", fileTexts, fileName);
        return fileTexts;
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> fileTexts, string fileName)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix + property.Name;
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    Flatten(property.Value, key + ".", fileTexts, fileName);
                    break;
                case JsonValueKind.String:
                    fileTexts[key] = property.Value.GetString()!;
                    break;
                default:
                    throw new InvalidOperationException($"The translation file {fileName} holds a {property.Value.ValueKind} at {key}; every text is a string.");
            }
        }
    }
}
