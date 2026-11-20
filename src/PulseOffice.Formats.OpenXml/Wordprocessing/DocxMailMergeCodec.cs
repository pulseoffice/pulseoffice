using System.Text.RegularExpressions;
using PulseOffice.Documents;
using PulseOffice.Documents.Fields;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed partial class DocxMailMergeCodec
{
    public const string ConnectionVariable = "PulseOffice.MergeConnection";

    private const string DataSourceName = "DSN=";

    public W.MailMerge? Write(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.MergeSource is not { } source) return null;
        var merge = new W.MailMerge(
            new W.MainDocumentType { Val = new W.MailMergeDocumentValues("formLetters") },
            new W.LinkToQuery(),
            new W.DataType { Val = new W.MailMergeDataValues("odbc") });
        if (source.Database.Length > 0) merge.Append(new W.ConnectString { Val = DataSourceName + source.Database });
        merge.Append(new W.Query { Val = source.Type == MergeCommandType.Table ? "SELECT * FROM `" + source.Command + "`" : source.Command });
        return merge;
    }

    public void Read(W.MailMerge? merge, string? connection, TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (merge is null) return;
        var connect = merge.GetFirstChild<W.ConnectString>()?.Val?.Value ?? string.Empty;
        var query = merge.GetFirstChild<W.Query>()?.Val?.Value ?? string.Empty;
        if (connect.Length == 0 && query.Length == 0) return;
        var table = WholeTable().Match(query);
        document.MergeSource = new MailMergeSource(Database(connect), table.Success ? table.Groups["table"].Value : query, table.Success ? MergeCommandType.Table : MergeCommandType.Command)
        {
            ConnectionId = connection is { Length: > 0 } ? connection : null,
        };
    }

    private string Database(string connect)
    {
        foreach (var part in connect.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (part.StartsWith(DataSourceName, StringComparison.OrdinalIgnoreCase)) return part[DataSourceName.Length..].Trim();
        return connect;
    }

    [GeneratedRegex(@"^\s*SELECT\s+\*\s+FROM\s+[`\[""]?(?<table>[^`\]""]+)[`\]""]?\s*;?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex WholeTable();
}
