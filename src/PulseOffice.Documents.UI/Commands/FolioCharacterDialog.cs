using PulseOffice.Documents.Styles;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.TextFormat;

namespace PulseOffice.Documents.UI.Commands;

public sealed record CharacterChange(CharacterProperties Set, bool ClearHighlight, bool AutomaticColor, bool DefaultLanguage = false)
{
    public bool IsEmpty => Set == CharacterProperties.None && !ClearHighlight && !AutomaticColor && !DefaultLanguage;

    public TextRun Apply(IDirectFormatting direct, TextRun run)
    {
        var changed = direct.Set(run, Set);
        if (ClearHighlight) changed = changed with { Highlight = null };
        if (AutomaticColor) changed = changed with { Color = null, ColorRole = null };
        if (DefaultLanguage) changed = changed with { Language = null };
        return changed;
    }

    public CharacterProperties Apply(CharacterProperties properties)
    {
        var changed = Set.Over(properties);
        if (ClearHighlight) changed = changed with { Highlight = null };
        if (AutomaticColor) changed = changed with { Color = null };
        if (DefaultLanguage) changed = changed with { Language = null };
        return changed;
    }
}

public interface IFolioCharacterTabs
{
    IReadOnlyList<FormTab> Tabs(TextRun current, string sample = "");

    CharacterChange Read(TextRun current, IReadOnlyDictionary<string, object> values);
}

public interface IFolioCharacterDialog
{
    Task<CharacterChange?> AskAsync(TextRun current, string sample, string titleKey, CancellationToken ct);
}

public sealed class FolioCharacterTabs(ICharacterFormatTabs shared) : IFolioCharacterTabs
{
    public IReadOnlyList<FormTab> Tabs(TextRun current, string sample = "") => shared.Tabs(Request(current, sample));

    public CharacterChange Read(TextRun current, IReadOnlyDictionary<string, object> values)
    {
        var before = State(current);
        var after = shared.Read(Request(current, string.Empty), values);
        var changes = new CharacterProperties();
        if (after.Font != before.Font && after.Font.Length > 0) changes = changes with { Font = after.Font };
        if (after.Size != before.Size) changes = changes with { FontSize = after.Size };
        if (after.Bold != before.Bold) changes = changes with { Bold = after.Bold };
        if (after.Italic != before.Italic) changes = changes with { Italic = after.Italic };
        if (!string.Equals(after.Language, before.Language, StringComparison.OrdinalIgnoreCase) && after.Language is not null) changes = changes with { Language = after.Language };
        if (after.Color is { } color && color != before.Color) changes = changes with { Color = color };
        if (after.Highlight is { } highlight && highlight != before.Highlight) changes = changes with { Highlight = highlight };
        if (after.Underline != before.Underline) changes = changes with { Underline = after.Underline == UnderlineKind.Single, DoubleUnderline = after.Underline == UnderlineKind.Double };
        if (after.Case != before.Case) changes = changes with { Case = after.Case };
        if (after.Relief != before.Relief) changes = changes with { Relief = after.Relief };
        if (after.Strikethrough != before.Strikethrough) changes = changes with { Strikethrough = after.Strikethrough };
        if (after.Overline != before.Overline) changes = changes with { Overline = after.Overline };
        if (after.SmallCaps != before.SmallCaps) changes = changes with { SmallCaps = after.SmallCaps };
        if (after.Shadow != before.Shadow) changes = changes with { Shadow = after.Shadow };
        if (after.Outline != before.Outline) changes = changes with { Outline = after.Outline };
        if (after.Hidden != before.Hidden) changes = changes with { Hidden = after.Hidden };
        if (after.Baseline != before.Baseline) changes = changes with { Baseline = after.Baseline };
        if (after.LetterSpacing != before.LetterSpacing) changes = changes with { LetterSpacing = after.LetterSpacing };
        if (after.Scale != before.Scale) changes = changes with { ScaleWidth = after.Scale };
        if (after.Kerning != before.Kerning) changes = changes with { Kerning = after.Kerning };
        return new CharacterChange(changes, after.Highlight is null && current.Highlight is not null, after.Color is null && current.Color is not null,
            after.Language is null && current.Language is not null);
    }

    private CharacterFormatRequest Request(TextRun current, string sample) => new(State(current), CharacterFeatures.All, sample);

    private CharacterFormatState State(TextRun run) => new()
    {
        Font = run.Font ?? string.Empty,
        Size = run.FontSize ?? 12,
        Bold = run.Bold,
        Italic = run.Italic,
        Underline = run.DoubleUnderline ? UnderlineKind.Double : run.Underline ? UnderlineKind.Single : UnderlineKind.None,
        Strikethrough = run.Strikethrough,
        Overline = run.Overline,
        SmallCaps = run.SmallCaps,
        Shadow = run.Shadow,
        Outline = run.Outline,
        Hidden = run.Hidden == true,
        Color = run.Color,
        Highlight = run.Highlight,
        Baseline = run.Baseline,
        LetterSpacing = run.LetterSpacing ?? 0,
        Scale = run.ScaleWidth ?? 100,
        Kerning = run.Kerning ?? true,
        Case = run.Case ?? TextCase.None,
        Relief = run.Relief ?? FontRelief.None,
        Language = run.Language,
    };
}

public sealed class FolioCharacterDialog(IFolioCharacterTabs tabs, ITabbedForms forms) : IFolioCharacterDialog
{
    private const int SampleLength = 40;

    public async Task<CharacterChange?> AskAsync(TextRun current, string sample, string titleKey, CancellationToken ct) =>
        await forms.AskAsync(titleKey, tabs.Tabs(current, Trim(sample)), ct).ConfigureAwait(true) is { } values ? tabs.Read(current, values) : null;

    private string Trim(string sample)
    {
        var line = (sample ?? string.Empty).Split('\n', 2)[0].Trim();
        return line.Length > SampleLength ? line[..SampleLength] : line;
    }
}
