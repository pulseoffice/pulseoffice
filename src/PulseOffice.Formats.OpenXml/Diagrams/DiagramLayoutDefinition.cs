using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml.Diagrams;

internal abstract record DiagramAtom;

internal sealed record DiagramLayoutNodeAtom(string Name, string? StyleLabel, string ChildOrder, IReadOnlyList<DiagramAtom> Children) : DiagramAtom;

internal sealed record DiagramAlgorithmAtom(string Type, IReadOnlyDictionary<string, string> Parameters) : DiagramAtom
{
    public string Parameter(string name, string fallback) => Parameters.GetValueOrDefault(name, fallback);

    public double Number(string name, double fallback) =>
        double.TryParse(Parameters.GetValueOrDefault(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}

internal sealed record DiagramShapeAtom(string Type, bool HideGeometry, double Rotation, IReadOnlyList<(string Name, double Value)> Adjustments) : DiagramAtom;

internal sealed record DiagramPresOfAtom(IReadOnlyList<string> Axes, IReadOnlyList<string> PointTypes) : DiagramAtom;

internal sealed record DiagramConstraint(
    string Type, string For, string ForName, string PointType, string ReferenceType, string ReferenceFor, string ReferenceForName,
    string ReferencePointType, string Operator, double Value, double Factor);

internal sealed record DiagramConstraintsAtom(IReadOnlyList<DiagramConstraint> Constraints) : DiagramAtom;

internal sealed record DiagramRule(string Type, string For, string ForName, double Value);

internal sealed record DiagramRulesAtom(IReadOnlyList<DiagramRule> Rules) : DiagramAtom;

internal sealed record DiagramVariablesAtom(IReadOnlyDictionary<string, string> Variables) : DiagramAtom;

internal sealed record DiagramForEachAtom(
    string Name, string? Reference, IReadOnlyList<string> Axes, IReadOnlyList<string> PointTypes, int Count, int Start, int Step,
    bool HideLastTransition, IReadOnlyList<DiagramAtom> Children) : DiagramAtom;

internal sealed record DiagramConditionAtom(
    string Function, string Argument, string Operator, string Value, IReadOnlyList<string> Axes, IReadOnlyList<string> PointTypes,
    IReadOnlyList<DiagramAtom> Children) : DiagramAtom;

internal sealed record DiagramChooseAtom(IReadOnlyList<DiagramConditionAtom> Branches, IReadOnlyList<DiagramAtom> Otherwise) : DiagramAtom;

internal sealed class DiagramLayoutDefinitionReader
{
    private static readonly XNamespace Diagram = "http://schemas.openxmlformats.org/drawingml/2006/diagram";

    public DiagramLayoutNodeAtom? Read(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Root?.Element(Diagram + "layoutNode") is { } root ? Node(root) : null;
    }

    private DiagramLayoutNodeAtom Node(XElement element) =>
        new((string?)element.Attribute("name") ?? string.Empty, (string?)element.Attribute("styleLbl"),
            (string?)element.Attribute("chOrder") ?? "b", Atoms(element));

    private List<DiagramAtom> Atoms(XElement container)
    {
        var atoms = new List<DiagramAtom>();
        foreach (var element in container.Elements())
        {
            if (element.Name.Namespace != Diagram) continue;
            DiagramAtom? atom = element.Name.LocalName switch
            {
                "layoutNode" => Node(element),
                "alg" => new DiagramAlgorithmAtom((string?)element.Attribute("type") ?? "composite",
                    element.Elements(Diagram + "param").Where(parameter => parameter.Attribute("type") is not null)
                        .GroupBy(parameter => (string)parameter.Attribute("type")!)
                        .ToDictionary(group => group.Key, group => (string?)group.First().Attribute("val") ?? string.Empty)),
                "shape" => new DiagramShapeAtom((string?)element.Attribute("type") ?? string.Empty, (string?)element.Attribute("hideGeom") is "1" or "true",
                    Number(element.Attribute("rot"), 0),
                    [.. (element.Element(Diagram + "adjLst")?.Elements(Diagram + "adj") ?? []).Select(adjust => ((string?)adjust.Attribute("idx") ?? "1", Number(adjust.Attribute("val"), 0)))]),
                "presOf" => new DiagramPresOfAtom(List(element.Attribute("axis")), List(element.Attribute("ptType"))),
                "constrLst" => new DiagramConstraintsAtom([.. element.Elements(Diagram + "constr").Select(Constraint)]),
                "ruleLst" => new DiagramRulesAtom([.. element.Elements(Diagram + "rule").Select(rule => new DiagramRule(
                    (string?)rule.Attribute("type") ?? string.Empty, (string?)rule.Attribute("for") ?? "self", (string?)rule.Attribute("forName") ?? string.Empty,
                    Number(rule.Attribute("val"), double.NaN)))]),
                "varLst" => new DiagramVariablesAtom(element.Elements().ToDictionary(variable => variable.Name.LocalName, variable => (string?)variable.Attribute("val") ?? string.Empty)),
                "forEach" => new DiagramForEachAtom((string?)element.Attribute("name") ?? string.Empty, (string?)element.Attribute("ref"),
                    List(element.Attribute("axis")), List(element.Attribute("ptType")), (int)Number(element.Attribute("cnt"), 0), (int)Number(element.Attribute("st"), 1),
                    (int)Number(element.Attribute("step"), 1), (string?)element.Attribute("hideLastTrans") is not ("0" or "false"), Atoms(element)),
                "choose" => new DiagramChooseAtom(
                    [.. element.Elements(Diagram + "if").Select(branch => new DiagramConditionAtom((string?)branch.Attribute("func") ?? string.Empty,
                        (string?)branch.Attribute("arg") ?? string.Empty, (string?)branch.Attribute("op") ?? "equ", (string?)branch.Attribute("val") ?? string.Empty,
                        List(branch.Attribute("axis")), List(branch.Attribute("ptType")), Atoms(branch)))],
                    element.Element(Diagram + "else") is { } otherwise ? Atoms(otherwise) : []),
                _ => null,
            };
            if (atom is not null) atoms.Add(atom);
        }
        return atoms;
    }

    private DiagramConstraint Constraint(XElement element) => new(
        (string?)element.Attribute("type") ?? string.Empty,
        (string?)element.Attribute("for") ?? "self",
        (string?)element.Attribute("forName") ?? string.Empty,
        (string?)element.Attribute("ptType") ?? "all",
        (string?)element.Attribute("refType") ?? "none",
        (string?)element.Attribute("refFor") ?? "self",
        (string?)element.Attribute("refForName") ?? string.Empty,
        (string?)element.Attribute("refPtType") ?? "all",
        (string?)element.Attribute("op") ?? "none",
        Number(element.Attribute("val"), double.NaN),
        Number(element.Attribute("fact"), 1));

    private IReadOnlyList<string> List(XAttribute? attribute) =>
        ((string?)attribute ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private double Number(XAttribute? attribute, double fallback) =>
        (string?)attribute is "INF" ? double.PositiveInfinity
        : double.TryParse((string?)attribute, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
