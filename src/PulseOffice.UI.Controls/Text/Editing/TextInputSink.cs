using Avalonia.Controls;

namespace PulseOffice.UI.Controls.Text.Editing;

public sealed class TextInputSink : TextBox, PulseOffice.UI.Controls.Services.IDocumentSurface
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    public TextInputSink()
    {
        Opacity = 0;
        IsHitTestVisible = false;
        AcceptsReturn = false;
        AcceptsTab = false;
        Width = 2;
        Height = 2;
        MinWidth = 0;
        MinHeight = 0;
        Padding = new Avalonia.Thickness(0);
        BorderThickness = new Avalonia.Thickness(0);
    }
}
