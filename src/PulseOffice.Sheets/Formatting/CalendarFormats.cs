namespace PulseOffice.Sheets.Formatting;

public interface ICalendarFormats
{
    bool NamesDays(NumberFormat format);

    bool NamesDays(Workbook workbook, int styleId);
}

public sealed class CalendarFormats : ICalendarFormats
{
    public bool NamesDays(NumberFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return format.Sections.Count > 0
            && format.Sections[0].ValueKind == FormatValueKind.DateTime
            && format.Sections[0].Tokens.Any(token => !token.IsLiteral
                && token.Text.Any(letter => char.ToLowerInvariant(letter) is 'd' or 'y' or 'e' or 'g'));
    }

    public bool NamesDays(Workbook workbook, int styleId)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return NamesDays(workbook.NumberFormats.Get(workbook.Styles.Get(styleId).NumberFormatId));
    }
}
