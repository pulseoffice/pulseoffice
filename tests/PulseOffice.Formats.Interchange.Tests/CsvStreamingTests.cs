using System.Text;
using PulseOffice.Foundation.Text;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests;

public class CsvStreamingTests
{
    private sealed class EndlessRows : TextReader
    {
        private long _row;
        private string _pending = string.Empty;
        private int _offset;

        public long Consumed { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            var written = 0;
            while (written < count)
            {
                if (_offset == _pending.Length)
                {
                    _pending = $"{_row++},\"a\r\nb\",x\n";
                    _offset = 0;
                }
                buffer[index + written++] = _pending[_offset++];
            }
            Consumed += written;
            return written;
        }
    }

    [Fact]
    public void Rows_are_read_as_the_text_arrives_not_after_the_whole_input()
    {
        var source = new EndlessRows();

        var rows = new DelimitedText().Read(source, DelimitedFormat.Comma).Take(3).ToList();

        Assert.Equal(["2", "a\nb", "x"], rows[2]);
        Assert.True(source.Consumed < 1024 * 1024);
    }

    [Fact]
    public void A_seekable_file_whose_tail_is_not_utf8_is_still_decoded_in_its_code_page()
    {
        var head = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 5000).Select(row => $"{row},plain\n")));
        var tail = Encoding.Latin1.GetBytes("caffè,end\n");
        using var stream = new MemoryStream([.. head, .. tail]);
        var delimited = new DelimitedText();

        var result = new CsvSpreadsheetReader(delimited, new CsvValueReader(), new CsvSniffer(delimited)).Read(stream);

        Assert.True(result.Succeeded);
        Assert.Equal("caffè", result.Value!.Sheets[0][new CellAddress(5000, 0)].Value.AsText());
    }

    [Fact]
    public void Reading_leaves_the_caller_stream_open()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("1,2\n"));
        var delimited = new DelimitedText();

        new CsvSpreadsheetReader(delimited, new CsvValueReader(), new CsvSniffer(delimited), CsvOptions.Default with { AutoDetect = false }).Read(stream);

        Assert.True(stream.CanRead);
    }
}
