using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Scripting;

namespace PulseOffice.Sheets.UI;

public sealed class SheetScriptApi : PulseOffice.Scripting.Editor.IScriptApiSection
{
    private static readonly (string Member, string Type, string Signature, string[] Parameters)[] DocMembers =
    [
        ("Name", "string", "Name: string", []),
        ("Path", "string", "Path: string", []),
        ("IsModified", "boolean", "IsModified: boolean", []),
        ("SheetCount", "number", "SheetCount: number", []),
        ("SheetNames", "string[]", "SheetNames: string[]", []),
        ("ActiveSheet", "string", "ActiveSheet: string", []),
        ("Title", "string", "Title: string", []),
        ("Author", "string", "Author: string", []),
        ("Subject", "string", "Subject: string", []),
        ("Keywords", "string", "Keywords: string", []),
        ("AddSheet", "boolean", "AddSheet(name)", ["name:name"]),
        ("RemoveSheet", "boolean", "RemoveSheet(name)", ["name:name"]),
        ("RenameSheet", "boolean", "RenameSheet(name, newName)", ["name:name", "newName:newName"]),
        ("Activate", "boolean", "Activate(name)", ["name:name"]),
        ("Undo", "boolean", "Undo()", []),
        ("Redo", "boolean", "Redo()", []),
        ("Recalculate", "void", "Recalculate()", []),
        ("Names", "string[]", "Names: string[]", []),
        ("DefineName", "boolean", "DefineName(name, reference)", ["name:name", "reference:reference"]),
        ("MacroCount", "number", "MacroCount: number", []),
    ];

    private static readonly (string Member, string Type, string Signature, string[] Parameters)[] SheetMembers =
    [
        ("Name", "string", "Name: string", []),
        ("Index", "number", "Index: number", []),
        ("Selection", "string", "Selection: string", []),
        ("UsedRange", "string", "UsedRange: string", []),
        ("ObjectCount", "number", "ObjectCount: number", []),
        ("Read", "string", "Read(reference)", ["reference:cell"]),
        ("Number", "number", "Number(reference)", ["reference:cell"]),
        ("Formula", "string", "Formula(reference)", ["reference:cell"]),
        ("IsEmpty", "boolean", "IsEmpty(reference)", ["reference:cell"]),
        ("Write", "void", "Write(reference, value)", ["reference:cell", "value:value"]),
        ("SetFormula", "void", "SetFormula(reference, formula)", ["reference:cell", "formula:formula"]),
        ("FormulaLocal", "string", "FormulaLocal(reference)", ["reference:cell"]),
        ("SetFormulaLocal", "void", "SetFormulaLocal(reference, formula)", ["reference:cell", "formula:formula"]),
        ("Range", "string[][]", "Range(reference)", ["reference:range"]),
        ("Numbers", "number[][]", "Numbers(reference)", ["reference:range"]),
        ("WriteRange", "void", "WriteRange(reference, values)", ["reference:range", "values:values"]),
        ("Clear", "void", "Clear(reference)", ["reference:range"]),
        ("Select", "void", "Select(reference)", ["reference:range"]),
        ("Recalculate", "void", "Recalculate()", []),
        ("Log", "void", "Log(message)", ["message:any"]),
        ("Bold", "void", "Bold(reference, on)", ["reference:range", "on:flag"]),
        ("Italic", "void", "Italic(reference, on)", ["reference:range", "on:flag"]),
        ("Underline", "void", "Underline(reference, on)", ["reference:range", "on:flag"]),
        ("FontSize", "void", "FontSize(reference, points)", ["reference:range", "points:points"]),
        ("FontName", "void", "FontName(reference, family)", ["reference:range", "family:family"]),
        ("Color", "void", "Color(reference, hex)", ["reference:range", "hex:hex"]),
        ("Background", "void", "Background(reference, hex)", ["reference:range", "hex:hex"]),
        ("Align", "void", "Align(reference, alignment)", ["reference:range", "alignment:alignment"]),
        ("NumberFormat", "void", "NumberFormat(reference, formatId)", ["reference:range", "formatId:formatId"]),
        ("Wrap", "void", "Wrap(reference, on)", ["reference:range", "on:flag"]),
        ("StyleName", "void", "StyleName(reference, name)", ["reference:range", "name:styleName"]),
        ("InsertRows", "void", "InsertRows(row, count)", ["row:row", "count:count"]),
        ("DeleteRows", "void", "DeleteRows(row, count)", ["row:row", "count:count"]),
        ("InsertColumns", "void", "InsertColumns(column, count)", ["column:column", "count:count"]),
        ("DeleteColumns", "void", "DeleteColumns(column, count)", ["column:column", "count:count"]),
        ("RowHeight", "void", "RowHeight(row, millimetres)", ["row:row", "millimetres:millimetres"]),
        ("ColumnWidth", "void", "ColumnWidth(column, millimetres)", ["column:column", "millimetres:millimetres"]),
        ("HideRows", "void", "HideRows(row, count)", ["row:row", "count:count"]),
        ("ShowRows", "void", "ShowRows(row, count)", ["row:row", "count:count"]),
        ("Freeze", "void", "Freeze(rows, columns)", ["rows:rows", "columns:columns"]),
        ("Merge", "boolean", "Merge(reference)", ["reference:range"]),
        ("Unmerge", "boolean", "Unmerge(reference)", ["reference:range"]),
        ("Sort", "void", "Sort(reference, column, ascending, hasHeader)",
            ["reference:range", "column:sortColumn", "ascending:ascending", "hasHeader:hasHeader"]),
        ("DistinctValues", "string[]", "DistinctValues(reference, column, hasHeader)",
            ["reference:range", "column:sortColumn", "hasHeader:hasHeader"]),
        ("Find", "string[]", "Find(text, inFormulas)", ["text:text", "inFormulas:inFormulas"]),
        ("Replace", "number", "Replace(text, replacement)", ["text:text", "replacement:replacement"]),
        ("Comment", "void", "Comment(reference, text)", ["reference:cell", "text:commentText"]),
        ("AddChart", "string", "AddChart(reference, kind, anchor)",
            ["reference:range", "kind:chartKind", "anchor:anchorCell"]),
        ("AddShape", "string", "AddShape(kind, anchor, widthMm, heightMm)",
            ["kind:shapeKind", "anchor:anchorCell", "widthMm:millimetres", "heightMm:millimetres"]),
        ("RemoveObject", "boolean", "RemoveObject(id)", ["id:objectId"]),
    ];

    private static readonly (string Member, string Type, string Signature, string[] Parameters)[] PivotMembers =
    [
        ("Names", "string[]", "Names: string[]", []),
        ("Count", "number", "Count: number", []),
        ("Create", "string", "Create(name, sourceRange, destination, rowFields, columnFields, valueFields, aggregation)",
            ["name:pivotName", "sourceRange:range", "destination:anchorCell", "rowFields:fieldNames",
             "columnFields:fieldNames", "valueFields:fieldNames", "aggregation:aggregation"]),
        ("Refresh", "number", "Refresh()", []),
        ("Remove", "boolean", "Remove(name)", ["name:pivotName"]),
    ];

    private static readonly (string Member, string Type, string Signature, string[] Parameters)[] PrintMembers =
    [
        ("Papers", "string[]", "Papers: string[]", []),
        ("Paper", "string", "Paper: string", []),
        ("Orientation", "string", "Orientation: string", []),
        ("Margins", "void", "Margins(millimetres)", ["millimetres:millimetres"]),
        ("Header", "void", "Header(left, centre, right)", ["left:bandLeft", "centre:bandCentre", "right:bandRight"]),
        ("Footer", "void", "Footer(left, centre, right)", ["left:bandLeft", "centre:bandCentre", "right:bandRight"]),
        ("FirstHeader", "void", "FirstHeader(left, centre, right)", ["left:bandLeft", "centre:bandCentre", "right:bandRight"]),
        ("FirstFooter", "void", "FirstFooter(left, centre, right)", ["left:bandLeft", "centre:bandCentre", "right:bandRight"]),
        ("EvenHeader", "void", "EvenHeader(left, centre, right)", ["left:bandLeft", "centre:bandCentre", "right:bandRight"]),
        ("EvenFooter", "void", "EvenFooter(left, centre, right)", ["left:bandLeft", "centre:bandCentre", "right:bandRight"]),
        ("DifferentFirstPage", "boolean", "DifferentFirstPage: boolean", []),
        ("DifferentOddEven", "boolean", "DifferentOddEven: boolean", []),
        ("FirstPageNumber", "number", "FirstPageNumber: number", []),
        ("GridLines", "boolean", "GridLines: boolean", []),
        ("Headings", "boolean", "Headings: boolean", []),
        ("PrintArea", "void", "PrintArea(reference)", ["reference:range"]),
        ("RepeatRows", "void", "RepeatRows(count)", ["count:repeatRows"]),
        ("PageCount", "number", "PageCount: number", []),
        ("Printers", "string[]", "Printers: string[]", []),
        ("ExportPdf", "boolean", "ExportPdf(path)", ["path:pdfPath"]),
    ];

    private static readonly (string Member, string Type, string Signature, string[] Parameters)[] RuleMembers =
    [
        ("Count", "number", "Count: number", []),
        ("Describe", "string[]", "Describe()", []),
        ("AddCondition", "boolean", "AddCondition(reference, comparison, value, backgroundHex, colorHex, bold)",
            ["reference:range", "comparison:comparison", "value:conditionValue",
             "backgroundHex:hex", "colorHex:hex", "bold:flag"]),
        ("AddDataBar", "boolean", "AddDataBar(reference, colorHex, negativeColorHex, gradient)",
            ["reference:range", "colorHex:hex", "negativeColorHex:hex", "gradient:barGradient"]),
        ("AddColorScale", "boolean", "AddColorScale(reference, colors)", ["reference:range", "colors:scaleColors"]),
        ("AddFormula", "boolean", "AddFormula(reference, formula, backgroundHex)",
            ["reference:range", "formula:ruleFormula", "backgroundHex:hex"]),
        ("Clear", "number", "Clear(reference)", ["reference:range"]),
    ];

    private static readonly (string Member, string Type, string Signature, string[] Parameters)[] ValidationMembers =
    [
        ("Count", "number", "Count: number", []),
        ("Describe", "string[]", "Describe()", []),
        ("AddList", "boolean", "AddList(reference, values, allowBlank, alert)",
            ["reference:range", "values:listValues", "allowBlank:allowBlank", "alert:alert"]),
        ("AddWholeNumber", "boolean", "AddWholeNumber(reference, minimum, maximum, alert)",
            ["reference:range", "minimum:minimum", "maximum:maximum", "alert:alert"]),
        ("AddDecimal", "boolean", "AddDecimal(reference, minimum, maximum, alert)",
            ["reference:range", "minimum:minimum", "maximum:maximum", "alert:alert"]),
        ("AddTextLength", "boolean", "AddTextLength(reference, minimum, maximum, alert)",
            ["reference:range", "minimum:minimum", "maximum:maximum", "alert:alert"]),
        ("AddDate", "boolean", "AddDate(reference, first, second, alert)",
            ["reference:range", "first:firstDate", "second:secondDate", "alert:alert"]),
        ("AddFormula", "boolean", "AddFormula(reference, formula, alert)",
            ["reference:range", "formula:ruleFormula", "alert:alert"]),
        ("Message", "boolean", "Message(reference, title, message)",
            ["reference:range", "title:inputTitle", "message:inputMessage"]),
        ("Clear", "number", "Clear(reference)", ["reference:range"]),
    ];

    private static readonly (string Member, string Type, string Signature, string[] Parameters)[] FilterMembers =
    [
        ("IsActive", "boolean", "IsActive: boolean", []),
        ("Range", "string", "Range: string", []),
        ("HiddenRows", "number", "HiddenRows: number", []),
        ("Activate", "boolean", "Activate(reference, hasHeader)", ["reference:range", "hasHeader:hasHeader"]),
        ("Deactivate", "void", "Deactivate()", []),
        ("ByValues", "number", "ByValues(column, values)", ["column:filterColumn", "values:listValues"]),
        ("ByCondition", "number", "ByCondition(column, criteria)", ["column:filterColumn", "criteria:criteria"]),
        ("ByConditions", "number", "ByConditions(column, criteria, secondCriteria, join)", ["column:filterColumn", "criteria:criteria", "secondCriteria:secondCriteria", "join:criteriaJoin"]),
        ("ClearColumn", "number", "ClearColumn(column)", ["column:filterColumn"]),
        ("ClearAll", "void", "ClearAll()", []),
        ("Values", "string[]", "Values(column)", ["column:filterColumn"]),
    ];

    private static readonly (string Member, string Type, string Signature, string[] Parameters)[] ScenarioMembers =
    [
        ("Names", "string[]", "Names: string[]", []),
        ("Count", "number", "Count: number", []),
        ("Capture", "boolean", "Capture(name, reference, comment)",
            ["name:scenarioName", "reference:range", "comment:scenarioComment"]),
        ("Apply", "boolean", "Apply(name)", ["name:scenarioName"]),
        ("Remove", "boolean", "Remove(name)", ["name:scenarioName"]),
        ("RangeOf", "string", "RangeOf(name)", ["name:scenarioName"]),
    ];

    public IEnumerable<PulseOffice.Scripting.Editor.ApiRootSpec> Roots =>
    [
        Root("doc", "Document", DocMembers),
        Root("sheet", "Sheet", SheetMembers),
        Root("pivot", "Pivot", PivotMembers),
        Root("page", "Print", PrintMembers),
        Root("rules", "Rules", RuleMembers),
        Root("validation", "Validation", ValidationMembers),
        Root("filter", "Filter", FilterMembers),
        Root("scenarios", "Scenarios", ScenarioMembers),
    ];

    private PulseOffice.Scripting.Editor.ApiRootSpec Root(
        string name, string type, IEnumerable<(string Member, string Type, string Signature, string[] Parameters)> members) =>
        new(ModuleKind.Sheets, name, type,
            [.. members.Select(member => new PulseOffice.Scripting.Editor.ApiMemberSpec(member.Member, member.Type, member.Signature, member.Parameters))]);
}
