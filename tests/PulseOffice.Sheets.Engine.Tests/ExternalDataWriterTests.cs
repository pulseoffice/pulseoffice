using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class ExternalDataWriterTests
{
    private sealed class InvariantValues : IExternalCellValues
    {
        public CellValue Parse(string text) =>
            text.Length > 1 && text[0] == '0' ? CellValue.Text(text)
            : double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) ? CellValue.Number(number)
            : text.Length == 0 ? CellValue.Empty : CellValue.Text(text);
    }

    private readonly ExternalDataWriter _writer = new(new InvariantValues());

    [Fact]
    public void LaScritturaPulisceL_AreaPrecedenteEConverteINumeri()
    {
        var sheet = Workbook.CreateDefault().Sheets[0];
        var anchor = new CellAddress(1, 1);
        var first = _writer.Write(sheet, anchor, [["a", "b"], ["1.5", "007"], ["x", "y"]], null);
        var second = _writer.Write(sheet, anchor, [["a"]], first);

        Assert.Equal(new CellRange(anchor, anchor), second);
        Assert.True(sheet.GetValue(new CellAddress(2, 1)).IsEmpty);
        var fresh = Workbook.CreateDefault().Sheets[0];
        _writer.Write(fresh, anchor, [["1.5", "007"]], null);
        Assert.Equal(1.5, fresh.GetValue(anchor).AsNumber());
        Assert.Equal(ValueKind.Text, fresh.GetValue(new CellAddress(1, 2)).Kind);
    }

    [Fact]
    public void LoStreamMostraLeRighePiuRecenti()
    {
        var sheet = Workbook.CreateDefault().Sheets[0];
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(1, 0));

        _writer.WriteStream(sheet, range, [["1"], ["2"], ["3"]], StreamInsertion.Bottom);
        Assert.Equal(2, sheet.GetValue(new CellAddress(0, 0)).AsNumber());
        Assert.Equal(3, sheet.GetValue(new CellAddress(1, 0)).AsNumber());

        _writer.WriteStream(sheet, range, [["1"], ["2"], ["3"]], StreamInsertion.Top);
        Assert.Equal(3, sheet.GetValue(new CellAddress(0, 0)).AsNumber());
    }
}
