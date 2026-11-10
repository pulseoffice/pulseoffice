using System.Text.Json.Serialization;

namespace PulseOffice.Presentations;

public sealed record BehaviourTiming
{
    public double? Begin { get; init; }

    public double? Duration { get; init; }

    public string? Fill { get; init; }

    public double Accelerate { get; init; }

    public double Decelerate { get; init; }

    public bool AutoReverse { get; init; }

    public double? RepeatCount { get; init; }

    public bool RepeatsIndefinitely { get; init; }

    public BehaviourTiming Scaled(double factor) => this with
    {
        Begin = Begin * factor,
        Duration = Duration is { } duration && duration > InstantDuration ? duration * factor : Duration,
    };

    public const double InstantDuration = 0.0011;
}

public interface IAnimationBehaviourVisitor<out T>
{
    T Visit(GroupBehaviour behaviour);

    T Visit(SetBehaviour behaviour);

    T Visit(AnimateBehaviour behaviour);

    T Visit(ColorBehaviour behaviour);

    T Visit(TransformBehaviour behaviour);

    T Visit(MotionBehaviour behaviour);

    T Visit(FilterBehaviour behaviour);

    T Visit(CommandBehaviour behaviour);

    T Visit(PhysicsBehaviour behaviour);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(GroupBehaviour), "group")]
[JsonDerivedType(typeof(SetBehaviour), "set")]
[JsonDerivedType(typeof(AnimateBehaviour), "animate")]
[JsonDerivedType(typeof(ColorBehaviour), "color")]
[JsonDerivedType(typeof(TransformBehaviour), "transform")]
[JsonDerivedType(typeof(MotionBehaviour), "motion")]
[JsonDerivedType(typeof(FilterBehaviour), "filter")]
[JsonDerivedType(typeof(CommandBehaviour), "command")]
[JsonDerivedType(typeof(PhysicsBehaviour), "physics")]
public abstract record AnimationBehaviour
{
    public BehaviourTiming Timing { get; init; } = new();

    public string? SubItem { get; init; }

    public abstract T Accept<T>(IAnimationBehaviourVisitor<T> visitor);

    public virtual AnimationBehaviour Scaled(double factor) => this with { Timing = Timing.Scaled(factor) };

    public virtual double LongestDuration => Timing.Duration is { } duration && duration > BehaviourTiming.InstantDuration ? duration : 0;
}

public sealed record GroupBehaviour : AnimationBehaviour
{
    public bool Sequential { get; init; }

    public IReadOnlyList<AnimationBehaviour> Children { get; init; } = [];

    public override T Accept<T>(IAnimationBehaviourVisitor<T> visitor) => visitor.Visit(this);

    public override AnimationBehaviour Scaled(double factor) =>
        this with { Timing = Timing.Scaled(factor), Children = [.. Children.Select(child => child.Scaled(factor))] };

    public override double LongestDuration =>
        Math.Max(base.LongestDuration, Children.Count == 0 ? 0 : Children.Max(child => child.LongestDuration));

    public bool Equals(GroupBehaviour? other) =>
        other is not null && base.Equals(other) && Sequential == other.Sequential && Children.SequenceEqual(other.Children);

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Sequential, Children.Count);
}

public sealed record SetBehaviour : AnimationBehaviour
{
    public required string Attribute { get; init; }

    public string? To { get; init; }

    public override T Accept<T>(IAnimationBehaviourVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record AnimateBehaviour : AnimationBehaviour
{
    public required string Attribute { get; init; }

    public IReadOnlyList<string>? Values { get; init; }

    public IReadOnlyList<double>? KeyTimes { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? By { get; init; }

    public string? Formula { get; init; }

    public string? CalcMode { get; init; }

    public string? Additive { get; init; }

    public string? KeySplines { get; init; }

    public override T Accept<T>(IAnimationBehaviourVisitor<T> visitor) => visitor.Visit(this);

    public bool Equals(AnimateBehaviour? other) =>
        other is not null && base.Equals(other) && Attribute == other.Attribute
        && Same(Values, other.Values) && Same(KeyTimes, other.KeyTimes)
        && From == other.From && To == other.To && By == other.By && Formula == other.Formula
        && CalcMode == other.CalcMode && Additive == other.Additive && KeySplines == other.KeySplines;

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Attribute, From, To, By);

    private bool Same<TItem>(IReadOnlyList<TItem>? left, IReadOnlyList<TItem>? right) =>
        left is null ? right is null : right is not null && left.SequenceEqual(right);
}

public sealed record ColorBehaviour : AnimationBehaviour
{
    public required string Attribute { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? By { get; init; }

    public string ColorSpace { get; init; } = "rgb";

    public bool Clockwise { get; init; } = true;

    public override T Accept<T>(IAnimationBehaviourVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record TransformBehaviour : AnimationBehaviour
{
    public required string TransformType { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? By { get; init; }

    public override T Accept<T>(IAnimationBehaviourVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record MotionBehaviour : AnimationBehaviour
{
    public required string Path { get; init; }

    public override T Accept<T>(IAnimationBehaviourVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record FilterBehaviour : AnimationBehaviour
{
    public required string Type { get; init; }

    public string? Subtype { get; init; }

    public bool Forward { get; init; } = true;

    public bool Entering { get; init; } = true;

    public override T Accept<T>(IAnimationBehaviourVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record CommandBehaviour : AnimationBehaviour
{
    public required string Command { get; init; }

    public override T Accept<T>(IAnimationBehaviourVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record PhysicsBehaviour : AnimationBehaviour
{
    public double? Bounciness { get; init; }

    public double? VelocityX { get; init; }

    public double? VelocityY { get; init; }

    public override T Accept<T>(IAnimationBehaviourVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record AnimationBehaviours
{
    public IReadOnlyList<AnimationBehaviour> Nodes { get; init; } = [];

    public string? IterateType { get; init; }

    public double? IterateInterval { get; init; }

    public bool TextOnly { get; init; }

    public BehaviourTiming RootTiming { get; init; } = new();

    public double NaturalDuration => Nodes.Count == 0 ? 0 : Nodes.Max(node => node.LongestDuration);

    public AnimationBehaviours ScaledTo(int durationMs)
    {
        var natural = NaturalDuration;
        if (natural <= 0 || durationMs <= 0) return this;
        var factor = durationMs / 1000.0 / natural;
        return Math.Abs(factor - 1) < 1e-9
            ? this
            : this with { Nodes = [.. Nodes.Select(node => node.Scaled(factor))], IterateInterval = IterateInterval * factor };
    }

    public bool Equals(AnimationBehaviours? other) =>
        other is not null && Nodes.SequenceEqual(other.Nodes) && IterateType == other.IterateType
        && IterateInterval == other.IterateInterval && TextOnly == other.TextOnly && RootTiming == other.RootTiming;

    public override int GetHashCode() => HashCode.Combine(Nodes.Count, IterateType, IterateInterval, TextOnly);
}
