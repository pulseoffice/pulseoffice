using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Inline;
using PulseOffice.Documents.Layout.Lines;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout;

public static class LayoutServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentsLayout(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new LayoutOptions());
        services.TryAddSingleton<PulseOffice.Drawing.Shapes.IShapeTextInk, PulseOffice.Drawing.Shapes.ShapeTextInk>();
        services.TryAddSingleton<IHyphenationPoints, NoHyphenation>();
        services.TryAddSingleton<IParagraphFlowSettings, ModelFlowSettings>();
        services.TryAddSingleton<IListLabels>(new ListLabels([new NumberMarker(), new BulletMarker()]));
        services.AddSingleton<INumberFormatter, ArabicNumbers>();
        services.AddSingleton<IFieldLayoutFactsFactory, FieldLayoutFactsFactory>();
        services.AddSingleton<INumberFormatter>(new LetterNumbers(false));
        services.AddSingleton<INumberFormatter>(new LetterNumbers(true));
        services.AddSingleton<INumberFormatter>(new RomanNumbers(false));
        services.AddSingleton<INumberFormatter>(new RomanNumbers(true));
        services.AddSingleton<IListLabeling, ListLabeling>();
        services.AddSingleton<IRunFonts, RunFonts>();
        services.TryAddSingleton<ILineSpacingRules, LineSpacingRules>();
        services.AddSingleton<IHyphenationLimits, HyphenationLimits>();
        services.AddSingleton<ITextCaseMapping, TextCaseMapping>();
        services.AddSingleton<IAsianScriptRanges, AsianScriptRanges>();
        services.AddSingleton<IRunTypesetter, RunTypesetter>();
        services.AddSingleton<IDropCapShaper, DropCapShaper>();
        services.AddSingleton<IParagraphShaper, ParagraphShaper>();
        services.AddSingleton<IBulletPictureSizing, BulletPictureSizing>();
        services.AddSingleton<ITabResolver, TabResolver>();
        services.TryAddSingleton<ILineBreakOpportunities, IdeographicBreaks>();
        services.AddSingleton<ILineBreaker, LineBreaker>();
        services.TryAddSingleton<PulseOffice.Text.Rich.Bidi.IBidiClasses, PulseOffice.Text.Rich.Bidi.UnicodeBidiClasses>();
        services.TryAddSingleton<PulseOffice.Text.Rich.Bidi.IBidiMirroring, PulseOffice.Text.Rich.Bidi.BidiMirroring>();
        services.TryAddSingleton<PulseOffice.Text.Rich.Bidi.IBidiAlgorithm, PulseOffice.Text.Rich.Bidi.BidiAlgorithm>();
        services.TryAddSingleton<PulseOffice.Text.Rich.Bidi.IBidiReordering, PulseOffice.Text.Rich.Bidi.BidiReordering>();
        services.AddSingleton<ILineBidi, LineBidi>();
        services.AddSingleton<ILineComposer, LineComposer>();
        services.AddSingleton<IParagraphComposition, ParagraphComposition>();
        services.AddSingleton<PageGeometries>();
        services.AddSingleton<IContinuousLayout, ContinuousLayout>();
        services.AddSingleton<IPageGeometries, ViewPageGeometries>();
        services.AddSingleton<IPageFieldText, PageFieldText>();
        services.AddSingleton<IPageNumberText, PageNumberText>();
        services.AddSingleton<IPageCountFields, PageCountFields>();
        services.TryAddSingleton(new PageFlowOptions());
        services.AddSingleton<IInlineInserter, InlineInserter>();
        services.AddSingleton<IBorderStrokes, BorderStrokes>();
        services.AddSingleton<IPageBackdrops, PageBackdrops>();
        services.AddSingleton<IParagraphDecorator, ParagraphDecorator>();
        services.AddSingleton<BlockStacker>();
        services.AddSingleton<IStoryComposer>(provider => provider.GetRequiredService<BlockStacker>());
        services.AddSingleton<ITableComposer>(provider => provider.GetRequiredService<BlockStacker>());
        services.AddSingleton<IObjectPlacement, ObjectPlacement>();
        services.AddSingleton<IWrapSegments, WrapSegments>();
        services.AddSingleton<IObjectOutlines, ObjectOutlines>();
        services.AddSingleton<IColumnBalancer, ColumnBalancer>();
        services.AddTransient<IParagraphEntries, ParagraphEntries>();
        services.AddSingleton<IChangeSpans, ChangeSpans>();
        services.AddTransient<IChangeMarkup, ChangeMarkup>();
        services.AddTransient<IPlaceholderMarkup, PlaceholderMarkup>();
        services.AddSingleton<IHiddenParagraphs, HiddenParagraphs>();
        services.AddTransient<IFlowBuilder, FlowBuilder>();
        services.AddTransient<ITableMeasure, TableMeasure>();
        services.AddTransient<IPageFrames, PageFrames>();
        services.AddSingleton<IPaginator, Paginator>();
        services.TryAddSingleton<ITextClusters, TextClusters>();
        services.AddSingleton<ILineOffsets, LineOffsets>();
        services.AddSingleton<ILayoutQueries, LayoutQueries>();
        services.AddSingleton<ILineNumbers, LineNumbers>();
        services.AddTransient<IDocumentLayoutEngine, DocumentLayoutEngine>();
        services.TryAddSingleton<Func<IDocumentLayoutEngine>>(provider => provider.GetRequiredService<IDocumentLayoutEngine>);
        services.AddSingleton<IPrintLayouts, PrintLayouts>();

        return services;
    }
}
