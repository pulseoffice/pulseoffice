using System.Diagnostics;
using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsValidationWritingPerformanceTests
{
    [Fact]
    public void Saving_many_validations_grows_linearly_with_their_number()
    {
        Save(500);

        var small = Save(1500);
        var large = Save(6000);

        Assert.True(large < small * 8, $"1500 rules took {small}, 6000 rules took {large}.");
    }

    private static TimeSpan Save(int rules)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < rules; row++)
        {
            for (var column = 0; column < 20; column++)
                sheet.SetValue(new CellAddress(row, column), CellValue.Number(row + column));
            sheet.Validations.Add(new DataValidation
            {
                Range = CellRange.Single(new CellAddress(row, 0)),
                Kind = ValidationKind.Decimal,
                Operator = ValidationOperator.Less,
                FirstFormula = (row + 10).ToString(CultureInfo.InvariantCulture),
            });
        }

        var clock = Stopwatch.StartNew();
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        clock.Stop();
        return clock.Elapsed;
    }
}
