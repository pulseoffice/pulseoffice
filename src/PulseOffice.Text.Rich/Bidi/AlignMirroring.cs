namespace PulseOffice.Text.Rich.Bidi;

public sealed class AlignMirroring
{
    public TextAlign Mirror(TextAlign align) => align switch
    {
        TextAlign.Left => TextAlign.Right,
        TextAlign.Right => TextAlign.Left,
        _ => align,
    };

    public TextAlign Physical(TextAlign align, TextDirection? direction) => direction == TextDirection.RightToLeft ? Mirror(align) : align;
}
