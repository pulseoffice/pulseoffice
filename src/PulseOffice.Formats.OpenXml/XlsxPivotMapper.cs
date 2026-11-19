using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using PivotArea = PulseOffice.Sheets.Pivot.PivotArea;
using PivotField = PulseOffice.Sheets.Pivot.PivotField;
using Workbook = PulseOffice.Sheets.Workbook;
using Worksheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxPivotMapper
{
    private readonly OccupiedCells _occupied = new();
    private readonly OoxmlTextEscape _escape = new();
    private readonly XlsxPivotExternalSources _external = new();
    private const int DataLayoutField = -2;

    private readonly IReadOnlyDictionary<PivotAggregation, DataConsolidateFunctionValues> _aggregations =
        new Dictionary<PivotAggregation, DataConsolidateFunctionValues>
        {
            [PivotAggregation.Sum] = DataConsolidateFunctionValues.Sum,
            [PivotAggregation.Count] = DataConsolidateFunctionValues.Count,
            [PivotAggregation.CountNumbers] = DataConsolidateFunctionValues.CountNumbers,
            [PivotAggregation.Average] = DataConsolidateFunctionValues.Average,
            [PivotAggregation.Max] = DataConsolidateFunctionValues.Maximum,
            [PivotAggregation.Min] = DataConsolidateFunctionValues.Minimum,
            [PivotAggregation.Product] = DataConsolidateFunctionValues.Product,
            [PivotAggregation.StandardDeviation] = DataConsolidateFunctionValues.StandardDeviation,
            [PivotAggregation.StandardDeviationPopulation] = DataConsolidateFunctionValues.StandardDeviationP,
            [PivotAggregation.Variance] = DataConsolidateFunctionValues.Variance,
            [PivotAggregation.VariancePopulation] = DataConsolidateFunctionValues.VarianceP,
        };

    private readonly IReadOnlyDictionary<ValueDisplayMode, ShowDataAsValues> _displayModes =
        new Dictionary<ValueDisplayMode, ShowDataAsValues>
        {
            [ValueDisplayMode.PercentOfTotal] = ShowDataAsValues.PercentOfTotal,
            [ValueDisplayMode.PercentOfRow] = ShowDataAsValues.PercentOfRaw,
            [ValueDisplayMode.PercentOfColumn] = ShowDataAsValues.PercentOfColumn,
            [ValueDisplayMode.RunningTotal] = ShowDataAsValues.RunTotal,
            [ValueDisplayMode.DifferenceFrom] = ShowDataAsValues.Difference,
            [ValueDisplayMode.PercentDifferenceFrom] = ShowDataAsValues.PercentageDifference,
            [ValueDisplayMode.Index] = ShowDataAsValues.Index,
        };

    public bool Describes(PivotField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.IsCalculated) return false;
        if (field.Area == PivotArea.Value)
            return _aggregations.ContainsKey(field.Aggregation)
                && (field.DisplayMode == ValueDisplayMode.Normal || _displayModes.ContainsKey(field.DisplayMode));
        return field.DateGrouping == DateGrouping.None && field.NumericGroupInterval is null;
    }

    public void Write(WorkbookPart workbookPart, Workbook workbook, IReadOnlyDictionary<Worksheet, WorksheetPart> parts)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(parts);

        var caches = new PivotCaches();
        var cacheId = 1u;

        foreach (var pivot in workbook.Pivots)
        {
            var source = pivot.IsExternal ? null : workbook.FindSheet(pivot.SourceSheet);
            if (!pivot.IsExternal && source is null) continue;
            if (workbook.FindSheet(pivot.DestinationSheet) is not { } destination) continue;
            if (!parts.TryGetValue(destination, out var destinationPart)) continue;

            var columns = source is null ? CachedColumns(pivot) : Columns(source, pivot);
            var cachePart = workbookPart.AddNewPart<PivotTableCacheDefinitionPart>();
            var saved = columns.Count > 0 && columns[0].Values.Count > 0;
            var recordsPart = saved ? cachePart.AddNewPart<PivotTableCacheRecordsPart>() : null;
            if (recordsPart is not null)
            {
                recordsPart.PivotCacheRecords = Records(columns, pivot);
                recordsPart.PivotCacheRecords.Save();
            }

            cachePart.PivotCacheDefinition = new PivotCacheDefinition(
                source is null
                    ? _external.Source(pivot, cachePart, workbookPart)
                    : new CacheSource(new WorksheetSource
                    {
                        Reference = pivot.SourceRange.ToString(),
                        Sheet = pivot.SourceSheet,
                    }) { Type = SourceValues.Worksheet },
                new CacheFields(columns.Select(CacheField)) { Count = (uint)columns.Count })
            {
                Id = recordsPart is null ? null : cachePart.GetIdOfPart(recordsPart),
                SaveData = recordsPart is null ? false : null,
                RefreshOnLoad = pivot.AutoRefreshOnOpen ? true : null,
                RecordCount = (uint)(columns.Count > 0 ? columns[0].Values.Count : 0),
                CreatedVersion = 6,
                RefreshedVersion = 6,
                MinRefreshableVersion = 3,
            };
            cachePart.PivotCacheDefinition.Save();

            var tablePart = destinationPart.AddNewPart<PivotTablePart>();
            tablePart.AddPart(cachePart);
            tablePart.PivotTableDefinition = Definition(pivot, columns, cacheId);
            tablePart.PivotTableDefinition.Save();

            caches.AppendChild(new PivotCache { CacheId = cacheId, Id = workbookPart.GetIdOfPart(cachePart) });
            cacheId++;
        }

        if (!caches.HasChildren) return;

        var book = workbookPart.Workbook!;
        OpenXmlElement anchor = (OpenXmlElement?)book.GetFirstChild<CalculationProperties>()
            ?? (OpenXmlElement?)book.GetFirstChild<DefinedNames>()
            ?? book.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.Sheets>()!;
        book.InsertAfter(caches, anchor);
    }

    private List<CacheColumn> Columns(Worksheet source, PivotDefinition pivot)
    {
        var range = _occupied.Clip(pivot.SourceRange, source);
        var columns = new List<CacheColumn>();

        for (var column = range.Start.Column; column <= range.End.Column; column++)
        {
            var index = column - range.Start.Column;
            var name = source.GetValue(new CellAddress(range.Start.Row, column)).ToString();
            if (name.Length == 0) name = "Column" + (index + 1).ToString(CultureInfo.InvariantCulture);
            var values = new List<CellValue>();
            for (var row = range.Start.Row + 1; row <= range.End.Row; row++)
                values.Add(source.GetValue(new CellAddress(row, column)));
            columns.Add(new CacheColumn(name, values));
        }

        foreach (var field in pivot.Fields.Where(candidate => !candidate.IsCalculated
                     && candidate.Area is PivotArea.Row or PivotArea.Column or PivotArea.Filter))
            if (IndexOf(field, columns) is var at and >= 0) columns[at].IsAxis = true;

        return columns;
    }

    private List<CacheColumn> CachedColumns(PivotDefinition pivot)
    {
        var cache = pivot.ExternalSource!.Cache;
        List<CacheColumn> columns;
        if (cache.Headers.Count > 0)
            columns = [.. cache.Headers.Select((header, index) => new CacheColumn(
                header.Length > 0 ? header : "Column" + (index + 1).ToString(CultureInfo.InvariantCulture),
                [.. cache.Records.Select(record => index < record.Count ? record[index] : CellValue.Empty)]))];
        else
            columns = [.. pivot.Fields.Where(field => !field.IsCalculated)
                .GroupBy(field => field.SourceName, StringComparer.Ordinal)
                .Select(group => new CacheColumn(group.Key, []))];

        foreach (var field in pivot.Fields.Where(candidate => !candidate.IsCalculated
                     && candidate.Area is PivotArea.Row or PivotArea.Column or PivotArea.Filter))
            if (IndexOf(field, columns) is var at and >= 0) columns[at].IsAxis = true;

        return columns;
    }

    private CacheField CacheField(CacheColumn column)
    {
        var numbers = column.Values.Where(value => value.Kind == ValueKind.Number).Select(value => value.AsNumber()).ToList();
        var hasText = column.Values.Any(value => value.Kind is ValueKind.Text or ValueKind.Boolean or ValueKind.Error);
        var hasBlank = column.Values.Any(value => value.IsEmpty);
        var shared = new SharedItems
        {
            ContainsSemiMixedTypes = hasText || numbers.Count == 0 ? null : false,
            ContainsString = hasText ? null : false,
            ContainsNumber = numbers.Count > 0 ? true : null,
            ContainsBlank = hasBlank ? true : null,
            ContainsMixedTypes = hasText && numbers.Count > 0 ? true : null,
            ContainsInteger = numbers.Count > 0 && numbers.All(number => number == Math.Floor(number)) ? true : null,
            MinValue = numbers.Count > 0 ? numbers.Min() : null,
            MaxValue = numbers.Count > 0 ? numbers.Max() : null,
        };

        if (column.IsAxis)
        {
            foreach (var value in column.Items) shared.AppendChild(Item(value));
            shared.Count = (uint)column.Items.Count;
        }

        return new CacheField(shared) { Name = _escape.Encode(column.Name), NumberFormatId = 0 };
    }

    private OpenXmlElement Item(CellValue value) => value.Kind switch
    {
        ValueKind.Empty => new MissingItem(),
        ValueKind.Number => new NumberItem { Val = value.AsNumber() },
        ValueKind.Boolean => new BooleanItem { Val = value.AsBoolean() },
        ValueKind.Error => new ErrorItem { Val = value.ErrorText },
        _ => new StringItem { Val = _escape.Encode(value.AsText()) },
    };

    private PivotCacheRecords Records(List<CacheColumn> columns, PivotDefinition pivot)
    {
        var records = new PivotCacheRecords();
        var count = columns.Count > 0 ? columns[0].Values.Count : 0;

        for (var row = 0; row < count; row++)
        {
            var record = new PivotCacheRecord();
            foreach (var column in columns)
                record.AppendChild(column.IsAxis
                    ? new FieldItem { Val = (uint)column.IndexOf(column.Values[row]) }
                    : Item(column.Values[row]));
            records.AppendChild(record);
        }

        records.Count = (uint)count;
        return records;
    }

    private PivotTableDefinition Definition(PivotDefinition pivot, List<CacheColumn> columns, uint cacheId)
    {
        var compact = pivot.Layout == PivotLayout.Compact;
        var outline = pivot.Layout != PivotLayout.Tabular;
        var placed = pivot.Fields.Where(field => !field.IsCalculated).ToList();
        var location = pivot.OutputRange ?? new CellRange(pivot.Destination, pivot.Destination);

        var definition = new PivotTableDefinition
        {
            Name = pivot.Name,
            CacheId = cacheId,
            ApplyNumberFormats = false,
            ApplyBorderFormats = false,
            ApplyFontFormats = false,
            ApplyPatternFormats = false,
            ApplyAlignmentFormats = false,
            ApplyWidthHeightFormats = true,
            DataCaption = "Values",
            UpdatedVersion = 6,
            MinRefreshableVersion = 3,
            CreatedVersion = 6,
            UseAutoFormatting = true,
            ItemPrintTitles = true,
            Indent = 0u,
            Compact = compact ? null : false,
            CompactData = compact ? null : false,
            Outline = outline ? true : null,
            OutlineData = outline ? true : null,
            RowGrandTotals = pivot.ShowGrandTotalColumns ? null : false,
            ColumnGrandTotals = pivot.ShowGrandTotalRows ? null : false,
            MissingCaption = pivot.EmptyCellText,
            ShowMissing = pivot.EmptyCellText is null ? null : true,
            MultipleFieldFilters = false,
        };

        definition.AppendChild(new Location
        {
            Reference = location.ToString(),
            FirstHeaderRow = 1u,
            FirstDataRow = (uint)Math.Max(pivot.In(PivotArea.Column).Count(), 1),
            FirstDataColumn = (uint)Math.Max(pivot.In(PivotArea.Row).Count(), 1),
        });

        var fields = new PivotFields { Count = (uint)columns.Count };
        for (var index = 0; index < columns.Count; index++)
            fields.AppendChild(PivotFieldOf(pivot, columns, index, compact, outline));
        definition.AppendChild(fields);

        var rows = Axis(pivot, columns, PivotArea.Row);
        var columnAxis = Axis(pivot, columns, PivotArea.Column);
        var values = pivot.In(PivotArea.Value).Where(field => !field.IsCalculated).ToList();
        if (values.Count > 1) columnAxis.Add(DataLayoutField);

        if (rows.Count > 0)
            definition.AppendChild(new RowFields(rows.Select(index => new Field { Index = index })) { Count = (uint)rows.Count });
        if (columnAxis.Count > 0)
            definition.AppendChild(new ColumnFields(columnAxis.Select(index => new Field { Index = index })) { Count = (uint)columnAxis.Count });

        var pages = Axis(pivot, columns, PivotArea.Filter);
        if (pages.Count > 0)
            definition.AppendChild(new PageFields(pages.Select(index => new PageField { Field = index, Hierarchy = -1 })) { Count = (uint)pages.Count });

        if (values.Count > 0)
            definition.AppendChild(new DataFields(values.Select(field => DataFieldOf(field, columns, pivot))) { Count = (uint)values.Count });

        definition.AppendChild(new PivotTableStyle
        {
            Name = "PivotStyleLight16",
            ShowRowHeaders = true,
            ShowColumnHeaders = true,
            ShowRowStripes = false,
            ShowColumnStripes = false,
            ShowLastColumn = true,
        });

        if (TopFilters(pivot, columns) is { } topFilters) definition.AppendChild(topFilters);

        return definition;
    }

    private DocumentFormat.OpenXml.Spreadsheet.PivotField PivotFieldOf(PivotDefinition pivot, List<CacheColumn> columns, int index, bool compact, bool outline)
    {
        var axisField = pivot.Fields.FirstOrDefault(field => !field.IsCalculated && field.Area is PivotArea.Row or PivotArea.Column or PivotArea.Filter
            && IndexOf(field, columns) == index);
        var isData = pivot.Fields.Any(field => !field.IsCalculated && field.Area == PivotArea.Value && IndexOf(field, columns) == index);

        var element = new DocumentFormat.OpenXml.Spreadsheet.PivotField
        {
            ShowAll = axisField?.ShowItemsWithNoData == true ? null : false,
            Compact = compact ? null : false,
            Outline = outline ? null : false,
            DataField = isData ? true : null,
        };

        if (axisField is null) return element;
        var column = columns[index];

        element.Axis = axisField.Area switch
        {
            PivotArea.Row => PivotTableAxisValues.AxisRow,
            PivotArea.Column => PivotTableAxisValues.AxisColumn,
            _ => PivotTableAxisValues.AxisPage,
        };
        if (axisField.DisplayName is { Length: > 0 } label) element.Name = label;
        if (axisField.SortDescending) element.SortType = FieldSortValues.Descending;
        else element.SortType = FieldSortValues.Ascending;
        if (!axisField.ShowSubtotals) element.DefaultSubtotal = false;
        if (axisField.Area == PivotArea.Filter && axisField.ExcludedValues.Count > 0) element.MultipleItemSelectionAllowed = true;

        var items = new Items();
        for (var item = 0; item < column.Items.Count; item++)
            items.AppendChild(new Item
            {
                Index = (uint)item,
                Hidden = axisField.ExcludedValues.Contains(KeyOf(column.Items[item])) ? true : null,
            });
        if (axisField.ShowSubtotals) items.AppendChild(new Item { ItemType = ItemValues.Default });
        items.Count = (uint)items.ChildElements.Count;
        element.AppendChild(items);

        if (axisField.SortByValue is { } byValue && WrittenDataIndex(pivot, byValue) is { } dataIndex)
            element.AppendChild(new AutoSortScope(new DocumentFormat.OpenXml.Spreadsheet.PivotArea(
                new PivotAreaReferences(new PivotAreaReference(new FieldItem { Val = dataIndex })
                { Field = DataReferenceField, Count = 1u, Selected = false })
                { Count = 1u })
            { DataOnly = false, Outline = false, FieldPosition = 0u }));

        if (pivot.RepeatItemLabels && axisField.Area == PivotArea.Row)
            element.AppendChild(new PivotFieldExtensionList(new PivotFieldExtension(
                new DocumentFormat.OpenXml.Office2010.Excel.PivotField { FillDownLabels = true })
            { Uri = FillDownExtension }));

        return element;
    }

    private DataField DataFieldOf(PivotField field, List<CacheColumn> columns, PivotDefinition pivot)
    {
        var index = IndexOf(field, columns);
        var data = new DataField
        {
            Name = field.DisplayName is { Length: > 0 } label ? label : DefaultCaption(field),
            Field = (uint)Math.Max(index, 0),
            Subtotal = _aggregations.TryGetValue(field.Aggregation, out var function) ? function : DataConsolidateFunctionValues.Sum,
            BaseField = 0,
            BaseItem = 0u,
        };

        if (field.DisplayMode != ValueDisplayMode.Normal && _displayModes.TryGetValue(field.DisplayMode, out var mode))
        {
            data.ShowDataAs = mode;
            var baseField = pivot.In(PivotArea.Row).Concat(pivot.In(PivotArea.Column)).FirstOrDefault();
            if (baseField is not null) data.BaseField = Math.Max(IndexOf(baseField, columns), 0);
            if (field.DisplayMode is ValueDisplayMode.DifferenceFrom or ValueDisplayMode.PercentDifferenceFrom)
                data.BaseItem = 1048828u;
        }

        return data;
    }

    private string DefaultCaption(PivotField field) => DefaultCaption(field.Aggregation, field.SourceName);

    private string DefaultCaption(PivotAggregation aggregation, string name) => (aggregation switch
    {
        PivotAggregation.Count or PivotAggregation.CountNumbers => "Count of ",
        PivotAggregation.Average => "Average of ",
        PivotAggregation.Max => "Max of ",
        PivotAggregation.Min => "Min of ",
        PivotAggregation.Product => "Product of ",
        PivotAggregation.StandardDeviation => "StdDev of ",
        PivotAggregation.StandardDeviationPopulation => "StdDevp of ",
        PivotAggregation.Variance => "Var of ",
        PivotAggregation.VariancePopulation => "Varp of ",
        _ => "Sum of ",
    }) + name;

    private const uint DataReferenceField = 4294967294u;

    private uint? WrittenDataIndex(PivotDefinition pivot, int valueIndex)
    {
        var values = pivot.In(PivotArea.Value).ToList();
        if (valueIndex < 0 || valueIndex >= values.Count || values[valueIndex].IsCalculated) return null;
        return (uint)values.Take(valueIndex).Count(field => !field.IsCalculated);
    }

    private int? ModelValueIndex(List<PivotField> fields, uint dataIndex)
    {
        var values = fields.Where(field => field.Area == PivotArea.Value).OrderBy(field => field.AreaOrder).ToList();
        return dataIndex < values.Count ? (int)dataIndex : null;
    }

    private uint? SortedByData(DocumentFormat.OpenXml.Spreadsheet.PivotField? field) =>
        field?.GetFirstChild<AutoSortScope>()?.Descendants<PivotAreaReference>()
            .Where(reference => reference.Field?.Value == DataReferenceField)
            .Select(reference => reference.Elements<FieldItem>().FirstOrDefault()?.Val?.Value)
            .FirstOrDefault();

    private PivotFilters? TopFilters(PivotDefinition pivot, List<CacheColumn> columns)
    {
        var filters = new PivotFilters();
        var id = 1u;
        foreach (var field in pivot.Fields.Where(candidate => !candidate.IsCalculated && candidate.Area is PivotArea.Row or PivotArea.Column))
        {
            if (field.TopFilter is not { Count: > 0 } top || WrittenDataIndex(pivot, top.ValueField) is not { } measure) continue;
            if (IndexOf(field, columns) is not (var at and >= 0)) continue;
            filters.AppendChild(new PivotFilter(new AutoFilter(new FilterColumn(new Top10
            {
                Val = top.Count,
                FilterValue = top.Count,
                Top = top.FromBottom ? false : null,
            }) { ColumnId = 0u }) { Reference = "A1" })
            {
                Field = (uint)at,
                Type = PivotFilterValues.Count,
                EvaluationOrder = -1,
                Id = id++,
                MeasureField = measure,
            });
        }
        if (!filters.HasChildren) return null;
        filters.Count = (uint)filters.ChildElements.Count;
        return filters;
    }

    private const string FillDownExtension = "{2946ED86-A175-432a-8AC1-64E0C546D7DE}";

    private bool FillsDown(DocumentFormat.OpenXml.Spreadsheet.PivotField? field) =>
        field?.GetFirstChild<PivotFieldExtensionList>()?.Elements<PivotFieldExtension>()
            .SelectMany(extension => extension.Elements<DocumentFormat.OpenXml.Office2010.Excel.PivotField>())
            .Any(extended => extended.FillDownLabels?.Value == true) == true;

    private List<int> Axis(PivotDefinition pivot, List<CacheColumn> columns, PivotArea area) =>
        [.. pivot.In(area).Where(field => !field.IsCalculated).Select(field => IndexOf(field, columns)).Where(index => index >= 0)];

    private int IndexOf(PivotField field, List<CacheColumn> columns)
    {
        var named = columns.FindIndex(column => string.Equals(column.Name, field.SourceName, StringComparison.Ordinal));
        if (named >= 0) return named;
        return field.SourceColumnIndex >= 0 && field.SourceColumnIndex < columns.Count ? field.SourceColumnIndex : -1;
    }

    private string KeyOf(CellValue value) => value.Kind switch
    {
        ValueKind.Empty => string.Empty,
        ValueKind.Text => value.AsText(),
        ValueKind.Boolean => value.AsBoolean() ? "VERO" : "FALSO",
        ValueKind.Error => value.ErrorText,
        _ => value.AsNumber().ToString("R", CultureInfo.InvariantCulture),
    };

    public int Read(WorkbookPart workbookPart, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(workbook);

        var sheets = workbookPart.Workbook?.Sheets?.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().ToList() ?? [];
        var sources = new XlsxPivotSourceResolver(workbookPart);
        var external = 0;

        foreach (var sheet in sheets)
        {
            if (sheet.Id?.Value is not { } id || sheet.Name?.Value is not { } sheetName) continue;
            if (!workbookPart.TryGetPartById(id, out var found) || found is not WorksheetPart part) continue;
            if (workbook.FindSheet(sheetName) is null) continue;

            foreach (var tablePart in part.PivotTableParts)
            {
                var cachePart = tablePart.PivotTableCacheDefinitionPart;
                if (cachePart?.PivotCacheDefinition?.CacheSource is { } cacheSource && sources.IsExternal(cacheSource))
                {
                    if (_external.Read(cachePart, workbookPart) is not { } read) external++;
                    else if (ReadOne(tablePart, sheetName, (read.Sheet, read.Range), read.Source) is { } outside) workbook.Pivots.Add(outside);
                }
                else if (cachePart?.PivotCacheDefinition?.CacheSource?.WorksheetSource is { } local
                    && sources.Resolve(local, sheetName) is { } resolved
                    && ReadOne(tablePart, sheetName, (resolved.Sheet, resolved.Range), null) is { } pivot) workbook.Pivots.Add(pivot);
            }
        }
        return external;
    }

    private PivotDefinition? ReadOne(PivotTablePart tablePart, string destinationSheet, (string Sheet, CellRange Range) source, PivotExternalSource? external)
    {
        var definition = tablePart.PivotTableDefinition;
        var cache = tablePart.PivotTableCacheDefinitionPart?.PivotCacheDefinition;
        if (definition is null || cache is null) return null;
        var (sourceSheet, sourceRange) = source;
        if (definition.Location?.Reference?.Value is not { } locationText || !CellRange.TryParseA1(locationText, out var location)) return null;

        var cacheFields = cache.CacheFields?.Elements<CacheField>().ToList() ?? [];
        var pivotFields = definition.PivotFields?.Elements<DocumentFormat.OpenXml.Spreadsheet.PivotField>().ToList() ?? [];
        var fields = new List<PivotField>();

        AddAxis(fields, definition.RowFields?.Elements<Field>().Select(field => field.Index?.Value ?? -1), PivotArea.Row, cacheFields, pivotFields);
        AddAxis(fields, definition.ColumnFields?.Elements<Field>().Select(field => field.Index?.Value ?? -1), PivotArea.Column, cacheFields, pivotFields);
        AddAxis(fields, definition.PageFields?.Elements<PageField>().Select(field => field.Field?.Value ?? -1), PivotArea.Filter, cacheFields, pivotFields);
        var selections = (definition.PageFields?.Elements<PageField>() ?? [])
            .Where(page => page.Item?.Value is not null && page.Field?.Value is >= 0)
            .ToDictionary(page => page.Field!.Value, page => page.Item!.Value);
        for (var at = 0; at < fields.Count; at++)
            if (fields[at].Area == PivotArea.Filter && selections.TryGetValue(fields[at].SourceColumnIndex, out var selected)
                && Selected(cacheFields[fields[at].SourceColumnIndex], fields[at].SourceColumnIndex < pivotFields.Count ? pivotFields[fields[at].SourceColumnIndex] : null, selected) is { } kept)
                fields[at] = fields[at] with { ExcludedValues = kept };
        var repeat = fields.Any(field => field.Area == PivotArea.Row && field.SourceColumnIndex < pivotFields.Count && FillsDown(pivotFields[field.SourceColumnIndex]));

        var order = 0;
        foreach (var data in definition.DataFields?.Elements<DataField>() ?? [])
        {
            var field = data.Field?.Value ?? 0;
            if (field >= (uint)cacheFields.Count) continue;
            var index = (int)field;
            var name = _escape.Decode(cacheFields[index].Name?.Value ?? string.Empty);
            var function = data.Subtotal?.Value ?? DataConsolidateFunctionValues.Sum;
            var mode = data.ShowDataAs?.Value;
            var caption = data.Name?.Value;
            var aggregation = _aggregations.FirstOrDefault(pair => pair.Value == function, new(PivotAggregation.Sum, function)).Key;
            fields.Add(new PivotField
            {
                SourceName = name,
                SourceColumnIndex = index,
                Area = PivotArea.Value,
                AreaOrder = order++,
                Aggregation = aggregation,
                DisplayMode = mode is { } shown ? _displayModes.FirstOrDefault(pair => pair.Value == shown, new(ValueDisplayMode.Normal, shown)).Key : ValueDisplayMode.Normal,
                DisplayName = caption is null || caption == DefaultCaption(aggregation, name) || caption == "Sum of " + name || caption == "Count of " + name ? null : caption,
            });
        }

        for (var at = 0; at < fields.Count; at++)
        {
            if (fields[at].Area is not (PivotArea.Row or PivotArea.Column) || fields[at].SourceColumnIndex >= pivotFields.Count) continue;
            if (SortedByData(pivotFields[fields[at].SourceColumnIndex]) is { } sortData && ModelValueIndex(fields, sortData) is { } sortIndex)
                fields[at] = fields[at] with { SortByValue = sortIndex };
        }
        foreach (var filter in definition.GetFirstChild<PivotFilters>()?.Elements<PivotFilter>() ?? [])
        {
            if (filter.Type?.Value != PivotFilterValues.Count || filter.Field?.Value is not { } filtered) continue;
            var top10 = filter.Descendants<Top10>().FirstOrDefault();
            if (top10?.Val?.Value is not { } count || count <= 0) continue;
            var measure = ModelValueIndex(fields, filter.MeasureField?.Value ?? 0u);
            var target = fields.FindIndex(field => field.Area is PivotArea.Row or PivotArea.Column && field.SourceColumnIndex == (int)filtered);
            if (measure is null || target < 0) continue;
            fields[target] = fields[target] with { TopFilter = new PivotTopFilter((int)count, measure.Value, top10.Top?.Value == false) };
        }
        if (fields.Count == 0) return null;

        var compact = definition.Compact?.Value != false;
        var outline = definition.Outline?.Value == true;

        return new PivotDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = definition.Name?.Value ?? "Pivot",
            SourceSheet = sourceSheet,
            SourceRange = sourceRange,
            ExternalSource = external,
            DestinationSheet = destinationSheet,
            Destination = location.Start,
            OutputRange = location,
            Fields = external is null ? fields : [.. fields.Select(field => Rebound(field, external.Cache.Headers))],
            Layout = compact ? PivotLayout.Compact : outline ? PivotLayout.Outline : PivotLayout.Tabular,
            ShowGrandTotalColumns = definition.RowGrandTotals?.Value != false,
            ShowGrandTotalRows = definition.ColumnGrandTotals?.Value != false,
            EmptyCellText = definition.ShowMissing?.Value != false ? definition.MissingCaption?.Value : null,
            AutoRefreshOnOpen = cache.RefreshOnLoad?.Value == true,
            RepeatItemLabels = repeat,
        };
    }

    private HashSet<string>? Selected(CacheField cacheField, DocumentFormat.OpenXml.Spreadsheet.PivotField? pivotField, uint item)
    {
        var shared = cacheField.SharedItems?.ChildElements.ToList() ?? [];
        var positions = pivotField?.Items?.Elements<Item>().Where(entry => entry.ItemType is null).Select(entry => entry.Index?.Value).ToList();
        uint? sharedIndex = positions is { Count: > 0 } && item < positions.Count ? positions[(int)item] : item;
        if (sharedIndex is not { } chosen || chosen >= shared.Count) return null;
        var keep = SharedKey(shared[(int)chosen]);
        return [.. shared.Select(SharedKey).Where(key => !string.Equals(key, keep, StringComparison.Ordinal))];
    }

    private PivotField Rebound(PivotField field, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
            if (string.Equals(headers[index], field.SourceName, StringComparison.Ordinal)) return field with { SourceColumnIndex = index };
        return field;
    }

    private void AddAxis(List<PivotField> fields, IEnumerable<int>? indices, PivotArea area,
        List<CacheField> cacheFields, List<DocumentFormat.OpenXml.Spreadsheet.PivotField> pivotFields)
    {
        if (indices is null) return;
        var order = 0;

        foreach (var index in indices)
        {
            if (index < 0 || index >= cacheFields.Count) continue;
            var cacheField = cacheFields[index];
            var pivotField = index < pivotFields.Count ? pivotFields[index] : null;
            var shared = cacheField.SharedItems?.ChildElements.ToList() ?? [];
            var excluded = pivotField?.Items?.Elements<Item>()
                .Where(item => item.Hidden?.Value == true && item.Index?.Value is { } at && at < shared.Count)
                .Select(item => SharedKey(shared[(int)item.Index!.Value]))
                .ToHashSet() ?? [];

            fields.Add(new PivotField
            {
                SourceName = _escape.Decode(cacheField.Name?.Value ?? string.Empty),
                SourceColumnIndex = index,
                Area = area,
                AreaOrder = order++,
                DisplayName = pivotField?.Name?.Value,
                ShowSubtotals = pivotField?.DefaultSubtotal?.Value != false,
                ShowItemsWithNoData = pivotField?.ShowAll?.Value != false,
                SortDescending = pivotField?.SortType?.Value == FieldSortValues.Descending,
                ExcludedValues = excluded,
            });
        }
    }

    private string SharedKey(OpenXmlElement item) => item switch
    {
        NumberItem number => KeyOf(CellValue.Number(number.Val?.Value ?? 0)),
        BooleanItem boolean => KeyOf(CellValue.Boolean(boolean.Val?.Value == true)),
        StringItem text => _escape.Decode(text.Val?.Value ?? string.Empty),
        ErrorItem error => error.Val?.Value ?? string.Empty,
        _ => string.Empty,
    };

    private sealed class CacheColumn(string name, List<CellValue> values)
    {
        public string Name => name;

        public List<CellValue> Values => values;

        public bool IsAxis { get; set; }

        public List<CellValue> Items { get; } = values.Distinct().ToList();

        private Dictionary<CellValue, int>? _positions;

        public int IndexOf(CellValue value)
        {
            _positions ??= Items.Select((item, position) => (item, position)).ToDictionary(pair => pair.item, pair => pair.position);
            return _positions.TryGetValue(value, out var position) ? position : -1;
        }
    }
}
