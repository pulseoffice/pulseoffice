using PulseOffice.Foundation.Pages;
using System.Text;

namespace PulseOffice.Printing;

public sealed record PageContext(
    int PageNumber, int PageCount, string DocumentName, string SectionName, DateTimeOffset PrintedAt);

public interface IHeaderFooterFields
{
    string Expand(string pattern, PageContext context);
}

public sealed class HeaderFooterFields(IFormatProvider? culture = null, IHeaderFooterCodes? codes = null) : IHeaderFooterFields
{
    private readonly IFormatProvider _culture = culture ?? System.Globalization.CultureInfo.CurrentCulture;
    private readonly IHeaderFooterCodes _codes = codes ?? new HeaderFooterCodes();

    public string Expand(string pattern, PageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = new StringBuilder(pattern?.Length ?? 0);

        foreach (var token in _codes.Tokens(pattern))
            result.Append(token.Kind switch
            {
                HeaderFooterTokenKind.Text => token.Value,
                HeaderFooterTokenKind.Field => Field(token.Value, context),
                _ => string.Empty,
            });

        return result.ToString();
    }

    private string Field(string code, PageContext context) => code switch
    {
        "P" => context.PageNumber.ToString(_culture),
        "N" => context.PageCount.ToString(_culture),
        "D" => context.PrintedAt.ToString("d", _culture),
        "T" => context.PrintedAt.ToString("t", _culture),
        "A" => context.SectionName,
        _ => context.DocumentName,
    };
}
