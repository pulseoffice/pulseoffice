using Avalonia.Controls.Chrome;
using Avalonia.Controls.Templates;

namespace PulseOffice.UI.Controls;

public sealed class BorderOnlyDecorationsTemplate(IWindowDrawnDecorationsTemplate inner) : IWindowDrawnDecorationsTemplate
{
    public TemplateResult<WindowDrawnDecorationsContent> Build()
    {
        var built = inner.Build();
        var underlay = built.Result.Underlay;
        built.Result.Underlay = null;
        var content = new WindowDrawnDecorationsContent { Underlay = underlay };
        return new TemplateResult<WindowDrawnDecorationsContent>(content, built.NameScope);
    }

    object Avalonia.Styling.ITemplate.Build() => Build();
}
