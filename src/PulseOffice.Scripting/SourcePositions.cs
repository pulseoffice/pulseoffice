namespace PulseOffice.Scripting;

public sealed class SourcePositions
{
    public int Offset(string source, ScriptPosition position)
    {
        ArgumentNullException.ThrowIfNull(source);

        var line = 1;
        var column = 1;

        for (var index = 0; index < source.Length; index++)
        {
            if (line == position.Line && (column == position.Column || source[index] is '\n' or '\r')) return index;
            if (line > position.Line) return index;

            if (source[index] == '\n') { line++; column = 1; }
            else column++;
        }

        return source.Length;
    }
}
