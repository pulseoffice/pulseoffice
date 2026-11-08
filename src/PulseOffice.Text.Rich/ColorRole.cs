namespace PulseOffice.Text.Rich;

public enum ColorRole
{
    Background,
    Text,
    Background2,
    Text2,
    Accent1,
    Accent2,
    Accent3,
    Accent4,
    Accent5,
    Accent6,
    Hyperlink,
    FollowedHyperlink,
}

public interface ITextTheme
{
    uint Resolve(ColorRole role);

    string MajorFont { get; }

    string MinorFont { get; }
}
