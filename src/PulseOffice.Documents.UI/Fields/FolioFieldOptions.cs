using System.Globalization;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;

namespace PulseOffice.Documents.UI.Fields;

public sealed record FieldOptionsContext(FolioEditor Editor, ITextCatalog Texts, DateTime Now);

public abstract class FolioFieldOptions
{
    public abstract bool Handles(FolioFieldChoice choice);

    public virtual string ExplanationKey(FolioFieldChoice choice) => "folio.field.plain.explanation";

    public virtual IReadOnlyList<FormField> Fields(FolioFieldChoice choice, TextRun current, FieldOptionsContext context) => [];

    public virtual TextRun? Compose(FolioFieldChoice choice, TextRun current, IReadOnlyDictionary<string, object> values, FieldOptionsContext context) => current;

    public virtual string? PreviewOverride(IReadOnlyDictionary<string, object> values) => null;

    public virtual void Commit(TextRun composed, IReadOnlyDictionary<string, object> values, FieldOptionsContext context)
    {
    }

    protected static FieldDetail DetailOf(TextRun run) => run.FieldDetail ?? new FieldDetail();
}

public sealed class ClockFieldOptions : FolioFieldOptions
{
    private const string Custom = "custom";

    private readonly IReadOnlyList<string?> _dates = [null, "D", "dd/MM/yyyy", "d MMMM yyyy", "dddd d MMMM yyyy", "MMMM yyyy", "yyyy-MM-dd"];

    private readonly IReadOnlyList<string?> _times = [null, "T", "HH:mm", "HH:mm:ss", "h:mm tt"];

    public override bool Handles(FolioFieldChoice choice) =>
        choice.Kind is TextFieldKind.Date or TextFieldKind.Time or TextFieldKind.CreatedDate or TextFieldKind.ModifiedDate;

    public override string ExplanationKey(FolioFieldChoice choice) => "folio.field.clock.explanation";

    public override IReadOnlyList<FormField> Fields(FolioFieldChoice choice, TextRun current, FieldOptionsContext context)
    {
        var formats = Formats(choice);
        var detail = DetailOf(current);
        var index = formats.ToList().IndexOf(detail.Format);
        var custom = index < 0;
        var options = formats.Select(format => Example(format, choice, context)).Append(context.Texts["folio.field.format.custom"]).ToList();
        var fields = new List<FormField>
        {
            new ChoiceField("format", context.Texts["folio.field.format"], options, custom ? formats.Count : index),
            new TextField(Custom, context.Texts["folio.field.format.code"], custom ? detail.Format ?? string.Empty : string.Empty, Fallback(choice))
            {
                ShownWhen = values => values.Choice("format", formats.Count + 1) == formats.Count,
            },
        };
        if (choice.Kind is TextFieldKind.Date or TextFieldKind.Time) fields.Add(new CheckField("fixed", context.Texts["folio.field.fixed"], detail.Fixed));
        return fields;
    }

    public override TextRun? Compose(FolioFieldChoice choice, TextRun current, IReadOnlyDictionary<string, object> values, FieldOptionsContext context)
    {
        var formats = Formats(choice);
        var picked = values.Choice("format", formats.Count + 1);
        var format = picked == formats.Count ? (values.Text(Custom).Trim() is { Length: > 0 } typed ? typed : null) : formats[picked];
        var fixedNow = values.Flag("fixed");
        var detail = DetailOf(current);
        return current with
        {
            FieldDetail = detail with
            {
                Format = format,
                Fixed = fixedNow,
                Value = fixedNow ? FixedValue(choice, detail, context) : null,
            },
        };
    }

    private string FixedValue(FolioFieldChoice choice, FieldDetail detail, FieldOptionsContext context) =>
        detail is { Fixed: true, Value: { Length: > 0 } kept }
            ? kept
            : context.Now.ToString(choice.Kind == TextFieldKind.Time ? "HH:mm:ss" : "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private IReadOnlyList<string?> Formats(FolioFieldChoice choice) => choice.Kind == TextFieldKind.Time ? _times : _dates;

    private string Fallback(FolioFieldChoice choice) => choice.Kind == TextFieldKind.Time ? "HH:mm" : "dd/MM/yyyy";

    private string Example(string? format, FolioFieldChoice choice, FieldOptionsContext context)
    {
        var shown = format ?? (choice.Kind == TextFieldKind.Time ? "t" : "d");
        try
        {
            return context.Now.ToString(shown, context.Editor.Culture());
        }
        catch (FormatException)
        {
            return shown;
        }
    }
}

public sealed class NamedFieldOptions(IMergeRecords merge) : FolioFieldOptions
{
    public override bool Handles(FolioFieldChoice choice) =>
        choice.Kind is TextFieldKind.CustomProperty or TextFieldKind.Database || choice is { Kind: TextFieldKind.Variable, Argument: null };

    public override string ExplanationKey(FolioFieldChoice choice) => choice.Kind switch
    {
        TextFieldKind.CustomProperty => "folio.field.property.explanation",
        TextFieldKind.Database => "folio.mailMerge.field.explanation",
        _ => "folio.field.variableGet.explanation",
    };

    public override IReadOnlyList<FormField> Fields(FolioFieldChoice choice, TextRun current, FieldOptionsContext context)
    {
        var names = Names(choice, context.Editor);
        var label = context.Texts[choice.Kind == TextFieldKind.Database ? "folio.mailMerge.column" : "folio.field.name"];
        return names.Count == 0
            ? [new TextField("name", label, current.FieldArgument ?? string.Empty, context.Texts[EmptyKey(choice)])]
            : [new ChoiceField("name", label, names, Math.Max(0, names.ToList().IndexOf(current.FieldArgument ?? string.Empty)))];
    }

    public override TextRun? Compose(FolioFieldChoice choice, TextRun current, IReadOnlyDictionary<string, object> values, FieldOptionsContext context)
    {
        var names = Names(choice, context.Editor);
        var name = names.Count == 0 ? values.Text("name").Trim() : names[values.Choice("name", names.Count)];
        return name.Length == 0 ? null : current with { FieldArgument = name };
    }

    private string EmptyKey(FolioFieldChoice choice) => choice.Kind switch
    {
        TextFieldKind.CustomProperty => "folio.field.property.placeholder",
        TextFieldKind.Database => "folio.field.database.placeholder",
        _ => "folio.field.variable.placeholder",
    };

    private IReadOnlyList<string> Names(FolioFieldChoice choice, FolioEditor editor) => choice.Kind switch
    {
        TextFieldKind.CustomProperty => [.. editor.Document.Properties.Custom.Keys.Order(StringComparer.CurrentCulture)],
        TextFieldKind.Database => [.. merge.Data(editor.Document)?.Columns ?? []],
        _ => [.. editor.Document.Variables.Keys.Concat(editor.Services.Stories.Paragraphs(editor.Document).SelectMany(paragraph => paragraph.Content.Runs)
                .Where(run => run.Field == TextFieldKind.VariableSet && run.FieldArgument is not null).Select(run => run.FieldArgument!))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.CurrentCulture)],
    };
}

public sealed class VariableSetFieldOptions : FolioFieldOptions
{
    public override bool Handles(FolioFieldChoice choice) => choice.Kind == TextFieldKind.VariableSet;

    public override string ExplanationKey(FolioFieldChoice choice) => "folio.field.variableSet.explanation";

    public override IReadOnlyList<FormField> Fields(FolioFieldChoice choice, TextRun current, FieldOptionsContext context) =>
    [
        new TextField("name", context.Texts["folio.field.name"], current.FieldArgument ?? string.Empty),
        new TextField("value", context.Texts["folio.field.value"], DetailOf(current).Format ?? string.Empty),
        new CheckField("invisible", context.Texts["folio.field.invisible"], DetailOf(current).WhenFalse == FieldVisibility.Invisible),
    ];

    public override TextRun? Compose(FolioFieldChoice choice, TextRun current, IReadOnlyDictionary<string, object> values, FieldOptionsContext context) =>
        values.Text("name").Trim() is { Length: > 0 } name
            ? current with { FieldArgument = name, FieldDetail = DetailOf(current) with { Format = values.Text("value"), WhenFalse = values.Flag("invisible") ? FieldVisibility.Invisible : null } }
            : null;
}

public sealed class UserFieldOptions : FolioFieldOptions
{
    public override bool Handles(FolioFieldChoice choice) => choice is { Kind: TextFieldKind.Variable, Argument: "user" };

    public override string ExplanationKey(FolioFieldChoice choice) => "folio.field.user.explanation";

    public override IReadOnlyList<FormField> Fields(FolioFieldChoice choice, TextRun current, FieldOptionsContext context) =>
    [
        new TextField("name", context.Texts["folio.field.name"], current.FieldArgument ?? string.Empty),
        new TextField("value", context.Texts["folio.field.value"],
            current.FieldArgument is { } existing && context.Editor.Document.Variables.TryGetValue(existing, out var value) ? value : string.Empty),
    ];

    public override TextRun? Compose(FolioFieldChoice choice, TextRun current, IReadOnlyDictionary<string, object> values, FieldOptionsContext context) =>
        values.Text("name").Trim() is { Length: > 0 } name ? current with { FieldArgument = name } : null;

    public override string? PreviewOverride(IReadOnlyDictionary<string, object> values) => values.Text("value");

    public override void Commit(TextRun composed, IReadOnlyDictionary<string, object> values, FieldOptionsContext context)
    {
        if (composed.FieldArgument is not { } name) return;
        var editor = context.Editor;
        var value = values.Text("value");
        editor.Change(() =>
        {
            var recorder = new EditRecorder(editor.Document, "user-field").Touch<KeyValuePair<string, string>>(editor.Document.Variables,
                () => [.. editor.Document.Variables], items =>
                {
                    editor.Document.Variables.Clear();
                    foreach (var (key, stored) in items) editor.Document.Variables[key] = stored;
                });
            editor.Document.Variables[name] = value;
            recorder.Commit(editor.History);
        });
    }
}

public sealed class ConditionFieldOptions : FolioFieldOptions
{
    private const string Sample = "lingua == \"it\"";

    public override bool Handles(FolioFieldChoice choice) => choice.Kind is TextFieldKind.ConditionalText or TextFieldKind.HiddenText or TextFieldKind.HiddenParagraph;

    public override string ExplanationKey(FolioFieldChoice choice) => choice.Kind switch
    {
        TextFieldKind.ConditionalText => "folio.field.conditional.explanation",
        TextFieldKind.HiddenText => "folio.field.hidden.explanation",
        _ => "folio.field.hiddenParagraph.explanation",
    };

    public override IReadOnlyList<FormField> Fields(FolioFieldChoice choice, TextRun current, FieldOptionsContext context)
    {
        var detail = DetailOf(current);
        var texts = context.Texts;
        FormField condition = new TextField("condition", texts["folio.field.condition"], current.FieldArgument ?? string.Empty, Sample);
        return choice.Kind switch
        {
            TextFieldKind.ConditionalText =>
            [
                condition,
                new TextField("then", texts["folio.field.then"], detail.WhenTrue ?? string.Empty),
                new TextField("else", texts["folio.field.else"], detail.WhenFalse ?? string.Empty),
            ],
            TextFieldKind.HiddenText => [condition, new TextField("text", texts["folio.field.text"], detail.WhenTrue ?? string.Empty)],
            _ => [condition],
        };
    }

    public override TextRun? Compose(FolioFieldChoice choice, TextRun current, IReadOnlyDictionary<string, object> values, FieldOptionsContext context)
    {
        if (values.Text("condition").Trim() is not { Length: > 0 } condition) return null;
        var detail = DetailOf(current);
        return choice.Kind switch
        {
            TextFieldKind.ConditionalText => current with { FieldArgument = condition, FieldDetail = detail with { WhenTrue = values.Text("then"), WhenFalse = values.Text("else") } },
            TextFieldKind.HiddenText => values.Text("text") is { Length: > 0 } text ? current with { FieldArgument = condition, FieldDetail = detail with { WhenTrue = text } } : null,
            _ => current with { Text = ConditionalFieldSource.HiddenParagraphMark, FieldArgument = condition },
        };
    }
}

public sealed class PlainFieldOptions : FolioFieldOptions
{
    public override bool Handles(FolioFieldChoice choice) => true;
}
