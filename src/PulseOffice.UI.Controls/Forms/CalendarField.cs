using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Forms;

public sealed record CalendarField(string Key, string Label, DateTime? Initial) : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var today = DateTime.Today;
        var calendar = new Calendar
        {
            SelectionMode = CalendarSelectionMode.SingleDate,
            SelectedDate = Initial ?? today,
            DisplayDate = Initial ?? today,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsTodayHighlighted = true,
            FontSize = metrics.FontSize,
        };
        calendar.Background = Brushes.Transparent;
        calendar.BorderBrush = Brushes.Transparent;
        calendar.Styles.Add(new Style(selector => selector.OfType<CalendarItem>())
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent), new Setter(TemplatedControl.BorderBrushProperty, Brushes.Transparent) },
        });
        foreach (var pseudo in new[] { string.Empty, ":inactive", ":blackout" })
            calendar.Styles.Add(new Style(selector => pseudo.Length == 0 ? selector.OfType<CalendarDayButton>() : selector.OfType<CalendarDayButton>().Class(pseudo))
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                    new Setter(TemplatedControl.FontSizeProperty, metrics.FontSize),
                },
            });
        return new FormFieldEditor(calendar, () => calendar.SelectedDate ?? Initial ?? today, Labelled: false);
    }
}
