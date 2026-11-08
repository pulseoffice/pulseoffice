using System.Globalization;
using PulseOffice.Document.Forms;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Forms;

public interface IFormControlDialogs
{
    Task<FormControlProperties?> EditAsync(FormControlObject control, Macros.MacroEventsContext events, string titleKey, CancellationToken ct);

    Task<IReadOnlyList<string>?> TabOrderAsync(IReadOnlyList<FormControlProperties> controls, string titleKey, CancellationToken ct);
}

public sealed class FormControlDialogs(
    IFormControlCatalog catalog,
    ITabOrdering ordering,
    IDialogService dialogs,
    Fonts.IFontPickerFactory fonts,
    Macros.IMacroEventsDialog macroEvents,
    ITextCatalog texts) : IFormControlDialogs
{
    public async Task<FormControlProperties?> EditAsync(
        FormControlObject control, Macros.MacroEventsContext events, string titleKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(events);

        var descriptor = catalog.Describe(control.ControlKind);
        var properties = control.Properties;

        var general = new FormFieldsPanel(GeneralFields(properties), texts);
        var dataFields = DataFields(descriptor, properties);
        var data = new FormFieldsPanel(dataFields, texts);
        var look = new FormFieldsPanel(AppearanceFields(properties), texts);
        var slots = EventSlots(descriptor, properties);
        var eventsPanel = macroEvents.CreatePanel(
            slots,
            slots.ToDictionary(slot => slot.Key, slot => properties.MacroFor(Enum.Parse<FormControlEvent>(slot.Key))),
            events);

        List<DialogTab> tabs = [new DialogTab(texts["form.tab.general"], () => general)];
        if (dataFields.Count > 0) tabs.Add(new DialogTab(texts["form.tab.data"], () => data));
        tabs.Add(new DialogTab(texts["form.tab.appearance"], () => look));
        tabs.Add(new DialogTab(texts["form.tab.events"], () => eventsPanel));

        var view = new TabbedDialogView(
            tabs,
            () => new FormAnswers(general.Values, data.Values, look.Values, eventsPanel.Assignments.Current),
            texts);

        var answers = await dialogs.ShowAsync<FormAnswers>(
            view, new DialogOptions { TitleKey = titleKey, PreferredWidth = 960 }, ct).ConfigureAwait(true);

        return answers is null ? null : Merge(descriptor, properties, answers);
    }

    public async Task<IReadOnlyList<string>?> TabOrderAsync(
        IReadOnlyList<FormControlProperties> controls, string titleKey, CancellationToken ct)
    {
        var ordered = ordering.Normalize(controls);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts["form.tabOrder.explanation"],
                [
                    new Lists.ListField(
                        "order", texts["form.tabOrder.list"], [.. ordered.Select(control => control.Name)],
                        Lists.ListEditMode.ReorderOnly,
                        Describe: name => ordered.FirstOrDefault(control => control.Name == name)?.LinkedCell is { } cell
                            ? texts.Format("form.tabOrder.linked", cell)
                            : null),
                ],
                texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 420 }, ct).ConfigureAwait(true);

        return values is null
            ? null
            : values.GetValueOrDefault("order") as IReadOnlyList<string> ?? [];
    }

    private sealed record FormAnswers(
        IReadOnlyDictionary<string, object> General,
        IReadOnlyDictionary<string, object> Data,
        IReadOnlyDictionary<string, object> Appearance,
        IReadOnlyDictionary<string, string?> Events);

    private List<FormField> GeneralFields(FormControlProperties properties) =>
    [
        new TextField("name", texts["form.property.name"], properties.Name),
        new TextField("label", texts["form.property.label"], properties.Label ?? string.Empty),
        new TextField("tooltip", texts["form.property.tooltip"], properties.Tooltip ?? string.Empty),
        new TextField("help", texts["form.property.helpText"], properties.HelpText ?? string.Empty),
        new CheckField("enabled", texts["form.property.enabled"], properties.Enabled),
        new CheckField("visible", texts["form.property.visible"], properties.Visible),
        new CheckField("printable", texts["form.property.printable"], properties.Printable),
        new CheckField("readOnly", texts["form.property.readOnly"], properties.ReadOnly),
        new CheckField("tabStop", texts["form.property.tabStop"], properties.TabStop),
        new NumberField("tabIndex", texts["form.property.tabIndex"], properties.TabIndex, 0, 999),
    ];

    private List<FormField> DataFields(
        FormControlDescriptor descriptor, FormControlProperties properties)
    {
        var range = properties.Range ?? new NumericRange(0, 100, 1, 10);

        List<FormField> fields =
        [
            new TextField("linkedCell", texts["form.property.linkedCell"], properties.LinkedCell ?? string.Empty),
        ];

        if (descriptor.Groups.HasFlag(FormPropertyGroup.List))
        {
            fields.Add(new TextField(
                "sourceRange", texts["form.property.sourceRange"], properties.SourceRange ?? string.Empty));

            fields.Add(new Lists.ListField("items", texts["form.property.items"], properties.Items));

            fields.Add(new CheckField("multiSelect", texts["form.property.multiSelect"], properties.MultiSelect));
            fields.Add(new NumberField("boundColumn", texts["form.property.boundColumn"], properties.BoundColumn, 1, 64));
            fields.Add(new CheckField("dropdown", texts["form.property.dropdown"], properties.Dropdown));
            fields.Add(new NumberField("dropdownLines", texts["form.property.dropdownLines"], properties.DropdownLines, 1, 40));
        }

        if (descriptor.Groups.HasFlag(FormPropertyGroup.Numeric))
        {
            fields.Add(new NumberField("minimum", texts["form.property.minimum"], range.Minimum));
            fields.Add(new NumberField("maximum", texts["form.property.maximum"], range.Maximum));
            fields.Add(new NumberField("step", texts["form.property.step"], range.Step, 0));
            fields.Add(new NumberField("decimals", texts["form.property.decimals"], properties.DecimalPlaces, 0, 10));
            fields.Add(new CheckField("thousands", texts["form.property.thousands"], properties.ThousandsSeparator));
            fields.Add(new TextField("currency", texts["form.property.currency"], properties.CurrencySymbol ?? string.Empty));
        }

        if (descriptor.Groups.HasFlag(FormPropertyGroup.Scroll))
        {
            fields.Add(new NumberField("page", texts["form.property.pageStep"], range.Page, 0));
            fields.Add(new ChoiceField("orientation", texts["form.property.orientation"],
                [texts["form.orientation.horizontal"], texts["form.orientation.vertical"]],
                (int)properties.Orientation));
        }

        if (descriptor.Groups.HasFlag(FormPropertyGroup.Text))
        {
            fields.Add(new NumberField("maxLength", texts["form.property.maxLength"], properties.MaxTextLength, 0, 4000));
            fields.Add(new CheckField("multiLine", texts["form.property.multiLine"], properties.MultiLine));
            fields.Add(new TextField("password", texts["form.property.passwordCharacter"],
                properties.PasswordCharacter?.ToString() ?? string.Empty));
        }

        if (descriptor.Groups.HasFlag(FormPropertyGroup.Image))
            fields.Add(new TextField("imagePath", texts["form.property.imagePath"], properties.ImagePath ?? string.Empty));

        if (descriptor.Groups.HasFlag(FormPropertyGroup.Button))
        {
            fields.Add(new ChoiceField("buttonAction", texts["form.property.buttonAction"],
                [
                    texts["form.action.none"], texts["form.action.submit"],
                    texts["form.action.reset"], texts["form.action.openUrl"],
                ],
                (int)properties.ButtonAction));

            fields.Add(new TextField("targetUrl", texts["form.property.targetUrl"], properties.TargetUrl ?? string.Empty));
            fields.Add(new CheckField("toggle", texts["form.property.toggle"], properties.Toggle));
        }

        if (descriptor.Kind == FormControlKind.CheckBox)
            fields.Add(new CheckField("triState", texts["form.property.triState"], properties.TriState));

        if (descriptor.Kind == FormControlKind.OptionButton)
            fields.Add(new TextField("group", texts["form.property.group"], properties.Group ?? string.Empty));

        return fields;
    }


    private List<FormField> AppearanceFields(FormControlProperties properties) =>
    [
        new FontField("fontFamily", texts["form.property.fontFamily"], properties.Text.Family, fonts),
        new NumberField("fontSize", texts["form.property.fontSize"], properties.Text.Size, 4, 96),
        new CheckField("bold", texts["form.property.bold"], properties.Text.Bold),
        new CheckField("italic", texts["form.property.italic"], properties.Text.Italic),
        new CheckField("underline", texts["form.property.underline"], properties.Text.Underline),
        new ColorField("textColour", texts["form.property.textColour"], properties.Text.ColorHex, AllowNone: false),
        new ChoiceField("alignment", texts["form.property.alignment"],
            [texts["form.align.left"], texts["form.align.centre"], texts["form.align.right"]],
            (int)properties.Text.Alignment),
        new ColorField("background", texts["form.property.background"], properties.BackgroundHex),
        new ColorField("borderColour", texts["form.property.borderColour"], properties.BorderHex),
        new ChoiceField("border", texts["form.property.border"],
            [texts["form.border.none"], texts["form.border.flat"], texts["form.border.raised"]],
            (int)properties.Border),
    ];

    private List<MacroEventSlot> EventSlots(FormControlDescriptor descriptor, FormControlProperties properties) =>
    [
        .. descriptor.Events.Select(raised => new MacroEventSlot(
            raised.ToString(),
            $"form.event.{Lower(raised.ToString())}",
            $"form.event.{Lower(raised.ToString())}.description",
            $"{Stem(properties.Name)}_{raised}")),
    ];

    private string Stem(string name)
    {
        var letters = new string([.. name.Where(char.IsLetterOrDigit)]);
        return letters.Length == 0 ? "Control" : letters;
    }

    private string Lower(string value) => char.ToLowerInvariant(value[0]) + value[1..];

    private FormControlProperties Merge(
        FormControlDescriptor descriptor, FormControlProperties properties, FormAnswers answers)
    {
        var range = properties.Range ?? new NumericRange(0, 100, 1, 10);

        var merged = properties with
        {
            Name = Text(answers.General, "name") is { Length: > 0 } name ? name : properties.Name,
            Label = Text(answers.General, "label") is { Length: > 0 } label ? label : null,
            Tooltip = Text(answers.General, "tooltip") is { Length: > 0 } tip ? tip : null,
            HelpText = Text(answers.General, "help") is { Length: > 0 } help ? help : null,
            Enabled = answers.General["enabled"] is true,
            Visible = answers.General["visible"] is true,
            Printable = answers.General["printable"] is true,
            ReadOnly = answers.General["readOnly"] is true,
            TabStop = answers.General["tabStop"] is true,
            TabIndex = (int)Number(answers.General, "tabIndex", properties.TabIndex),
            LinkedCell = Text(answers.Data, "linkedCell") is { Length: > 0 } cell ? cell : null,
            Text = properties.Text with
            {
                Family = Text(answers.Appearance, "fontFamily") is { Length: > 0 } family
                    ? family
                    : properties.Text.Family,
                Size = Number(answers.Appearance, "fontSize", properties.Text.Size),
                Bold = answers.Appearance["bold"] is true,
                Italic = answers.Appearance["italic"] is true,
                Underline = answers.Appearance["underline"] is true,
                ColorHex = Text(answers.Appearance, "textColour") is { Length: > 0 } ink
                    ? ink
                    : properties.Text.ColorHex,
                Alignment = (FormTextAlignment)(int)Number(
                    answers.Appearance, "alignment", (int)properties.Text.Alignment),
            },
            BackgroundHex = Text(answers.Appearance, "background") is { Length: > 0 } back ? back : null,
            BorderHex = Text(answers.Appearance, "borderColour") is { Length: > 0 } edge ? edge : null,
            Border = (FormBorderKind)(int)Number(answers.Appearance, "border", (int)properties.Border),
        };

        if (descriptor.Groups.HasFlag(FormPropertyGroup.List))
            merged = merged with
            {
                SourceRange = Text(answers.Data, "sourceRange") is { Length: > 0 } source ? source : null,
                Items = answers.Data.GetValueOrDefault("items") is IReadOnlyList<string> { Count: > 0 } items
                    ? [.. items]
                    : [],
                MultiSelect = answers.Data["multiSelect"] is true,
                BoundColumn = (int)Number(answers.Data, "boundColumn", properties.BoundColumn),
                Dropdown = answers.Data["dropdown"] is true,
                DropdownLines = (int)Number(answers.Data, "dropdownLines", properties.DropdownLines),
            };

        if (descriptor.Groups.HasFlag(FormPropertyGroup.Numeric))
            merged = merged with
            {
                Range = new NumericRange(
                    Math.Min(Number(answers.Data, "minimum", range.Minimum), Number(answers.Data, "maximum", range.Maximum)),
                    Math.Max(Number(answers.Data, "minimum", range.Minimum), Number(answers.Data, "maximum", range.Maximum)),
                    Number(answers.Data, "step", range.Step),
                    descriptor.Groups.HasFlag(FormPropertyGroup.Scroll)
                        ? Number(answers.Data, "page", range.Page)
                        : range.Page),
                DecimalPlaces = (int)Number(answers.Data, "decimals", properties.DecimalPlaces),
                ThousandsSeparator = answers.Data["thousands"] is true,
                CurrencySymbol = Text(answers.Data, "currency") is { Length: > 0 } symbol ? symbol : null,
            };

        if (descriptor.Groups.HasFlag(FormPropertyGroup.Scroll))
            merged = merged with
            {
                Orientation = (FormOrientation)(int)Number(
                    answers.Data, "orientation", (int)properties.Orientation),
            };

        if (descriptor.Groups.HasFlag(FormPropertyGroup.Text))
            merged = merged with
            {
                MaxTextLength = (int)Number(answers.Data, "maxLength", properties.MaxTextLength),
                MultiLine = answers.Data["multiLine"] is true,
                PasswordCharacter = Text(answers.Data, "password") is { Length: > 0 } password
                    ? password[0]
                    : null,
            };

        if (descriptor.Groups.HasFlag(FormPropertyGroup.Image))
            merged = merged with
            {
                ImagePath = Text(answers.Data, "imagePath") is { Length: > 0 } image ? image : null,
            };

        if (descriptor.Groups.HasFlag(FormPropertyGroup.Button))
            merged = merged with
            {
                ButtonAction = (FormButtonAction)(int)Number(
                    answers.Data, "buttonAction", (int)properties.ButtonAction),
                TargetUrl = Text(answers.Data, "targetUrl") is { Length: > 0 } url ? url : null,
                Toggle = answers.Data["toggle"] is true,
            };

        if (descriptor.Kind == FormControlKind.CheckBox)
            merged = merged with { TriState = answers.Data["triState"] is true };

        if (descriptor.Kind == FormControlKind.OptionButton)
            merged = merged with
            {
                Group = Text(answers.Data, "group") is { Length: > 0 } group ? group : null,
            };

        foreach (var raised in descriptor.Events)
            if (answers.Events.TryGetValue(raised.ToString(), out var macro))
                merged = merged.Bind(raised, macro);

        return merged;
    }

    private string? Text(IReadOnlyDictionary<string, object> values, string key) =>
        values.TryGetValue(key, out var value) ? (value as string)?.Trim() : null;

    private double Number(IReadOnlyDictionary<string, object> values, string key, double fallback) =>
        values.TryGetValue(key, out var value)
            ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
            : fallback;
}
