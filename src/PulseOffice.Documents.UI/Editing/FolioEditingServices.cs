using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Spelling;

namespace PulseOffice.Documents.UI.Editing;

public sealed record FolioEditingServices(
    IDocumentEditor Editor,
    IDocumentClipboard Clipboard,
    IStoryCatalog Stories,
    IParagraphOrder Order,
    ICaretNavigator Navigator,
    ILayoutQueries Queries,
    ILayoutLines Lines,
    ILineOffsets Geometry,
    IPositionTrackerFactory Trackers,
    ICaretWord Words,
    IStyleResolver Styles,
    IWordBoundaryCorrection Boundary,
    IAutoCorrectSettings AutoCorrect,
    Func<IDocumentLayoutEngine> Engines,
    IPreeditLayout Preedit,
    IObjectFragments Objects,
    IFormatBrush Brush,
    FolioTableServices Tables,
    PulseOffice.Documents.Indexes.IGeneratedRegions Generated,
    PulseOffice.Documents.Protection.IEditGuard Guard,
    IFolioTypingAssist Assist,
    FolioControlServices Controls,
    ITextClusters Clusters,
    PulseOffice.Text.Rich.ITextCultures Cultures);

public sealed record FolioControlServices(
    PulseOffice.Documents.Controls.IContentControlCatalog Catalog,
    PulseOffice.Documents.Controls.IContentControlEditing Editing,
    PulseOffice.Documents.Controls.IContentControlGuard Guard,
    IContentControlPlaceholders Placeholders,
    PulseOffice.Documents.Protection.IFormFilling Forms);

public sealed record FolioTableServices(
    ITableEditing Editing,
    PulseOffice.Documents.Editing.Tables.ITableSelections Selections,
    PulseOffice.Documents.Editing.Tables.ITableGeometryEditing Geometry,
    PulseOffice.Documents.Editing.Tables.ITableFormulaEditing Formulas,
    PulseOffice.Documents.Editing.Tables.ITableCellFactory Cells,
    ITableMeasure Measure,
    Commands.IFolioNumberRecognition Recognition);

