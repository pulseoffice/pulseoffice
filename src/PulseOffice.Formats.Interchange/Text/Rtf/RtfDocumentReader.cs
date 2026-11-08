using CellVerticalAlign = PulseOffice.Documents.Blocks.CellVerticalAlign;
using System.Globalization;
using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Interchange.Text.Assembly;
using PulseOffice.Formats.Interchange.Text.Plain;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Rtf;

public sealed class RtfDocumentReader(
    IRtfTokenizer tokenizer,
    ICodePages codePages,
    IRtfVocabulary vocabulary,
    IDirectFormatting direct,
    IStyleResolver resolver,
    ITextDocumentFactory factory,
    ImageHeader header,
    PulseOffice.Documents.Editing.IListThreads threads) : ITextDocumentReader
{
    private const long MaximumBytes = 256L * 1024 * 1024;

    public TextDocumentFormat Format => TextInterchangeFormats.Rtf;

    public bool CanRead(Stream stream)
    {
        var buffer = new byte[16];
        var read = stream.Read(buffer, 0, buffer.Length);
        var head = Encoding.ASCII.GetString(buffer, 0, read).TrimStart();
        return head.StartsWith("{\\rtf", StringComparison.Ordinal);
    }

    public OperationResult<TextDocument> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > MaximumBytes)
                    return OperationResult<TextDocument>.Fail(Diagnostic.Error("RTF102", "The RTF file is too large to open."));
                buffer.Write(chunk, 0, read);
            }

            var data = buffer.ToArray();
            var start = 0;
            while (start < data.Length && data[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0xEF or 0xBB or 0xBF) start++;
            if (data.Length - start < 5 || Encoding.ASCII.GetString(data, start, 5) != "{\\rtf")
                return OperationResult<TextDocument>.Fail(Diagnostic.Error("RTF101", "The file is not an RTF document."));

            var document = factory.Create();
            var session = new RtfReadSession(new TextDocumentAssembler(document, direct), codePages, vocabulary, resolver, header);
            foreach (var token in tokenizer.Tokenize(data[start..])) session.Accept(token);
            session.Finish();
            threads.Settle(document);
            return OperationResult<TextDocument>.Ok(document, session.Diagnostics);
        }
        catch (IOException exception)
        {
            return OperationResult<TextDocument>.Fail(Diagnostic.Error("RTF100", $"Reading the RTF file failed: {exception.Message}").With(exception.Message));
        }
    }
}

internal sealed class RtfReadSession(TextDocumentAssembler assembler, ICodePages codePages, IRtfVocabulary vocabulary, IStyleResolver resolver, ImageHeader header)
{
    private const int MaximumDepth = 1024;
    private const int MaximumPictureBytes = 64 * 1024 * 1024;
    private const int MaximumTwips = 31680;
    private const int MaximumLists = 4096;
    private const int DefaultRowGap = 108;
    private const int MaximumRowGap = 2880;
    private const int MaximumCellDefinitions = 4096;

    private readonly Stack<(RtfGroupState State, int Marker)> _stack = new();
    private readonly Dictionary<int, RtfFont> _fonts = [];
    private readonly List<uint?> _colors = [];
    private readonly Dictionary<int, string> _paragraphStyles = [];
    private readonly Dictionary<int, string> _characterStyles = [];
    private readonly Dictionary<int, List<NumberFormat>> _lists = [];
    private readonly Dictionary<int, int> _overrides = [];
    private readonly Dictionary<int, List<int>> _listStarts = [];
    private readonly Dictionary<int, (int Level, int Start)> _overrideStarts = [];
    private readonly List<RtfCellDefinition> _cells = [];
    private readonly List<byte> _bytes = [];
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly StringBuilder _entry = new();

    private RtfGroupState _state = new();
    private int _depth;
    private long _overflow;
    private int _skip;
    private bool _star;
    private Encoding _ansi = System.Text.Encoding.Latin1;
    private int _defaultFont;
    private int _entryIndex = -1;
    private int _red;
    private int _green;
    private int _blue;
    private List<NumberFormat>? _listLevels;
    private int _listId;
    private List<int>? _levelStarts;
    private int _overrideLevel = -1;
    private (int Level, int Start)? _overrideStart;
    private RtfPicture? _picture;
    private int _pictureDepth = -1;
    private int _entryDepth = -1;
    private int _entryParent = -1;
    private int _shapeDepth = -1;
    private int[] _shape = new int[4];
    private bool _fitToText = true;
    private string? _shapeProperty;
    private bool _pageBreak;
    private bool _tableOpen;
    private bool _rowOpen;
    private bool _cellOpen;
    private bool _cellDiscard;
    private int _cellIndex;
    private int _notes;
    private bool _skippedObjects;
    private List<int>? _firstRowWidths;
    private int _headerRows;
    private bool _headerCounting = true;
    private bool _rowIsHeader;
    private bool _ansiSet;

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public void Accept(RtfToken token)
    {
        if (Overflowing(token.Kind)) return;
        if (token.Kind is not (RtfTokenKind.Text or RtfTokenKind.HexByte)) FlushBytes();
        switch (token.Kind)
        {
            case RtfTokenKind.GroupStart:
                _depth++;
                _stack.Push((_state, _depth));
                _state = _state.Copy();
                if (_state.Destination is RtfDestination.FontTable or RtfDestination.StyleSheet && _depth == _entryParent + 1)
                {
                    _entryDepth = _depth;
                    _entryIndex = _state.Destination == RtfDestination.StyleSheet ? 0 : -1;
                    _entryIsCharacter = false;
                    _entry.Clear();
                }
                _star = false;
                break;
            case RtfTokenKind.GroupEnd:
                _skip = 0;
                EndGroup();
                break;
            case RtfTokenKind.ControlWord:
                if (_skip > 0)
                {
                    _skip--;
                    if (token.Word != "bin") break;
                }
                Control(token.Word, token.Parameter);
                _star = false;
                break;
            case RtfTokenKind.ControlSymbol:
                Symbol(token.Symbol);
                _star = token.Symbol == '*';
                break;
            case RtfTokenKind.HexByte:
                if (_skip > 0)
                {
                    _skip--;
                    break;
                }
                if (_state.Destination == RtfDestination.Picture) _picture?.Append(token.Bytes!);
                else _bytes.Add(token.Bytes![0]);
                break;
            case RtfTokenKind.Text:
                Text(token.Bytes!);
                break;
            case RtfTokenKind.Binary:
                if (_state.Destination == RtfDestination.Picture) _picture?.Append(token.Bytes!);
                break;
        }
    }

    private bool Overflowing(RtfTokenKind kind)
    {
        if (kind == RtfTokenKind.GroupStart && _depth >= MaximumDepth)
        {
            _overflow++;
            return true;
        }
        if (_overflow == 0) return false;
        if (kind == RtfTokenKind.GroupEnd) _overflow--;
        return true;
    }

    public void Finish()
    {
        FlushBytes();
        while (_stack.Count > 0) EndGroup();
        CloseTable();
        assembler.Close(Paragraph());
        if (assembler.TablesTruncated)
            _diagnostics.Add(Diagnostic.Warning("ODF258", "A table was larger than 1024 columns or one million cells: the rows and columns beyond the limit were dropped."));
        if (_skippedObjects)
            _diagnostics.Add(Diagnostic.Info("RTF103", "Embedded objects and pictures in formats other than PNG and JPEG were skipped; their text was kept."));
    }

    private void EndGroup()
    {
        FlushBytes();
        CommitEntry(_depth);
        if (_pictureDepth == _depth)
        {
            FinishPicture();
            _pictureDepth = -1;
        }

        var closing = _state;
        if (_shapeDepth == _depth) _shapeDepth = -1;
        if (_stack.Count == 0) return;
        var (parent, _) = _stack.Pop();
        _depth--;

        if (closing.Destination == RtfDestination.InfoText && closing.InfoKey is { } key)
        {
            var value = _entry.ToString().Trim();
            _entry.Clear();
            if (key == "sn") _shapeProperty = value;
            else if (key == "sv" && _shapeProperty == "fFitShapeToText") _fitToText = value != "0";
            if (value.Length > 0)
            {
                if (key == "title") assembler.Document.Properties.Title = value;
                else if (key == "author") assembler.Document.Properties.Author = value;
                else if (key == "subject") assembler.Document.Properties.Subject = value;
            }
        }

        if (closing.Destination == RtfDestination.FieldInstruction && closing.Field is { } field) field.Instruction.Append(' ');
        if (closing.Destination == RtfDestination.BookmarkStart) OpenBookmark();
        if (closing.Destination == RtfDestination.BookmarkEnd) CloseBookmark();

        if (closing.Footnote && _notes > 0)
        {
            assembler.Leave(Paragraph(closing));
            _notes--;
        }
        _state = parent;
    }

    private void FlushBytes()
    {
        if (_bytes.Count == 0) return;
        var encoding = TextEncoding();
        var text = encoding.GetString([.. _bytes]);
        _bytes.Clear();
        Emit(text, true);
    }

    private Encoding TextEncoding() =>
        _state.Character.Font is { } font && _fonts.TryGetValue(font, out var found) && found.CodePage > 0 ? codePages.Get(found.CodePage) : _ansi;

    private void Text(byte[] bytes)
    {
        var start = 0;
        if (_skip > 0)
        {
            var skipped = Math.Min(_skip, bytes.Length);
            _skip -= skipped;
            start = skipped;
        }
        if (start >= bytes.Length) return;
        if (_state.Destination == RtfDestination.Picture)
        {
            _picture?.AppendHex(bytes.AsSpan(start));
            return;
        }
        if (_state.Destination is RtfDestination.Body or RtfDestination.FieldResult && !_state.Character.Hidden)
        {
            for (var i = start; i < bytes.Length; i++) _bytes.Add(bytes[i]);
            return;
        }
        FlushBytes();
        Emit(_ansi.GetString(bytes, start, bytes.Length - start), false);
    }

    private void Emit(string text) => Emit(text, false);

    private void Emit(string text, bool escaped)
    {
        switch (_state.Destination)
        {
            case RtfDestination.Body or RtfDestination.FieldResult:
                if (_state.Character.Hidden || text.Length == 0) return;
                Content();
                assembler.Write(text, Run());
                break;
            case RtfDestination.FontTable or RtfDestination.StyleSheet:
                foreach (var character in text)
                {
                    if (character == ';' && !escaped) CommitEntry(-1);
                    else _entry.Append(character);
                }
                break;
            case RtfDestination.ColorTable:
                foreach (var character in text.Where(c => c == ';'))
                {
                    _colors.Add(_red + _green + _blue < 0 ? null : 0xFF000000u | (uint)(Math.Clamp(_red, 0, 255) << 16) | (uint)(Math.Clamp(_green, 0, 255) << 8) | (uint)Math.Clamp(_blue, 0, 255));
                    _red = _green = _blue = -1000;
                }
                break;
            case RtfDestination.InfoText or RtfDestination.BookmarkStart or RtfDestination.BookmarkEnd:
                if (_entry.Length < 4096) _entry.Append(text);
                break;
            case RtfDestination.FieldInstruction:
                if (_state.Field is { } field && field.Instruction.Length < 4096) field.Instruction.Append(text);
                break;
        }
    }

    private void CommitEntry(int depth)
    {
        if (depth >= 0 && depth != _entryDepth) return;
        if (_state.Destination == RtfDestination.FontTable && _entryIndex >= 0)
        {
            var name = _entry.ToString().Trim();
            if (name.Length > 0 && _fonts.Count < 4096)
                _fonts[_entryIndex] = new RtfFont(name, _fonts.TryGetValue(_entryIndex, out var existing) ? existing.CodePage : 0);
        }
        else if (_state.Destination == RtfDestination.StyleSheet && _entryIndex >= 0)
        {
            var name = _entry.ToString().Trim();
            if (name.Length > 0)
            {
                if (_entryIsCharacter) _characterStyles[_entryIndex] = name;
                else _paragraphStyles[_entryIndex] = name;
            }
        }
        if (_state.Destination is RtfDestination.FontTable or RtfDestination.StyleSheet)
        {
            _entry.Clear();
            if (depth >= 0) _entryIndex = -1;
        }
    }

    private bool _entryIsCharacter;

    private void Symbol(char symbol)
    {
        switch (symbol)
        {
            case '\\' or '{' or '}':
                FlushBytes();
                Emit(symbol.ToString());
                break;
            case '~':
                Emit("\u00A0");
                break;
            case '-':
                Emit("\u00AD");
                break;
            case '_':
                Emit("\u2011");
                break;
            case '\t':
                Emit("\t");
                break;
        }
    }

    private void Control(string word, int? parameter)
    {
        var value = parameter ?? 1;
        var character = _state.Character;
        var paragraph = _state.Paragraph;
        if (vocabulary.Destination(word, _star) is { } destination)
        {
            Destination(word, destination, parameter);
            return;
        }

        switch (word)
        {
            case "ansicpg":
                if (parameter is > 0) _ansi = codePages.Get(parameter.Value);
                _ansiSet = true;
                break;
            case "ansi":
                if (!_ansiSet) _ansi = codePages.Get(1252);
                break;
            case "mac":
                _ansi = codePages.Get(10000);
                break;
            case "pc":
                _ansi = codePages.Get(437);
                break;
            case "deff":
                _defaultFont = value;
                break;
            case "uc":
                _state.UnicodeSkip = Math.Clamp(value, 0, 16);
                break;
            case "u":
                var code = parameter ?? 0;
                if (code < 0) code += 65536;
                FlushBytes();
                Emit(((char)Math.Clamp(code, 0, 0xFFFF)).ToString(), true);
                _skip = _state.UnicodeSkip;
                break;
            case "f" when _state.Destination == RtfDestination.FontTable:
                CommitEntry(-1);
                _entryIndex = value;
                break;
            case "fcharset" when _state.Destination == RtfDestination.FontTable && _entryIndex >= 0:
                _fonts[_entryIndex] = new RtfFont(_fonts.TryGetValue(_entryIndex, out var font) ? font.Name : string.Empty, vocabulary.CharsetCodePage(value));
                break;
            case "red" when _state.Destination == RtfDestination.ColorTable:
                _red = Math.Max(0, _red) + value;
                break;
            case "green" when _state.Destination == RtfDestination.ColorTable:
                _green = Math.Max(0, _green) + value;
                break;
            case "blue" when _state.Destination == RtfDestination.ColorTable:
                _blue = Math.Max(0, _blue) + value;
                break;
            case "s" when _state.Destination == RtfDestination.StyleSheet:
                _entryIndex = value;
                _entryIsCharacter = false;
                break;
            case "cs" when _state.Destination == RtfDestination.StyleSheet:
                _entryIndex = value;
                _entryIsCharacter = true;
                break;
            case "list" when _state.Destination == RtfDestination.ListTable:
                _listLevels = [];
                _levelStarts = [];
                break;
            case "listlevel" when _state.Destination == RtfDestination.ListTable && _listLevels is { Count: < ListStyle.LevelCount } && _levelStarts is not null:
                _listLevels.Add(NumberFormat.Arabic);
                _levelStarts.Add(1);
                break;
            case "levelnfc" or "levelnfcn" when _state.Destination == RtfDestination.ListTable && _listLevels is { Count: > 0 }:
                _listLevels[^1] = vocabulary.ListFormat(value);
                break;
            case "levelstartat" when _state.Destination == RtfDestination.ListTable && _levelStarts is { Count: > 0 }:
                _levelStarts[^1] = Math.Max(0, value);
                break;
            case "listid" when _state.Destination == RtfDestination.ListTable && _listLevels is not null && _levelStarts is not null && _lists.Count < MaximumLists:
                _lists[value] = _listLevels;
                _listStarts[value] = _levelStarts;
                break;
            case "listoverride" when _state.Destination == RtfDestination.ListOverrideTable:
                _overrideLevel = -1;
                _overrideStart = null;
                break;
            case "lfolevel" when _state.Destination == RtfDestination.ListOverrideTable:
                _overrideLevel++;
                break;
            case "levelstartat" when _state.Destination == RtfDestination.ListOverrideTable && _overrideLevel is >= 0 and < ListStyle.LevelCount:
                _overrideStart = (_overrideLevel, Math.Max(0, value));
                break;
            case "listid" when _state.Destination == RtfDestination.ListOverrideTable:
                _listId = value;
                break;
            case "ls" when _state.Destination == RtfDestination.ListOverrideTable && _overrides.Count < MaximumLists:
                _overrides[value] = _listId;
                if (_overrideStart is { } overrideStart) _overrideStarts[value] = overrideStart;
                else _overrideStarts.Remove(value);
                break;
            case "pnlvlblt":
                if (_stack.TryPeek(out var bulletParent)) bulletParent.State.Paragraph.LegacyBullet = true;
                break;
            case "pnlvlbody" or "pnlvl":
                if (_stack.TryPeek(out var numberParent)) numberParent.State.Paragraph.LegacyBullet = false;
                break;
            case "picw" or "pich" or "picwgoal" or "pichgoal" or "picscalex" or "picscaley" or "pngblip" or "jpegblip" or "wmetafile" or "emfblip" or "macpict" or "dibitmap" or "wbitmap" when _picture is not null:
                _picture.Set(word, value);
                break;
            case "plain":
                _state.Character = new RtfCharacterState();
                break;
            case "pard":
                _state.Paragraph = new RtfParagraphState();
                break;
            case "b": character.Bold = value != 0; break;
            case "i": character.Italic = value != 0; break;
            case "ul": character.Underline = value != 0; character.DoubleUnderline = null; break;
            case "uldb": character.DoubleUnderline = value != 0; character.Underline = null; break;
            case "uld" or "ulw" or "ulth" or "uldash" or "ulwave" or "uldashd" or "uldashdd" or "ulhwave" or "ulldash" or "ulthd": character.Underline = value != 0; break;
            case "ulnone": character.Underline = false; character.DoubleUnderline = null; break;
            case "strike" or "striked": character.Strikethrough = value != 0; break;
            case "scaps": character.SmallCaps = value != 0; break;
            case "caps": character.Case = value != 0 ? TextCase.Upper : TextCase.None; break;
            case "embo": character.Relief = value != 0 ? FontRelief.Embossed : FontRelief.None; break;
            case "impr": character.Relief = value != 0 ? FontRelief.Engraved : FontRelief.None; break;
            case "expndtw": character.Expand = Math.Clamp(value, -MaximumTwips, MaximumTwips); break;
            case "charscalex": character.Scale = Math.Clamp(value, 1, 600); break;
            case "kerning": character.Kerning = value != 0; break;
            case "rtlch": character.RightToLeft = true; break;
            case "ltrch": character.RightToLeft = false; break;
            case "rtlpar": paragraph.Direction = PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft; break;
            case "ltrpar": paragraph.Direction = PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight; break;
            case "shad": character.Shadow = value != 0; break;
            case "outl": character.Outline = value != 0; break;
            case "super": character.Baseline = TextBaseline.Superscript; break;
            case "sub": character.Baseline = TextBaseline.Subscript; break;
            case "nosupersub": character.Baseline = TextBaseline.Normal; break;
            case "v": character.Hidden = value != 0; break;
            case "fs": character.FontSize = Math.Clamp(value, 2, 3276); break;
            case "f": character.Font = value; break;
            case "cf": character.Color = value; break;
            case "highlight" or "chcbpat" or "cb": character.Highlight = value; break;
            case "lang" or "langfe": if (word == "lang") character.Language = value; break;
            case "cs": character.CharacterStyle = value; break;
            case "ql": paragraph.Align = TextAlign.Left; break;
            case "qc": paragraph.Align = TextAlign.Center; break;
            case "qr": paragraph.Align = TextAlign.Right; break;
            case "qj" or "qd": paragraph.Align = TextAlign.Justify; break;
            case "li" or "lin": paragraph.Left = Math.Clamp(value, -MaximumTwips, MaximumTwips); break;
            case "ri" or "rin": paragraph.Right = Math.Clamp(value, -MaximumTwips, MaximumTwips); break;
            case "fi": paragraph.First = Math.Clamp(value, -MaximumTwips, MaximumTwips); break;
            case "sb": paragraph.Before = Math.Clamp(value, 0, MaximumTwips); break;
            case "sa": paragraph.After = Math.Clamp(value, 0, MaximumTwips); break;
            case "keepn": paragraph.KeepWithNext = value != 0; break;
            case "keep": paragraph.KeepTogether = value != 0; break;
            case "pagebb": paragraph.PageBreakBefore = value != 0; break;
            case "s": paragraph.Style = value; break;
            case "ls": paragraph.ListOverride = value; break;
            case "ilvl": paragraph.ListLevel = Math.Clamp(value, 0, ListStyle.LevelCount - 1); break;
            case "outlinelevel": paragraph.OutlineLevel = Math.Clamp(value, 0, 9); break;
            case "intbl": paragraph.InTable = true; break;
            case "par":
                Par();
                break;
            case "line":
                Emit("\n");
                break;
            case "tab":
                Emit("\t");
                break;
            case "page":
                if (_state.Destination != RtfDestination.Body) break;
                if (assembler.HasPendingContent) Par();
                _pageBreak = true;
                break;
            case "emdash": Emit("\u2014"); break;
            case "endash": Emit("\u2013"); break;
            case "bullet": Emit("\u2022"); break;
            case "lquote": Emit("\u2018"); break;
            case "rquote": Emit("\u2019"); break;
            case "ldblquote": Emit("\u201C"); break;
            case "rdblquote": Emit("\u201D"); break;
            case "enspace": Emit("\u2002"); break;
            case "emspace": Emit("\u2003"); break;
            case "qmspace": Emit("\u2005"); break;
            case "paperw" or "paperh" or "margl" or "margr" or "margt" or "margb" when _state.Destination == RtfDestination.Body && _depth <= 1 && parameter is > 0 and < 100000:
                Page(word, parameter.Value);
                break;
            case "shpleft" or "shpright" or "shptop" or "shpbottom" when _shapeDepth >= 0:
                _shape[word switch { "shpleft" => 0, "shpright" => 1, "shptop" => 2, _ => 3 }] = value;
                break;
            case "trowd":
                _paragraphBordering = false;
                _cells.Clear();
                _pendingCell = new RtfPendingCell();
                _rowGap = DefaultRowGap;
                break;
            case "trgaph":
                _rowGap = Math.Clamp(value, 0, MaximumRowGap);
                break;
            case "cellx":
                if (_cells.Count < MaximumCellDefinitions) _cells.Add(_pendingCell.Build(value, _rowGap));
                _pendingCell = new RtfPendingCell();
                break;
            case var opening when _paragraphBorderWords.Opens(opening):
                paragraph.Borders = _paragraphBorderWords.Open(paragraph.Borders, opening);
                _paragraphBordering = true;
                break;
            case var bordering when _paragraphBordering && paragraph.Borders is { } borders && _paragraphBorderWords.Apply(borders, bordering, value) is { } applied:
                paragraph.Borders = applied;
                break;
            case var rowBorder when rowBorder.StartsWith("trbrdr", StringComparison.Ordinal):
                _paragraphBordering = false;
                break;
            case var definition when definition.StartsWith("cl", StringComparison.Ordinal) && _pendingCell.Apply(definition, value):
                _paragraphBordering = false;
                break;
            case var line when line.StartsWith("brdr", StringComparison.Ordinal) && _pendingCell.Apply(line, value):
                break;
            case "cbpat": paragraph.Shading = value; break;
            case "trhdr": _rowIsHeader = true; break;
            case "cell" when _state.Destination is RtfDestination.Body or RtfDestination.FieldResult:
                Cell();
                break;
            case "row" when _state.Destination is RtfDestination.Body or RtfDestination.FieldResult:
                Row();
                break;
            case "chftn":
                break;
            case "ftnalt":
                if (_notes > 0 && assembler.Document.Notes.All.LastOrDefault() is { } note) note.Kind = NoteKind.Endnote;
                break;
        }
    }

    private RtfPendingCell _pendingCell = new();
    private readonly RtfParagraphBorderWords _paragraphBorderWords = new(new RtfBorderLineStyles());
    private bool _paragraphBordering;
    private int _rowGap = DefaultRowGap;

    private void Page(string word, int value)
    {
        if (assembler.Document.Styles.Page.Find(assembler.Document.InitialPageStyle) is not { } page) return;
        var twip = new Twip(value);
        switch (word)
        {
            case "paperw": page.Size = page.Size with { Width = twip }; break;
            case "paperh": page.Size = page.Size with { Height = twip }; break;
            case "margl": page.Margins = page.Margins with { Left = twip }; break;
            case "margr": page.Margins = page.Margins with { Right = twip }; break;
            case "margt": page.Margins = page.Margins with { Top = twip }; break;
            case "margb": page.Margins = page.Margins with { Bottom = twip }; break;
        }
    }

    private void Destination(string word, RtfDestination destination, int? parameter)
    {
        switch (word)
        {
            case "footnote":
                if (_state.Destination is not (RtfDestination.Body or RtfDestination.FieldResult)) break;
                Content();
                var reserved = assembler.ReserveNote(NoteKind.Footnote);
                assembler.Enter(reserved.Content);
                _notes++;
                _state.Footnote = true;
                _state.Paragraph = new RtfParagraphState();
                _state.Character = new RtfCharacterState();
                return;
            case "field":
                _state.Field = new RtfFieldContext();
                return;
            case "fldinst":
                _state.Destination = RtfDestination.FieldInstruction;
                return;
            case "fldrslt":
                _state.Destination = _state.Destination == RtfDestination.Skip ? RtfDestination.Skip : RtfDestination.FieldResult;
                return;
            case "pict":
                if (_state.Destination is RtfDestination.Skip && _shapeDepth < 0) return;
                _state.Destination = RtfDestination.Picture;
                _picture = new RtfPicture();
                _pictureDepth = _depth;
                return;
            case "object":
                _skippedObjects = true;
                _state.Destination = RtfDestination.ObjectWrapper;
                return;
            case "result":
                if (_state.Destination == RtfDestination.ObjectWrapper) _state.Destination = RtfDestination.Body;
                return;
            case "nonshppict":
                _state.Destination = RtfDestination.Skip;
                return;
            case "shppict":
                return;
            case "header" or "footer":
                if (_depth > 2 || assembler.Document.Styles.Page.Find(assembler.Document.InitialPageStyle) is not { } page)
                {
                    _state.Destination = RtfDestination.Skip;
                    return;
                }
                var region = word == "header" ? page.Header : page.Footer;
                region.Enabled = true;
                region.Default.Replace([]);
                assembler.Enter(region.Default);
                _notes++;
                _state.Footnote = true;
                _state.RegionStyle = word == "header" ? DefaultStyleNames.Header : DefaultStyleNames.Footer;
                _state.Paragraph = new RtfParagraphState();
                _state.Character = new RtfCharacterState();
                _state.Destination = RtfDestination.Body;
                return;
            case "sn" or "sv" when _shapeDepth >= 0:
                _entry.Clear();
                _state.Destination = RtfDestination.InfoText;
                _state.InfoKey = word;
                return;
            case "shpinst":
                _shapeDepth = _depth;
                _shape = new int[4];
                _fitToText = true;
                _state.Destination = RtfDestination.Skip;
                return;
            case "shptxt":
                if (_shapeDepth < 0 || _state.Destination is not RtfDestination.Skip || _notes > 0) return;
                Content();
                assembler.BeginTextBox(new DocumentSize(new Twip(Math.Clamp(_shape[1] - _shape[0], 20, 40000)), new Twip(Math.Clamp(_shape[3] - _shape[2], 20, 40000))), _fitToText);
                _notes++;
                _state.Footnote = true;
                _state.RegionStyle = DefaultStyleNames.FrameContents;
                _state.Paragraph = new RtfParagraphState();
                _state.Character = new RtfCharacterState();
                _state.Destination = RtfDestination.Body;
                return;
        }

        if (destination == RtfDestination.InfoText) _entry.Clear();
        if (destination is RtfDestination.BookmarkStart or RtfDestination.BookmarkEnd) _entry.Clear();
        if (destination == RtfDestination.ColorTable) _red = _green = _blue = -1000;
        if (destination is RtfDestination.FontTable or RtfDestination.StyleSheet)
        {
            _entryParent = _depth;
            _entryDepth = _depth;
            _entryIndex = destination == RtfDestination.StyleSheet ? 0 : -1;
            _entryIsCharacter = false;
            _entry.Clear();
        }
        _state.Destination = _state.Destination == RtfDestination.Skip ? RtfDestination.Skip : destination;
        _state.InfoKey = word;
    }

    private void OpenBookmark()
    {
        var name = _entry.ToString().Trim();
        _entry.Clear();
        if (name.Length == 0) return;
        Content();
        assembler.Mark(offset => new BookmarkMark(offset, name, MarkEdge.Start));
    }

    private void CloseBookmark()
    {
        var name = _entry.ToString().Trim();
        _entry.Clear();
        if (name.Length == 0) return;
        assembler.EndBookmark(name);
    }

    private void FinishPicture()
    {
        if (_picture is null) return;
        var picture = _picture;
        _picture = null;
        if (picture.MediaType is null || picture.Length == 0 || picture.Length > MaximumPictureBytes)
        {
            _skippedObjects = true;
            return;
        }

        var data = picture.Data();
        var pixels = header.Size(data);
        var width = picture.Width(pixels?.Width);
        var height = picture.Height(pixels?.Height);
        var size = new DocumentSize(new Twip(Math.Clamp(width, 20, 40000)), new Twip(Math.Clamp(height, 20, 40000)));
        Content();
        assembler.Image(new RasterImageObject
        {
            Id = assembler.Document.NextId("img"),
            Data = data,
            MediaType = picture.MediaType,
            PixelWidth = pixels?.Width ?? 0,
            PixelHeight = pixels?.Height ?? 0,
            NaturalSize = size,
        }, size);
    }

    private void Content()
    {
        if (_notes > 0) return;
        if (_state.Paragraph.InTable)
        {
            if (!_tableOpen)
            {
                assembler.Flush(Paragraph());
                assembler.BeginTable();
                _tableOpen = true;
                _headerRows = 0;
                _headerCounting = true;
            }
            if (!_rowOpen)
            {
                assembler.BeginRow();
                _rowOpen = true;
                _cellIndex = 0;
            }
            if (!_cellOpen) OpenCell();
        }
        else CloseTable();
    }

    private void OpenCell()
    {
        var definition = _cellIndex < _cells.Count ? _cells[_cellIndex] : null;
        var span = 1;
        if (definition is { MergeFirst: true })
            for (var next = _cellIndex + 1; next < _cells.Count && _cells[next].MergeNext; next++) span++;
        _cellDiscard = definition is { MergeNext: true };
        assembler.BeginCell(new CellShape
        {
            ColumnSpan = span,
            MergeUp = definition is { VerticalNext: true },
            Background = definition?.Shading is { } shading ? Color(shading) : null,
            VerticalAlign = definition?.VerticalAlign switch { 1 => CellVerticalAlign.Middle, 2 => CellVerticalAlign.Bottom, _ => CellVerticalAlign.Top },
            Borders = definition is { HasBox: true } boxed ? Box(boxed) : null,
        });
        _cellOpen = true;
    }

    private BoxBorders Box(RtfCellDefinition definition) => new()
    {
        Top = Line(definition.TopEdge),
        Left = Line(definition.LeftEdge),
        Bottom = Line(definition.BottomEdge),
        Right = Line(definition.RightEdge),
        PaddingTop = (definition.PaddingTop ?? 0) / 20.0,
        PaddingBottom = (definition.PaddingBottom ?? 0) / 20.0,
        PaddingLeft = (definition.PaddingLeft ?? definition.Gap) / 20.0,
        PaddingRight = (definition.PaddingRight ?? definition.Gap) / 20.0,
    };

    private BoxBorders Box(RtfParagraphBorders borders) => new()
    {
        Top = Line(borders.Top),
        Left = Line(borders.Left),
        Bottom = Line(borders.Bottom),
        Right = Line(borders.Right),
        PaddingTop = borders.TopSpace / 20.0,
        PaddingLeft = borders.LeftSpace / 20.0,
        PaddingBottom = borders.BottomSpace / 20.0,
        PaddingRight = borders.RightSpace / 20.0,
    };

    private BorderLine? Line(RtfCellEdge? edge) =>
        edge is null ? null : new BorderLine(edge.WidthTwips / 20.0, Color(edge.ColorIndex) ?? 0xFF000000) { Style = edge.Style };

    private void Cell()
    {
        if (_notes > 0) return;
        if (!_tableOpen || !_rowOpen || !_cellOpen)
        {
            var saved = _state.Paragraph.InTable;
            _state.Paragraph.InTable = true;
            Content();
            _state.Paragraph.InTable = saved;
        }
        assembler.EndCell(Paragraph(), _cellDiscard);
        _cellOpen = false;
        _cellDiscard = false;
        _cellIndex++;
    }

    private void Row()
    {
        if (_notes > 0 || !_tableOpen) return;
        if (_cellOpen) Cell();
        _rowOpen = false;
        _firstRowWidths ??= Widths();
        if (_headerCounting && _rowIsHeader) _headerRows++;
        else _headerCounting = false;
        _rowIsHeader = false;
    }

    private List<int> Widths()
    {
        var widths = new List<int>();
        var previous = 0;
        foreach (var cell in _cells)
        {
            widths.Add(Math.Max(1, cell.Right - previous));
            previous = cell.Right;
        }
        return widths;
    }

    private void CloseTable()
    {
        if (!_tableOpen) return;
        if (_cellOpen) Cell();
        _tableOpen = false;
        _rowOpen = false;
        assembler.EndTable(_firstRowWidths?.Select(width => new Twip(width)).ToList(), _headerRows);
        _firstRowWidths = null;
    }

    private void Par()
    {
        if (_state.Destination is not (RtfDestination.Body or RtfDestination.FieldResult)) return;
        Content();
        assembler.EndParagraph(Paragraph());
    }

    private ParagraphShape Paragraph() => Paragraph(_state);

    private ParagraphShape Paragraph(RtfGroupState state)
    {
        var paragraph = state.Paragraph;
        string? style = null;
        if (paragraph.Style is { } index && _paragraphStyles.TryGetValue(index, out var name)) style = vocabulary.ParagraphStyle(name, assembler.Document.Styles);
        if (style is null && paragraph.OutlineLevel is { } outline && outline < 9) style = DefaultStyleNames.HeadingLevel(outline + 1);
        if (_notes > 0 && style is null) style = RegionStyle(state) ?? (assembler.Document.Notes.All.LastOrDefault()?.Kind == NoteKind.Endnote ? DefaultStyleNames.Endnote : DefaultStyleNames.Footnote);

        string? listStyle = null;
        var bullet = false;
        var numbered = false;
        string? thread = null;
        string? segment = null;
        int? segmentStart = null;
        if (paragraph.ListOverride is { } listOverride && listOverride > 0)
        {
            var definition = _overrides.TryGetValue(listOverride, out var definitionId) ? definitionId : -listOverride;
            thread = "rtf" + definition.ToString(CultureInfo.InvariantCulture);
            if (_overrideStarts.TryGetValue(listOverride, out var overridden) && overridden.Level == paragraph.ListLevel)
            {
                segment = "ls" + listOverride.ToString(CultureInfo.InvariantCulture);
                segmentStart = overridden.Start;
            }
            else if (_listStarts.TryGetValue(definition, out var starts) && paragraph.ListLevel < starts.Count && starts[paragraph.ListLevel] != 1)
            {
                segment = thread + ":" + paragraph.ListLevel.ToString(CultureInfo.InvariantCulture);
                segmentStart = starts[paragraph.ListLevel];
            }
            var format = _overrides.TryGetValue(listOverride, out var listId) && _lists.TryGetValue(listId, out var levels) && levels.Count > 0
                ? levels[Math.Min(paragraph.ListLevel, levels.Count - 1)]
                : NumberFormat.Arabic;
            if (format != NumberFormat.None)
            {
                bullet = format == NumberFormat.Bullet;
                numbered = !bullet;
                listStyle = format switch
                {
                    NumberFormat.Bullet => DefaultStyleNames.List1,
                    NumberFormat.LowerLetter or NumberFormat.UpperLetter => DefaultStyleNames.NumberingAbc,
                    NumberFormat.LowerRoman or NumberFormat.UpperRoman => DefaultStyleNames.NumberingIvx,
                    _ => DefaultStyleNames.Numbering123,
                };
            }
        }
        else if (paragraph.LegacyBullet is { } legacy)
        {
            bullet = legacy;
            numbered = !legacy;
            listStyle = legacy ? DefaultStyleNames.List1 : DefaultStyleNames.Numbering123;
        }

        var properties = new ParagraphProperties
        {
            Align = paragraph.Align,
            MarginLeft = listStyle is null ? Points(paragraph.Left) : null,
            MarginRight = Points(paragraph.Right),
            Indent = listStyle is null ? Points(paragraph.First) : null,
            SpaceBefore = Points(paragraph.Before),
            SpaceAfter = Points(paragraph.After),
            KeepWithNext = paragraph.KeepWithNext,
            KeepTogether = paragraph.KeepTogether,
            PageBreakBefore = paragraph.PageBreakBefore is true || _pageBreak ? true : null,
            Direction = paragraph.Direction,
            Borders = paragraph.Borders is { } borders && (borders.HasLines || borders.HasSpace) ? Box(borders) : null,
            Background = paragraph.Shading is { } shading ? Color(shading) : null,
        };
        _pageBreak = false;
        if (paragraph.InTable && style is null) style = DefaultStyleNames.TableContents;
        return new ParagraphShape { StyleName = style, Properties = properties, ListStyleName = listStyle, Bullet = bullet, Numbered = numbered, Level = paragraph.ListLevel, ListId = thread, ListSegment = segment, ListSegmentStart = segmentStart };
    }

    private CharacterProperties Own(CharacterProperties run, CharacterProperties inherited) => new()
    {
        Bold = Differs(run.Bold, inherited.Bold),
        Italic = Differs(run.Italic, inherited.Italic),
        Underline = Differs(run.Underline, inherited.Underline),
        DoubleUnderline = Differs(run.DoubleUnderline, inherited.DoubleUnderline),
        Strikethrough = Differs(run.Strikethrough, inherited.Strikethrough),
        SmallCaps = Differs(run.SmallCaps, inherited.SmallCaps),
        Shadow = Differs(run.Shadow, inherited.Shadow),
        Outline = Differs(run.Outline, inherited.Outline),
        Baseline = run.Baseline,
        FontSize = run.FontSize is { } size && inherited.FontSize is { } basis && Math.Abs(size - basis) < 0.01 ? null : run.FontSize,
        Font = run.Font is { } name && string.Equals(name, inherited.Font ?? assembler.Document.Styles.DefaultCharacter.Font, StringComparison.OrdinalIgnoreCase) ? null : run.Font,
        Color = run.Color is { } color && color == inherited.Color ? null : run.Color,
        Highlight = run.Highlight,
        Language = run.Language,
        Case = run.Case,
        Relief = run.Relief,
        LetterSpacing = run.LetterSpacing,
        ScaleWidth = run.ScaleWidth,
        Kerning = run.Kerning,
        RightToLeft = run.RightToLeft,
    };

    private bool? Differs(bool? value, bool? inherited) =>
        value is null ? null : inherited is true ? (value.Value ? null : false) : value;

    private string? ParagraphStyleName(RtfParagraphState paragraph)
    {
        if (paragraph.Style is { } index && _paragraphStyles.TryGetValue(index, out var name) && vocabulary.ParagraphStyle(name, assembler.Document.Styles) is { } mapped) return mapped;
        return paragraph.OutlineLevel is { } outline && outline < 9 ? DefaultStyleNames.HeadingLevel(outline + 1) : null;
    }

    private string? RegionStyle(RtfGroupState state)
    {
        if (state.RegionStyle is { } own) return own;
        foreach (var (saved, _) in _stack)
            if (saved.RegionStyle is { } inherited) return inherited;
        return null;
    }

    private double? Points(int? twips) => twips is { } value ? value / 20.0 : null;

    private uint? Color(int index) => index > 0 && index < _colors.Count ? _colors[index] : null;

    private RunShape Run()
    {
        var character = _state.Character;
        string? fontName = null;
        if (character.Font is { } font && _fonts.TryGetValue(font, out var found) && found.Name.Length > 0) fontName = found.Name;
        string? language = null;
        if (character.Language is { } lcid && lcid > 0 && lcid != 1024)
        {
            try
            {
                language = CultureInfo.GetCultureInfo(lcid).Name;
            }
            catch (CultureNotFoundException)
            {
                language = null;
            }
        }

        var properties = new CharacterProperties
        {
            Bold = character.Bold,
            Italic = character.Italic,
            Underline = character.Underline,
            DoubleUnderline = character.DoubleUnderline,
            Strikethrough = character.Strikethrough,
            SmallCaps = character.SmallCaps,
            Shadow = character.Shadow,
            Outline = character.Outline,
            Baseline = character.Baseline is TextBaseline.Normal ? null : character.Baseline,
            FontSize = character.FontSize is { } half ? half / 2.0 : null,
            Font = fontName,
            Color = character.Color is { } color ? Color(color) : null,
            Highlight = character.Highlight is { } highlight ? Color(highlight) : null,
            Language = language is { Length: > 0 } ? language : null,
            Case = character.Case,
            Relief = character.Relief,
            LetterSpacing = character.Expand is { } expand ? expand / 20.0 : null,
            ScaleWidth = character.Scale is { } scale && scale > 0 ? scale : null,
            Kerning = character.Kerning,
            RightToLeft = character.RightToLeft is true ? true : null,
        };
        var sheet = assembler.Document.Styles;
        var characterStyle = character.CharacterStyle is { } styleIndex && _characterStyles.TryGetValue(styleIndex, out var styleName)
            ? vocabulary.CharacterStyle(styleName, sheet)
            : null;
        var paragraphStyle = ParagraphStyleName(_state.Paragraph);
        var inherited = resolver.ResolveCharacterStyle(sheet, characterStyle)
            .Over(paragraphStyle is null ? CharacterProperties.None : resolver.ResolveParagraphStyle(sheet, paragraphStyle).Character);
        var shape = new RunShape { Character = Own(properties, inherited), StyleName = characterStyle };
        if (_state.Destination == RtfDestination.FieldResult && _state.Field is { } field)
            shape = vocabulary.ApplyField(shape, field.Instruction.ToString());
        return shape;
    }
}
