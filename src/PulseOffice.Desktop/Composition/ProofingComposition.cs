using PulseOffice.Formats.Intake;
using PulseOffice.Documents;
using PulseOffice.Foundation.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PulseOffice.Diagnostics;
using PulseOffice.Platform;
using PulseOffice.Remote;
using PulseOffice.Remote.Cloud;
using PulseOffice.Sheets;
using PulseOffice.Settings;
using PulseOffice.Foundation.Windowing;
using PulseOffice.Shell;
using PulseOffice.Shell.Surfaces;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Formats;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Formats.Pdf;
using PulseOffice.Printing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Document.Forms;
using PulseOffice.Security;
using PulseOffice.Text.Scripts;
using PulseOffice.Text.Spelling;
using PulseOffice.Text.Spelling.Hunspell;
using PulseOffice.Sheets.UI;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shortcuts;
using PulseOffice.UI.Theming;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.Text.Rich;
using Avalonia;

namespace PulseOffice.Desktop.Composition;

internal static class ProofingComposition
{
    public static IServiceCollection AddProofing(this IServiceCollection services, IUserDataPaths paths)
    {
        services.AddSingleton<IWordTokenizer, WordTokenizer>();
        services.AddSingleton<AutoCorrectEntryNormalizer>();
        services.AddSingleton<IAutoCorrectJsonReader, AutoCorrectJsonReader>();
        services.AddSingleton<IAutoCorrectListReader, AcorListReader>();
        services.AddSingleton<IAutoCorrectSource>(provider => new BundledAutoCorrectSource(
            Path.Combine(AppContext.BaseDirectory, "autocorrect"),
            provider.GetRequiredService<IAutoCorrectJsonReader>(),
            EntryOrigin.Bundled, priority: 0));
        services.AddSingleton<IAutoCorrectSource>(provider => new BundledAutoCorrectSource(
            Path.Combine(AppContext.BaseDirectory, "autocorrect-pulseoffice"),
            provider.GetRequiredService<IAutoCorrectJsonReader>(),
            EntryOrigin.BuiltIn, priority: 10));
        services.AddSingleton<ITextLanguageResolver, TextLanguageResolver>();
        services.AddSingleton<IAutoCorrectDefaults, AutoCorrectDefaults>();
        services.AddSingleton<IAutoCorrectTable, AutoCorrectTable>();
        services.AddSingleton<IAutoCorrect, AutoCorrect>();
        services.AddSingleton<IWordBoundaryCorrection, WordBoundaryCorrection>();
        services.AddSingleton<IAutoCorrectSettings, AutoCorrectSettings>();
        services.AddSingleton<IAutoCorrectProfileCodec, AutoCorrectProfileCodec>();
        services.AddSingleton<IAutoCorrectProfileStore>(provider => new AutoCorrectProfileStore(
            provider.GetRequiredService<IAutoCorrectProfileCodec>(), paths.Configuration("autocorrect.json")));
        services.AddSingleton<IAutoCorrectPreferences, AutoCorrectPreferences>();
        services.AddSingleton<IAutoCorrectDraftFactory, AutoCorrectDraftFactory>();
        services.AddSingleton<IReplacementQuery, ReplacementQuery>();
        services.AddSingleton<PulseOffice.UI.Controls.AutoCorrection.AutoCorrectViewServices>();
        services.AddSingleton<ShellAutoCorrectCommands>();
        services.AddSingleton<IStyleCatalogAccess, StyleCatalogAccess>();
        services.AddSingleton<IAutoSpellSettings, AutoSpellSettings>();
        services.AddSingleton<ISheetSpelling, SheetSpelling>();
        services.AddSingleton<PulseOffice.Presentations.IDeckSpelling, PulseOffice.Presentations.DeckSpelling>();
        services.AddSingleton<ISpellChecker, SpellChecker>();
        services.AddSingleton<IGrammarRule, RepeatedWordRule>();
        services.AddSingleton<IGrammarRule, DoubleSpaceRule>();
        services.AddSingleton<IGrammarRule, SpaceBeforePunctuationRule>();
        services.AddSingleton<IGrammarRule, SentenceStartRule>();
        services.AddSingleton<IGrammarChecker, GrammarChecker>();
        services.AddSingleton<PulseOffice.UI.Controls.Spelling.TextReviewSession>();
        services.AddSingleton<PulseOffice.UI.Controls.Spelling.ITextReviewSession>(s => s.GetRequiredService<PulseOffice.UI.Controls.Spelling.TextReviewSession>());
        services.AddSingleton<ITextReviewEditWatch, TextReviewEditWatch>();
        services.AddSingleton<PulseOffice.UI.Controls.Spelling.IDictionaryNotice>(s => s.GetRequiredService<PulseOffice.UI.Controls.Spelling.TextReviewSession>());
        services.AddSingleton<PulseOffice.UI.Controls.Spelling.ISpellingPrompt>(s => s.GetRequiredService<PulseOffice.UI.Controls.Spelling.TextReviewSession>());
        services.AddSingleton<PulseOffice.UI.Controls.Spelling.IThesaurusPrompt>(s => s.GetRequiredService<PulseOffice.UI.Controls.Spelling.TextReviewSession>());
        services.AddSingleton<PulseOffice.UI.Controls.Spelling.ILanguagePrompt>(s => s.GetRequiredService<PulseOffice.UI.Controls.Spelling.TextReviewSession>());
        services.AddSingleton<PulseOffice.UI.Controls.Spelling.IDictionaryLanguageNames, PulseOffice.UI.Controls.Spelling.DictionaryLanguageNames>();
        services.AddSingleton<PulseOffice.UI.Controls.Spelling.ISpellingLanguagePicker, PulseOffice.UI.Controls.Spelling.SpellingLanguagePicker>();

        services.AddSingleton<PulseOffice.Platform.ISystemDictionaryDirectories>(provider => new PulseOffice.Platform.SystemDictionaryDirectories(provider.GetRequiredService<PulseOffice.Platform.IEnvironmentVariables>(), provider.GetRequiredService<PulseOffice.Platform.IHostSystem>().Family));
        services.AddSingleton<IDictionaryLocations>(provider => new DictionaryLocations(
            paths.Configuration("dictionaries"),
            provider.GetRequiredService<PulseOffice.Platform.ISystemDictionaryDirectories>()));
        services.AddSingleton<PulseOffice.Text.Spelling.Dictionaries.IDictionaryChanges, PulseOffice.Text.Spelling.Dictionaries.DictionaryChanges>();
        services.AddSingleton<PulseOffice.Text.Spelling.Dictionaries.IDictionaryCodes, PulseOffice.Text.Spelling.Dictionaries.DictionaryCodes>();
        services.AddSingleton<PulseOffice.Text.Spelling.Dictionaries.IDictionaryLicenseClassifier, PulseOffice.Text.Spelling.Dictionaries.DictionaryLicenseClassifier>();
        services.AddSingleton<PulseOffice.Text.Spelling.Dictionaries.ITexHyphenationConverter, PulseOffice.Text.Spelling.Dictionaries.TexHyphenationConverter>();
        services.AddSingleton<PulseOffice.Text.Spelling.Dictionaries.ITexLicenseReader, PulseOffice.Text.Spelling.Dictionaries.TexLicenseReader>();
        services.AddSingleton(_ => new PulseOffice.Text.Spelling.Hunspell.Catalog.FileDictionaryServiceOptionsStore(
            paths.Configuration("dictionaries.json")).Load());
        services.AddSingleton<PulseOffice.Text.Spelling.Hunspell.Catalog.IDictionaryHttp, PulseOffice.Text.Spelling.Hunspell.Catalog.DictionaryHttp>();
        services.AddSingleton<PulseOffice.Text.Spelling.Hunspell.Catalog.IDictionaryCacheStore>(_ => new PulseOffice.Text.Spelling.Hunspell.Catalog.DictionaryCacheStore(
            paths.Configuration("dictionaries-cache")));
        services.AddSingleton<PulseOffice.Text.Spelling.Dictionaries.IDictionaryRanking, PulseOffice.Text.Spelling.Dictionaries.DictionaryRanking>();
        services.AddSingleton<PulseOffice.Text.Spelling.Dictionaries.IWordNetThesaurusConverter, PulseOffice.Text.Spelling.Dictionaries.WordNetThesaurusConverter>();
        services.AddSingleton<PulseOffice.Text.Spelling.Dictionaries.IMyThesWriter, PulseOffice.Text.Spelling.Dictionaries.MyThesWriter>();
        services.AddSingleton<PulseOffice.Text.Spelling.Hunspell.Catalog.IDictionaryArchive>(_ => new PulseOffice.Text.Spelling.Hunspell.Catalog.DictionaryArchive());
        Type[] dictionarySources =
        [
            typeof(PulseOffice.Text.Spelling.Hunspell.Catalog.WooormSpellingSource),
            typeof(PulseOffice.Text.Spelling.Hunspell.Catalog.MozillaSpellingSource),
            typeof(PulseOffice.Text.Spelling.Hunspell.Catalog.TexHyphenationSource),
            typeof(PulseOffice.Text.Spelling.Hunspell.Catalog.WordNetThesaurusSource),
            typeof(PulseOffice.Text.Spelling.Hunspell.Catalog.OpenThesaurusSource),
        ];
        foreach (var sourceType in dictionarySources)
        {
            services.AddSingleton(sourceType);
            services.AddSingleton<PulseOffice.Text.Spelling.Hunspell.Catalog.IRefreshableDictionarySource>(provider => new PulseOffice.Text.Spelling.Hunspell.Catalog.CachedDictionarySource(
                (PulseOffice.Text.Spelling.Dictionaries.IDictionarySource)provider.GetRequiredService(sourceType),
                provider.GetRequiredService<PulseOffice.Text.Spelling.Hunspell.Catalog.IDictionaryCacheStore>(),
                provider.GetRequiredService<PulseOffice.Text.Spelling.Hunspell.Catalog.DictionaryServiceOptions>(),
                TimeProvider.System,
                provider.GetRequiredService<PulseOffice.Text.Spelling.Dictionaries.IDictionaryCodes>()));
        }

        services.AddSingleton<PulseOffice.Text.Spelling.Hunspell.Catalog.IInstalledDictionaries, PulseOffice.Text.Spelling.Hunspell.Catalog.InstalledDictionaries>();
        services.AddSingleton<PulseOffice.Text.Spelling.Hunspell.Catalog.ISystemDictionaryInventory, PulseOffice.Text.Spelling.Hunspell.Catalog.SystemDictionaryInventory>();
        services.AddSingleton<PulseOffice.Text.Spelling.Dictionaries.IDictionaryManager, PulseOffice.Text.Spelling.Hunspell.Catalog.DictionaryManager>();
        services.AddSingleton<PulseOffice.UI.Controls.Dictionaries.DictionaryManagerServices>();
        services.AddSingleton<ShellDictionaryCommands>();

        services.AddSingleton<ISpellDictionaryProvider, HunspellDictionaryProvider>();
        services.AddSingleton<PulseOffice.Text.Spelling.Hunspell.IDeclaredEncodingText, PulseOffice.Text.Spelling.Hunspell.DeclaredEncodingText>();
        services.AddSingleton<IHyphenationProvider, FileHyphenationProvider>();
        services.AddSingleton<PulseOffice.Documents.Layout.Inline.IHyphenationPoints, PulseOffice.Documents.Layout.Inline.DictionaryHyphenation>();
        services.AddSingleton<IThesaurusProvider, FileThesaurusProvider>();

        services.AddSingleton<IUserDictionary>(provider => new UserDictionaryFile(
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<UserDictionaryFile>(),
            paths.Configuration("user-words.txt")));

        services.AddSingleton<LedgerSpellingCommands>();

        return services;
    }
}
