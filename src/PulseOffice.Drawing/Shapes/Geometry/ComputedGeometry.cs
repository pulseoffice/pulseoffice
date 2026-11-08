namespace PulseOffice.Drawing.Shapes.Geometry;

public interface IComputedGeometryVisitor<out T>
{
    T VisitPreset(PresetGeometry geometry);

    T VisitFormula(FormulaGeometry geometry);
}

public abstract record ComputedGeometry
{
    public double ReferenceWidth { get; init; } = 1;

    public double ReferenceHeight { get; init; } = 1;

    public abstract T Accept<T>(IComputedGeometryVisitor<T> visitor);
}

public sealed record GeometryAdjustment(string Name, double Value);

public sealed record PresetGeometry(string Name) : ComputedGeometry
{
    public IReadOnlyList<GeometryAdjustment> Adjustments { get; init; } = [];

    public override T Accept<T>(IComputedGeometryVisitor<T> visitor) => visitor.VisitPreset(this);

    public bool Equals(PresetGeometry? other) =>
        other is not null && string.Equals(Name, other.Name, StringComparison.Ordinal) && Adjustments.SequenceEqual(other.Adjustments);

    public override int GetHashCode() => HashCode.Combine(Name, Adjustments.Count);
}

public sealed record EnhancedEquation(string Name, string Formula);

public sealed record EnhancedHandle(IReadOnlyList<KeyValuePair<string, string>> Attributes)
{
    public bool Equals(EnhancedHandle? other) => other is not null && Attributes.SequenceEqual(other.Attributes);

    public override int GetHashCode() => Attributes.Count;
}

public sealed record FormulaGeometry(string Path) : ComputedGeometry
{
    public string? Type { get; init; }

    public double ViewLeft { get; init; }

    public double ViewTop { get; init; }

    public double ViewWidth { get; init; } = 21600;

    public double ViewHeight { get; init; } = 21600;

    public IReadOnlyList<double> Modifiers { get; init; } = [];

    public IReadOnlyList<EnhancedEquation> Equations { get; init; } = [];

    public IReadOnlyList<EnhancedHandle> Handles { get; init; } = [];

    public string? TextAreas { get; init; }

    public override T Accept<T>(IComputedGeometryVisitor<T> visitor) => visitor.VisitFormula(this);

    public bool Equals(FormulaGeometry? other) =>
        other is not null && Path == other.Path && Type == other.Type && TextAreas == other.TextAreas
        && ViewLeft.Equals(other.ViewLeft) && ViewTop.Equals(other.ViewTop) && ViewWidth.Equals(other.ViewWidth) && ViewHeight.Equals(other.ViewHeight)
        && Modifiers.SequenceEqual(other.Modifiers) && Equations.SequenceEqual(other.Equations) && Handles.SequenceEqual(other.Handles);

    public override int GetHashCode() => HashCode.Combine(Path, Type, Equations.Count);
}
