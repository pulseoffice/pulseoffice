using Avalonia.Controls;
using PulseOffice.Document.Borders;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Borders;

public sealed record BorderEditorField(string Key, string Label, IBorderEditors Editors, BorderFrameScope Scope, BorderFrame Initial)
    : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = Editors.Create(Scope, Initial, metrics);
        return new FormFieldEditor(editor, () => editor.Frame, Labelled: false) { Observe = changed => editor.Model.Changed += (_, _) => changed() };
    }
}

public sealed class BorderEditorPrompt : UserControl, IDialogContent, IDialogFooterOwner
{
    public BorderEditorPrompt(BorderEditor editor, ITextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(texts);
        Editor = editor;
        Content = editor;
        Footer = new DialogFooter()
            .WithCancel(texts["dialog.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["dialog.confirm"], () => Completed?.Invoke(this, editor.Frame), primary: true);
    }

    public BorderEditor Editor { get; }

    public Control? Footer { get; }

    public event EventHandler<object?>? Completed;
}

public interface IBorderDialog
{
    Task<BorderFrame?> AskAsync(string titleKey, BorderFrameScope scope, BorderFrame initial, CancellationToken ct);
}

public sealed class BorderDialog(IDialogService dialogs, IBorderEditors editors, ITextCatalog texts) : IBorderDialog
{
    public Task<BorderFrame?> AskAsync(string titleKey, BorderFrameScope scope, BorderFrame initial, CancellationToken ct) =>
        dialogs.ShowAsync<BorderFrame>(
            new BorderEditorPrompt(editors.Create(scope, initial, DesignTokens.For(InterfaceDensity.Compact)), texts),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 480 },
            ct);
}
