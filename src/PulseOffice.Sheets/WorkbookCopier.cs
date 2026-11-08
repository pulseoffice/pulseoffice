namespace PulseOffice.Sheets;

public interface IWorkbookCopier
{
    Workbook Copy(Workbook source);
}

public sealed class WorkbookCopier : IWorkbookCopier
{
    public Workbook Copy(Workbook source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var copy = new Workbook
        {
            DefaultLanguage = source.DefaultLanguage,
            Protection = source.Protection,
            Calculation = source.Calculation,
            Consolidation = source.Consolidation,
            MacroSignature = source.MacroSignature,
            FormSettings = source.FormSettings,
            Fonts = source.Fonts,
        };

        source.Styles.CopyTo(copy.Styles);
        copy.NamedStyles.ReplaceAll(source.NamedStyles.All);
        source.PageStyles.CopyTo(copy.PageStyles);
        source.NumberFormats.CopyTo(copy.NumberFormats);
        copy.Properties.CopyFrom(source.Properties);
        copy.ExternalData.Restore(source.ExternalData.Save(source.DatabaseRanges), copy.DatabaseRanges);
        copy.Changes.Restore(source.Changes.Save());
        copy.Changes.IsRecording = source.Changes.IsRecording;
        copy.Changes.IsProtected = source.Changes.IsProtected;
        copy.Changes.ProtectionHash = source.Changes.ProtectionHash;
        copy.Versions.Replace(source.Versions.All);
        foreach (var macro in source.Macros.All) copy.Macros.Store(macro);
        copy.Pivots.AddRange(source.Pivots);

        foreach (var sheet in source.Sheets) CopySheet(sheet, copy.AddSheet(sheet.Name));
        foreach (var name in source.DefinedNames) copy.DefineName(name);

        return copy;
    }

    private void CopySheet(Worksheet source, Worksheet copy)
    {
        copy.IsHidden = source.IsHidden;
        copy.TabColorHex = source.TabColorHex;
        copy.RightToLeft = source.RightToLeft;
        copy.Frozen = source.Frozen;
        copy.PageStyleName = source.PageStyleName;
        copy.RepeatedPrintRows = source.RepeatedPrintRows;
        copy.RepeatedPrintColumns = source.RepeatedPrintColumns;
        copy.Protection = source.Protection;

        source.Layout.CopyTo(copy.Layout);
        source.CopyCellsTo(copy);

        copy.Merges.AddRange(source.Merges.All);
        copy.AutoFilter.Restore(source.AutoFilter.Save());
        copy.RangeFilter = source.RangeFilter;
        copy.RangeSort = source.RangeSort;
        copy.Sparklines.AddRange(source.Sparklines.Groups);
        copy.RowBreaks.UnionWith(source.RowBreaks);
        copy.ColumnBreaks.UnionWith(source.ColumnBreaks);
        foreach (var binding in source.QueryBindings.All) copy.QueryBindings.AddOrUpdate(binding);
        copy.Objects.AddRange(source.Objects);
        copy.ConditionalRules.AddRange(source.ConditionalRules);
        copy.Scenarios.AddRange(source.Scenarios);
        copy.Validations.AddRange(source.Validations);
        foreach (var (address, comment) in source.Comments) copy.Comments[address] = comment;
        copy.PrintRanges.AddRange(source.PrintRanges);
        foreach (var label in source.Labels.All) copy.Labels.Add(label);
        foreach (var (kind, macro) in source.Events.All) copy.Events.Bind(kind, macro);
        copy.TraceArrows.AddRange(source.TraceArrows);
        copy.RowGroups.AddRange(source.RowGroups);
        copy.ColumnGroups.AddRange(source.ColumnGroups);
    }
}
