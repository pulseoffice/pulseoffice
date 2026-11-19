using System.Globalization;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IValidationChoiceOrder
{
    IReadOnlyList<string> Arrange(IReadOnlyList<string> choices, bool sorted);
}

public sealed class ValidationChoiceOrder : IValidationChoiceOrder
{
    public IReadOnlyList<string> Arrange(IReadOnlyList<string> choices, bool sorted)
    {
        ArgumentNullException.ThrowIfNull(choices);
        if (!sorted || choices.Count < 2) return choices;
        return [.. choices
            .Select((text, index) => (Text: text, Index: index, Number: Number(text)))
            .OrderBy(choice => choice.Number is null)
            .ThenBy(choice => choice.Number ?? 0)
            .ThenBy(choice => choice.Text, StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.IgnoreCase))
            .ThenBy(choice => choice.Index)
            .Select(choice => choice.Text)];
    }

    private static double? Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var number) ? number : null;
}
