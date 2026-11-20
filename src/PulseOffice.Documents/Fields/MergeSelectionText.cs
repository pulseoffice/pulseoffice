using System.Text.Json;
using System.Text.Json.Nodes;

namespace PulseOffice.Documents.Fields;

public interface IMergeSelectionText
{
    string Write(MergeSelection selection);

    MergeSelection Read(string? text);
}

public sealed class MergeSelectionText : IMergeSelectionText
{
    public string Write(MergeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var conditions = new JsonArray();
        foreach (var condition in selection.Filter.Conditions.Where(condition => condition.Column.Length > 0))
            conditions.Add(new JsonObject
            {
                ["join"] = condition.Join.ToString(),
                ["column"] = condition.Column,
                ["operator"] = condition.Operator.ToString(),
                ["value"] = condition.Value,
            });
        var sort = new JsonArray();
        foreach (var key in selection.Sort.Where(key => key.Column.Length > 0))
            sort.Add(new JsonObject { ["column"] = key.Column, ["descending"] = key.Descending });
        var excluded = new JsonArray();
        foreach (var row in selection.Excluded.Order()) excluded.Add(row);
        var records = new JsonArray();
        foreach (var record in selection.ExcludedRecords.Order(StringComparer.Ordinal)) records.Add(record);
        return new JsonObject { ["filter"] = conditions, ["sort"] = sort, ["excluded"] = excluded, ["excludedRecords"] = records, ["keyColumn"] = selection.KeyColumn }.ToJsonString();
    }

    public MergeSelection Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return MergeSelection.All;
        try
        {
            if (JsonNode.Parse(text) is not JsonObject root) return MergeSelection.All;
            var conditions = new List<MergeCondition>();
            foreach (var item in (root["filter"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var column = Text(item, "column");
                if (column.Length == 0 || !Enum.TryParse<MergeFilterOperator>(Text(item, "operator"), out var op)) continue;
                var join = Enum.TryParse<MergeJoin>(Text(item, "join"), out var parsed) ? parsed : MergeJoin.And;
                conditions.Add(new MergeCondition(column, op, Text(item, "value")) { Join = join });
            }
            var sort = (root["sort"] as JsonArray ?? []).OfType<JsonObject>()
                .Where(item => Text(item, "column").Length > 0)
                .Select(item => new MergeSort(Text(item, "column"), item["descending"] is JsonValue flag && flag.TryGetValue<bool>(out var descending) && descending))
                .ToList();
            var excluded = new HashSet<int>();
            foreach (var item in (root["excluded"] as JsonArray ?? []).OfType<JsonValue>())
                if (item.TryGetValue<int>(out var row) && row >= 0) excluded.Add(row);
            var records = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in (root["excludedRecords"] as JsonArray ?? []).OfType<JsonValue>())
                if (item.TryGetValue<string>(out var record) && record.Length > 0) records.Add(record);
            return new MergeSelection(new MergeFilter(conditions), sort, excluded) { ExcludedRecords = records, KeyColumn = Text(root, "keyColumn") };
        }
        catch (JsonException)
        {
            return MergeSelection.All;
        }
    }

    private string Text(JsonObject item, string name) =>
        item[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;
}
