using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.Data.UI;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Formulas;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Formulas;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.UI.Embedding;

public interface IContentPainter
{
    bool CanPaint(EmbeddedObject content);

    void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, double pixelsPerPoint = 1, PulseOffice.UI.Controls.Shapes.ShapeSurroundings backdrop = default);
}

public abstract class ContentPainter<TContent> : IContentPainter where TContent : EmbeddedObject
{
    public virtual bool CanPaint(EmbeddedObject content) => content is TContent;

    public void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, double pixelsPerPoint = 1, PulseOffice.UI.Controls.Shapes.ShapeSurroundings backdrop = default)
    {
        if (content is TContent typed) Paint(context, typed, bounds, pixelsPerPoint, backdrop);
    }

    protected virtual void Paint(DrawingContext context, TContent content, Rect bounds, double pixelsPerPoint, PulseOffice.UI.Controls.Shapes.ShapeSurroundings surroundings) =>
        Paint(context, content, bounds, pixelsPerPoint);

    protected virtual void Paint(DrawingContext context, TContent content, Rect bounds, double pixelsPerPoint) =>
        Paint(context, content, bounds);

    protected abstract void Paint(DrawingContext context, TContent content, Rect bounds);
}

public sealed class FormulaContentPainter(IFormulaObjectRenderer formulas) : ContentPainter<FormulaObject>
{
    protected override void Paint(DrawingContext context, FormulaObject formula, Rect bounds) =>
        formulas.Render(context, formula, bounds);
}

public sealed class ChartContentPainter(ChartRenderer charts, PulseOffice.Text.Rich.ITextCultures cultures) : ContentPainter<ChartDefinition>
{
    protected override void Paint(DrawingContext context, ChartDefinition chart, Rect bounds, double pixelsPerPoint, PulseOffice.UI.Controls.Shapes.ShapeSurroundings surroundings) =>
        charts.Render(context, chart, chart.InlineData ?? ChartData.Empty, bounds, cultures.For(surroundings.Language));

    protected override void Paint(DrawingContext context, ChartDefinition chart, Rect bounds) =>
        Paint(context, chart, bounds, 1, new PulseOffice.UI.Controls.Shapes.ShapeSurroundings());
}

public sealed class TableContentPainter(IDataTableRenderer tables, PulseOffice.Text.Rich.ITextCultures cultures) : ContentPainter<DataTableObject>
{
    protected override void Paint(DrawingContext context, DataTableObject table, Rect bounds) =>
        Paint(context, table, bounds, 1, new PulseOffice.UI.Controls.Shapes.ShapeSurroundings());

    protected override void Paint(DrawingContext context, DataTableObject table, Rect bounds, double pixelsPerPoint, PulseOffice.UI.Controls.Shapes.ShapeSurroundings surroundings)
    {
        var culture = cultures.For(surroundings.Language);
        if (pixelsPerPoint <= 0)
        {
            tables.Render(context, table, bounds, culture);
            return;
        }

        using (context.PushTransform(Matrix.CreateScale(pixelsPerPoint, pixelsPerPoint) * Matrix.CreateTranslation(bounds.X, bounds.Y)))
            tables.Render(context, table, new Rect(0, 0, bounds.Width / pixelsPerPoint, bounds.Height / pixelsPerPoint), culture);
    }
}

public sealed class FormControlContentPainter(IFormControlRenderer controls, PulseOffice.Text.Rich.ITextCultures cultures) : ContentPainter<FormControlObject>
{
    private readonly FormControlPalette _palette = new(
        new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
        new SolidColorBrush(Colors.White),
        new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
        new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
        new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
        new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
        new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)));

    protected override void Paint(DrawingContext context, FormControlObject form, Rect bounds, double pixelsPerPoint, PulseOffice.UI.Controls.Shapes.ShapeSurroundings surroundings) =>
        controls.Render(context, form.ControlKind, form.Properties, form.Properties.Items, bounds, _palette, cultures.For(surroundings.Language));

    protected override void Paint(DrawingContext context, FormControlObject form, Rect bounds) =>
        Paint(context, form, bounds, 1, new PulseOffice.UI.Controls.Shapes.ShapeSurroundings());
}

public sealed class SignatureLineContentPainter(PulseOffice.UI.Controls.Forms.ISignatureLineRenderer lines, PulseOffice.Text.Rich.ITextCultures cultures) : ContentPainter<PulseOffice.Document.Forms.SignatureLineObject>
{
    private readonly FormControlPalette _palette = new(
        new SolidColorBrush(Colors.White),
        new SolidColorBrush(Colors.White),
        new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
        new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
        new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
        new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
        new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)));

    protected override void Paint(DrawingContext context, PulseOffice.Document.Forms.SignatureLineObject line, Rect bounds, double pixelsPerPoint, PulseOffice.UI.Controls.Shapes.ShapeSurroundings surroundings) =>
        lines.Render(context, line, bounds, _palette, cultures.For(surroundings.Language));

    protected override void Paint(DrawingContext context, PulseOffice.Document.Forms.SignatureLineObject line, Rect bounds) =>
        Paint(context, line, bounds, 1, new PulseOffice.UI.Controls.Shapes.ShapeSurroundings());
}

public sealed class PackagedContentPainter(IPackagedObjectRenderer packaged) : ContentPainter<PackagedObject>
{
    private readonly PackagedPalette _palette = new(
        new SolidColorBrush(Colors.White),
        new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
        new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
        new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)));

    protected override void Paint(DrawingContext context, PackagedObject content, Rect bounds) =>
        packaged.Render(context, content, bounds, _palette);
}

public sealed class DrawingContentPainter(IDrawingObjectRenderer objects) : IContentPainter
{
    public bool CanPaint(EmbeddedObject content) => objects.CanRender(content);

    public void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, double pixelsPerPoint = 1, PulseOffice.UI.Controls.Shapes.ShapeSurroundings backdrop = default)
    {
        if (pixelsPerPoint <= 0 || Math.Abs(pixelsPerPoint - 1) < 1e-9)
        {
            objects.Render(context, content, bounds, backdrop);
            return;
        }

        using (context.PushTransform(Matrix.CreateScale(pixelsPerPoint, pixelsPerPoint) * Matrix.CreateTranslation(bounds.X, bounds.Y)))
            objects.Render(context, content, new Rect(0, 0, bounds.Width / pixelsPerPoint, bounds.Height / pixelsPerPoint), backdrop);
    }
}

public sealed class ContentPainterChain(IEnumerable<IContentPainter> painters) : IContentPainter
{
    private readonly IReadOnlyList<IContentPainter> _painters = [.. painters];

    public bool CanPaint(EmbeddedObject content) => _painters.Any(painter => painter.CanPaint(content));

    public void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, double pixelsPerPoint = 1, PulseOffice.UI.Controls.Shapes.ShapeSurroundings backdrop = default) =>
        _painters.FirstOrDefault(painter => painter.CanPaint(content))?.Paint(context, content, bounds, pixelsPerPoint, backdrop);
}

public sealed class StandardContentPainters(IDrawingObjectRenderer objects, PulseOffice.Localization.ITextCatalog texts) : IContentPainter
{
    private readonly ContentPainterChain _chain = new(
    [
        new FormulaContentPainter(new FormulaObjectRenderer(new FormulaRenderer(), new FormulaParser())),
        new ChartContentPainter(new ChartRenderer(texts), new PulseOffice.Text.Rich.TextCultures()),
        new TableContentPainter(new DataTableRenderer(texts), new PulseOffice.Text.Rich.TextCultures()),
        new FormControlContentPainter(new PulseOffice.UI.Controls.Forms.FormControlRenderer(), new PulseOffice.Text.Rich.TextCultures()),
        new SignatureLineContentPainter(new PulseOffice.UI.Controls.Forms.SignatureLineRenderer(), new PulseOffice.Text.Rich.TextCultures()),
        new PackagedContentPainter(new PulseOffice.UI.Controls.Objects.PackagedObjectRenderer()),
        new DrawingContentPainter(objects),
    ]);

    public bool CanPaint(EmbeddedObject content) => _chain.CanPaint(content);

    public void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, double pixelsPerPoint = 1, PulseOffice.UI.Controls.Shapes.ShapeSurroundings backdrop = default) =>
        _chain.Paint(context, content, bounds, pixelsPerPoint, backdrop);
}
