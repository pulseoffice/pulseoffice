using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxDeletedFieldState(DocxFieldInstructions fields)
{
    private readonly System.Text.StringBuilder _instruction = new();

    private int _depth;

    private bool _inInstruction;

    private bool _locked;

    private string? _data;

    private bool _emitted;

    public DocxField? Field { get; private set; }

    public bool SkipsText => _depth > 0 && _inInstruction;

    public DocxField? Pending { get; private set; }

    public void Char(W.FieldChar fieldChar)
    {
        var type = fieldChar.FieldCharType?.Value;
        if (type == W.FieldCharValues.Begin)
        {
            _depth++;
            if (_depth > 1) return;
            _inInstruction = true;
            _instruction.Clear();
            _locked = fieldChar.FieldLock?.Value == true;
            _data = fieldChar.GetFirstChild<W.FieldData>()?.Text;
            _emitted = false;
            Field = null;
        }
        else if (type == W.FieldCharValues.Separate && _depth == 1)
        {
            Settle();
        }
        else if (type == W.FieldCharValues.End && _depth > 0)
        {
            _depth--;
            if (_depth > 0) return;
            if (_inInstruction) Settle();
            Pending = Field is not null && !_emitted ? Field : null;
            Field = null;
        }
    }

    public void Code(string text)
    {
        if (_depth == 1 && _inInstruction) _instruction.Append(text);
    }

    public TextRun Mark(TextRun run)
    {
        if (Field is not { } field) return run;
        _emitted = true;
        return run with { Field = field.Kind, FieldArgument = field.Argument, FieldDetail = field.Detail };
    }

    public DocxField? TakePending()
    {
        var pending = Pending;
        Pending = null;
        return pending;
    }

    public string Shown(DocxField field) => fields.Shown(field);

    private void Settle()
    {
        _inInstruction = false;
        var parsed = fields.WithNativeData(fields.Parse(_instruction.ToString()), _data);
        Field = parsed is not null && _locked ? parsed with { Detail = (parsed.Detail ?? new FieldDetail()) with { Fixed = true } } : parsed;
    }
}
