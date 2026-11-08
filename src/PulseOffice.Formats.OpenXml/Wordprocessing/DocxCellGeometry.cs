using System.Globalization;
using PulseOffice.Documents.Blocks;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxCellGeometry
{
    private readonly string[] _topToBottom = ["tbRl", "tbRlV", "tbLrV", "tb", "rl", "tbV", "rlV"];

    private readonly string[] _bottomToTop = ["btLr", "lr"];

    private readonly double _largestPadding = 1584;

    public W.TableCellMargin? Margins(TableCell cell)
    {
        if (cell.Padding is not { } padding) return null;
        return new W.TableCellMargin(
            new W.TopMargin { Width = Twips(padding.Top), Type = W.TableWidthUnitValues.Dxa },
            new W.LeftMargin { Width = Twips(padding.Left), Type = W.TableWidthUnitValues.Dxa },
            new W.BottomMargin { Width = Twips(padding.Bottom), Type = W.TableWidthUnitValues.Dxa },
            new W.RightMargin { Width = Twips(padding.Right), Type = W.TableWidthUnitValues.Dxa });
    }

    public W.TextDirection? Direction(TableCell cell) => cell.TextDirection switch
    {
        CellTextDirection.TopToBottom => new W.TextDirection { Val = new W.TextDirectionValues("tbRl") },
        CellTextDirection.BottomToTop => new W.TextDirection { Val = new W.TextDirectionValues("btLr") },
        _ => null,
    };

    public CellPadding? Padding(W.TableCellMargin? margins, double left, double vertical)
    {
        if (margins is null) return null;
        var top = Points(margins.TopMargin?.Width?.Value) ?? vertical;
        var bottom = Points(margins.BottomMargin?.Width?.Value) ?? vertical;
        var start = Points(margins.LeftMargin?.Width?.Value) ?? Points(margins.StartMargin?.Width?.Value) ?? left;
        var end = Points(margins.RightMargin?.Width?.Value) ?? Points(margins.EndMargin?.Width?.Value) ?? left;
        return new CellPadding(top, bottom, start, end);
    }

    public CellTextDirection Direction(W.TextDirection? direction)
    {
        var value = direction?.Val?.InnerText;
        if (value is null) return CellTextDirection.Horizontal;
        if (_topToBottom.Contains(value, StringComparer.Ordinal)) return CellTextDirection.TopToBottom;
        return _bottomToTop.Contains(value, StringComparer.Ordinal) ? CellTextDirection.BottomToTop : CellTextDirection.Horizontal;
    }

    private string Twips(double points) =>
        ((int)Math.Round(Math.Clamp(double.IsFinite(points) ? points : 0, 0, _largestPadding / 20) * 20)).ToString(CultureInfo.InvariantCulture);

    private double? Points(string? twips) =>
        int.TryParse(twips, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, 0, _largestPadding) / 20.0 : null;
}
