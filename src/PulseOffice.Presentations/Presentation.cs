namespace PulseOffice.Presentations;

public sealed class Presentation : PulseOffice.Security.IMacroSignatureHolder, PulseOffice.Document.Forms.IFormDocument, PulseOffice.Document.Fonts.IFontDocument, PulseOffice.Document.Forms.IMacroDocument
{
    private readonly List<Slide> _slides = [];
    private int _counter;

    public SlideSize Size { get; set; } = SlideSize.Widescreen;

    public Theme Theme { get; set; } = new() { Name = "Light" };

    public DocumentDesign? DocumentDesign { get; set; }

    private readonly List<SlideMaster> _masters =
    [
        new SlideMaster { Id = "master1", Name = "Light", Theme = new Theme { Name = "Light" } },
    ];

    public SlideMaster Master
    {
        get => _masters[0];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _masters[0] = value;
        }
    }

    public IReadOnlyList<SlideMaster> Masters => _masters;

    public void AddMaster(SlideMaster master)
    {
        ArgumentNullException.ThrowIfNull(master);
        EnsureUnique(master.Id);
        _masters.Add(master);
    }

    public void InsertMaster(int index, SlideMaster master)
    {
        ArgumentNullException.ThrowIfNull(master);
        EnsureUnique(master.Id);
        _masters.Insert(Math.Clamp(index, 1, _masters.Count), master);
    }

    private void EnsureUnique(string id)
    {
        if (_masters.Any(existing => string.Equals(existing.Id, id, StringComparison.Ordinal)))
            throw new InvalidOperationException($"A master with id '{id}' already exists.");
    }

    public bool RemoveMaster(string id)
    {
        var index = _masters.FindIndex(master => master.Id == id);
        if (index <= 0) return false;
        _masters.RemoveAt(index);
        return true;
    }

    public void ReplaceMaster(SlideMaster master)
    {
        ArgumentNullException.ThrowIfNull(master);
        var index = _masters.FindIndex(existing => existing.Id == master.Id);
        if (index >= 0) _masters[index] = master;
    }

    public SlideMaster MasterOf(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        return slide.MasterId is { } id && _masters.FirstOrDefault(master => master.Id == id) is { } found ? found : _masters[0];
    }

    public Theme ThemeOf(Slide slide) => MasterOf(slide) is var master && ReferenceEquals(master, _masters[0]) ? Theme : master.Theme;

    public string NextMasterId()
    {
        var number = _masters.Count + 1;
        while (_masters.Any(master => master.Id == $"master{number}")) number++;
        return $"master{number}";
    }

    public IReadOnlyList<Slide> Slides => _slides;

    public ShowSetup Setup { get; set; } = new();

    public string? FileName { get; set; }

    public HeaderFooterSettings HeaderFooter { get; set; } = new();

    public GridSettings Grid { get; } = new();

    public PulseOffice.Drawing.Shapes.GraphicStyleCatalog GraphicStyles { get; } = new();

    public PulseOffice.Document.Metadata.DocumentProperties Properties { get; } = new();

    public PulseOffice.Document.Versions.DocumentVersionCollection Versions { get; } = new();

    public NotesMaster NotesMaster { get; set; } = new();

    public HandoutMaster Handout { get; set; } = new();

    public PulseOffice.Document.Forms.IMacroLibrary Macros { get; } = new PulseOffice.Document.Forms.MacroLibrary();

    public PulseOffice.Security.MacroSignature? MacroSignature { get; set; }

    public SoundLibrary Sounds { get; } = new();

    public PulseOffice.Document.Forms.FormDocumentSettings FormSettings { get; set; } = new();

    public PulseOffice.Text.Rich.TextLanguages DefaultLanguages { get; set; } = PulseOffice.Text.Rich.TextLanguages.None;

    public string? TextLanguage => Properties.Language ?? DefaultLanguages.Western;

    public PulseOffice.Text.Rich.ScriptFonts DefaultScriptFonts { get; set; } = PulseOffice.Text.Rich.ScriptFonts.None;

    public PulseOffice.Document.Fonts.DocumentFonts Fonts { get; set; } = PulseOffice.Document.Fonts.DocumentFonts.Empty;

    public IReadOnlyList<(string Name, string Language, string Source)> SignableMacros => [.. Macros.All.Select(macro => (macro.Name, macro.Language.ToString(), macro.Source))];

    private readonly List<CustomShow> _customShows = [];

    public IReadOnlyList<CustomShow> CustomShows => _customShows;

    public void AddCustomShow(CustomShow show)
    {
        ArgumentNullException.ThrowIfNull(show);
        RemoveCustomShow(show.Name);
        _customShows.Add(show);
    }

    public bool RemoveCustomShow(string name)
    {
        var index = _customShows.FindIndex(s => s.Name == name);
        if (index < 0) return false;
        _customShows.RemoveAt(index);
        return true;
    }

    public event EventHandler? Changed;

    public string NextId()
    {
        string candidate;
        do candidate = $"slide{++_counter}";
        while (_slides.Any(slide => string.Equals(slide.Id, candidate, StringComparison.Ordinal)));
        return candidate;
    }

    public void AddExisting(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        _slides.Add(slide);
        Raise();
    }

    public void InsertExisting(int index, Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        _slides.Insert(Math.Clamp(index, 0, _slides.Count), slide);
        Raise();
    }

    public void ReplaceAt(int index, Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        if (index < 0 || index >= _slides.Count) return;
        _slides[index] = slide;
        Raise();
    }

    public bool Remove(Slide slide)
    {
        if (!_slides.Remove(slide)) return false;
        Raise();
        return true;
    }

    public bool Move(int from, int to)
    {
        if (from < 0 || from >= _slides.Count) return false;
        to = Math.Clamp(to, 0, _slides.Count - 1);
        if (from == to) return false;

        var slide = _slides[from];
        _slides.RemoveAt(from);
        _slides.Insert(to, slide);
        Raise();
        return true;
    }

    public int IndexOf(Slide slide) => _slides.IndexOf(slide);

    public Presentation Copy()
    {
        var copy = new Presentation
        {
            Size = Size,
            Theme = Theme,
            DocumentDesign = DocumentDesign,
            Setup = Setup,
            FileName = FileName,
            HeaderFooter = HeaderFooter,
            NotesMaster = NotesMaster,
            Handout = Handout,
            MacroSignature = MacroSignature,
            FormSettings = FormSettings,
            DefaultLanguages = DefaultLanguages,
            DefaultScriptFonts = DefaultScriptFonts,
            Fonts = Fonts,
        };
        copy._counter = _counter;
        copy._masters.Clear();
        copy._masters.AddRange(_masters);
        copy._slides.AddRange(_slides.Select(slide => slide.Copy()));
        copy._customShows.AddRange(_customShows);
        Grid.CopyTo(copy.Grid);
        copy.GraphicStyles.Restore(GraphicStyles.All);
        copy.Properties.CopyFrom(Properties);
        copy.Versions.Replace(Versions.All);
        foreach (var macro in Macros.All) copy.Macros.Store(macro);
        foreach (var sound in Sounds.All) copy.Sounds.Add(sound);
        return copy;
    }

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
