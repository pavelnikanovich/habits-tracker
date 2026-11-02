using HabitsTracker.Identity.Adapters.Translations;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using HabitsTracker.Tests.PortContracts;

namespace HabitsTracker.Identity.Adapters.Tests;

// The languages are the translation files of the frontend: a new file is a new language (FR-020, research R11).
public sealed class TranslationFilesTests : IDisposable
{
    private static readonly string FrontendFiles = Repository.PathTo("frontend/public/i18n");

    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("translation-files-");

    public void Dispose() => folder.Delete(recursive: true);

    [Fact]
    public void Lists_the_files()
    {
        var files = new TranslationFiles(FrontendFiles);

        Assert.Equal(
            [new LocaleResponse("en", "English"), new LocaleResponse("pl", "Polski"), new LocaleResponse("ru", "Русский")],
            files.List());
    }

    [Fact]
    public void A_new_file_is_a_new_language()
    {
        CopyFrontendFiles();
        Write("de.json", """{ "language": { "name": "Deutsch" } }""");

        var files = new TranslationFiles(folder.FullName);

        Assert.True(files.IsAvailable("de"));
        Assert.Contains(new LocaleResponse("de", "Deutsch"), files.List());
    }

    [Fact]
    public void A_file_without_a_language_name_fails_at_load()
    {
        CopyFrontendFiles();
        Write("de.json", """{ "language": { "switcher": "Sprache" } }""");

        var exception = Assert.Throws<InvalidOperationException>(() => new TranslationFiles(folder.FullName));

        Assert.Contains("de.json", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("README.json")]
    [InlineData("pl-PL.json")]
    public void A_file_whose_name_is_not_a_locale_code_fails_at_load(string fileName)
    {
        CopyFrontendFiles();
        Write(fileName, """{ "language": { "name": "Polski" } }""");

        var exception = Assert.Throws<InvalidOperationException>(() => new TranslationFiles(folder.FullName));

        Assert.Contains(fileName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_reads_a_key_of_the_locale()
    {
        var files = new TranslationFiles(FrontendFiles);

        Assert.Equal("Polski", files.Text(Locale.FromStorage("pl"), "language.name"));
        Assert.Throws<KeyNotFoundException>(() => files.Text(Locale.FromStorage("pl"), "no.such.key"));
    }

    private void CopyFrontendFiles()
    {
        foreach (var file in Directory.EnumerateFiles(FrontendFiles, "*.json"))
            File.Copy(file, Path.Combine(folder.FullName, Path.GetFileName(file)));
    }

    private void Write(string fileName, string json) => File.WriteAllText(Path.Combine(folder.FullName, fileName), json);

    public sealed class TranslationFilesLocalesContract : LocalesContract
    {
        protected override ILocales Locales { get; } = new TranslationFiles(FrontendFiles);
    }
}
