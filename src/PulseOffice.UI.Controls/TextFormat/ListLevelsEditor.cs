using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.TextFormat;

public interface IListLevelsEditors
{
    ListLevelsEditor Create(ListFormatRequest request, DesignTokens.Metrics metrics);
}

public sealed class ListLevelsEditors(
    IListPresetCatalog presets,
    IListSampleRows rows,
    IGalleryNavigator navigator,
    IMeasureUnits units,
    IDialogService dialogs,
    IIconCatalog icons,
    ITextCatalog texts,
    IListPictureChooser pictures) : IListLevelsEditors
{
    public ListLevelsEditor Create(ListFormatRequest request, DesignTokens.Metrics metrics) =>
        new(request, new ListLevelsEditorServices(presets, new ListPresetPainter(rows), navigator, units, dialogs, icons, texts, pictures), metrics);
}

public sealed record ListLevelsEditorServices(
    IListPresetCatalog Presets,
    IThumbnailPainter<ListPreset> Painter,
    IGalleryNavigator Navigator,
    IMeasureUnits Units,
    IDialogService Dialogs,
    IIconCatalog Icons,
    ITextCatalog Texts,
    IListPictureChooser Pictures);

public sealed class ListLevelsEditor : UserControl
{
    private readonly ListFormatRequest _request;
    private readonly ListLevelsEditorServices _services;
    private readonly IReadOnlyList<ListPreset> _presets;
    private readonly ThumbnailGallery<ListPreset> _gallery;
    private readonly ListBox _levelList;
    private readonly ComboBox _numbering;
    private readonly TextBox _bullet = new() { Width = 56, MaxLength = 2, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly TextBox _prefix = new() { Width = 72 };
    private readonly TextBox _suffix = new() { Width = 72 };
    private readonly NumberBox _start;
    private readonly NumberBox _display;
    private readonly MeasureField _indent;
    private readonly MeasureField _hanging;
    private readonly List<DialogRow> _numberRows = [];
    private readonly DialogRow _bulletRow;
    private readonly DialogRow? _pictureRow;
    private readonly Image _picturePreview = new() { Width = 20, Height = 20, Stretch = Avalonia.Media.Stretch.Uniform };
    private readonly Button _removePicture = new();
    private Avalonia.Media.Imaging.Bitmap? _pictureBitmap;
    private IReadOnlyList<ListLevelFormat> _levels;
    private int _level;
    private bool _loading;
    private ListBulletPicture? _shownPicture;

    public ListLevelsEditor(ListFormatRequest request, ListLevelsEditorServices services, DesignTokens.Metrics metrics)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _levels = request.Levels;
        _level = Math.Clamp(request.Level, 0, Math.Max(0, request.Levels.Count - 1));
        var texts = services.Texts;
        var culture = CultureInfo.CurrentCulture;

        _presets = services.Presets.Presets(request);
        _gallery = new ThumbnailGallery<ListPreset>(
            [.. _presets.Select(preset => new ThumbnailGalleryItem<ListPreset>(preset, preset.Caption,
                preset.Level.Numbering switch
                {
                    ListNumbering.None => null,
                    ListNumbering.Bullet => texts["textFormat.list.group.bullets"],
                    _ => texts["textFormat.list.group.numbers"],
                }))],
            services.Painter,
            services.Navigator,
            new ThumbnailGalleryOptions { TileWidth = 84, ThumbnailHeight = 52, Columns = 5, AccessibleName = texts["textFormat.list.gallery"] },
            metrics);
        _gallery.SelectionChanged += (_, preset) => Choose(preset);

        _levelList = new ListBox
        {
            ItemsSource = Enumerable.Range(1, request.Levels.Count).Select(number => number.ToString(culture)).ToList(),
            SelectedIndex = _level,
            Width = 52,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        AutomationProperties.SetName(_levelList, texts["textFormat.list.level"]);
        _levelList.SelectionChanged += (_, _) =>
        {
            if (_levelList.SelectedIndex < 0 || _levelList.SelectedIndex == _level) return;
            _level = _levelList.SelectedIndex;
            Load();
            Changed?.Invoke(this, EventArgs.Empty);
        };

        _numbering = new ComboBox
        {
            ItemsSource = request.Numberings.Select(numbering => texts[$"textFormat.list.numbering.{numbering.ToString().ToLowerInvariant()}"]).ToList(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _numbering.SelectionChanged += (_, _) => Edit(level => level with { Numbering = request.Numberings[Math.Max(0, _numbering.SelectedIndex)] });
        _bullet.TextChanged += (_, _) => Edit(level => level with { Bullet = _bullet.Text is { Length: > 0 } bullet ? bullet : level.Bullet });
        _prefix.TextChanged += (_, _) => Edit(level => level with { Prefix = _prefix.Text ?? string.Empty });
        _suffix.TextChanged += (_, _) => Edit(level => level with { Suffix = _suffix.Text ?? string.Empty });
        _start = new NumberBox(new NumberEntry(new NumericBounds(0, 9999), true, culture), 1, metrics, boxWidth: 72);
        _start.ValueCommitted += (_, value) => Edit(level => level with { Start = (int)Math.Round(value) });
        _display = new NumberBox(new NumberEntry(new NumericBounds(1, Math.Max(1, request.Levels.Count)), true, culture), 1, metrics, boxWidth: 72);
        _display.ValueCommitted += (_, value) => Edit(level => level with { DisplayLevels = (int)Math.Round(value) });
        _indent = Measure(texts["textFormat.list.indent"], metrics, culture);
        _indent.ValueCommitted += (_, value) => Edit(level => level with { Indent = value });
        _hanging = Measure(texts["textFormat.list.hanging"], metrics, culture);
        _hanging.ValueCommitted += (_, value) => Edit(level => level with { Hanging = value });

        var details = new DialogSection(texts["textFormat.list.section.level"], metrics);
        details.Add(texts["textFormat.list.numbering"], _numbering, editorWidth: 180);
        _bulletRow = details.Add(texts["textFormat.list.bullet"], BulletEditor(texts, metrics), editorWidth: 180);
        if (request.Offers(ListFeatures.Picture)) _pictureRow = details.Add(texts["textFormat.list.picture"], PictureEditor(texts, metrics), editorWidth: 220);
        if (request.Offers(ListFeatures.Affixes))
        {
            _numberRows.Add(details.Add(texts["textFormat.list.prefix"], _prefix, editorWidth: 120));
            _numberRows.Add(details.Add(texts["textFormat.list.suffix"], _suffix, editorWidth: 120));
        }
        if (request.Offers(ListFeatures.Start)) _numberRows.Add(details.Add(texts["textFormat.list.start"], _start, editorWidth: 120));
        if (request.Offers(ListFeatures.DisplayLevels)) _numberRows.Add(details.Add(texts["textFormat.list.display"], _display, editorWidth: 120));
        if (request.Offers(ListFeatures.Indents))
        {
            details.Add(texts["textFormat.list.indent"], _indent, editorWidth: 160);
            details.Add(texts["textFormat.list.hanging"], _hanging, editorWidth: 160);
        }

        var lower = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = metrics.SpacingLarge };
        Grid.SetIsSharedSizeScope(lower, true);
        var levelColumn = new StackPanel { Spacing = metrics.SpacingSmall };
        levelColumn.Children.Add(new TextBlock { Text = texts["textFormat.list.level"], FontSize = metrics.FontSizeSmall });
        levelColumn.Children.Add(_levelList);
        lower.Children.Add(levelColumn);
        Grid.SetColumn(details, 1);
        lower.Children.Add(details);

        var layout = new StackPanel { Spacing = metrics.SpacingLarge };
        layout.Children.Add(_gallery);
        if (request.Levels.Count > 1 || request.Features != ListFeatures.None) layout.Children.Add(lower);
        Content = layout;
        Load();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ListLevelFormat> Levels => _levels;

    public int Level => _level;

    public ThumbnailGallery<ListPreset> Gallery => _gallery;

    public ListLevelsAnswer Answer => new(_levels, _level);

    public void Choose(ListPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        _levels = _services.Presets.Apply(_levels, _level, preset);
        Load();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SelectLevel(int level)
    {
        if (_levels.Count == 0) return;
        _levelList.SelectedIndex = Math.Clamp(level, 0, _levels.Count - 1);
    }

    private Control BulletEditor(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        AutomationProperties.SetName(_bullet, texts["textFormat.list.bullet"]);
        if (!_request.Offers(ListFeatures.Symbol)) return _bullet;
        var browse = new Button { Content = texts["textFormat.list.symbol"] };
        browse.Click += async (_, _) =>
        {
            if (await _services.Dialogs.ShowAsync<string>(new SymbolPickerView(texts), new DialogOptions { TitleKey = "textFormat.list.symbol.title", PreferredWidth = 520 }).ConfigureAwait(true) is { Length: > 0 } glyph)
                _bullet.Text = glyph;
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall };
        row.Children.Add(_bullet);
        row.Children.Add(browse);
        return row;
    }

    public void SetPicture(ListBulletPicture? picture) => Edit(level => level with { Picture = picture });

    private Control PictureEditor(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var choose = new Button { Content = texts["textFormat.list.picture.choose"] };
        AutomationProperties.SetName(choose, texts["textFormat.list.picture.choose"]);
        choose.Click += async (_, _) =>
        {
            if (await _services.Pictures.ChooseAsync(CancellationToken.None).ConfigureAwait(true) is { } picture) SetPicture(picture);
        };
        _removePicture.Content = texts["textFormat.list.picture.remove"];
        AutomationProperties.SetName(_removePicture, texts["textFormat.list.picture.remove"]);
        _removePicture.Click += (_, _) => SetPicture(null);
        _removePicture.IsEnabled = false;
        _picturePreview.IsVisible = false;
        AutomationProperties.SetName(_picturePreview, texts["textFormat.list.picture"]);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall };
        row.Children.Add(_picturePreview);
        row.Children.Add(choose);
        row.Children.Add(_removePicture);
        return row;
    }

    private void ShowPicture(ListBulletPicture? picture)
    {
        _removePicture.IsEnabled = picture is not null;
        _picturePreview.IsVisible = picture is not null;
        _picturePreview.Source = null;
        _pictureBitmap?.Dispose();
        _pictureBitmap = null;
        if (picture is null) return;
        try
        {
            using var stream = new MemoryStream(picture.Data.ToArray(), false);
            _pictureBitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            _picturePreview.Source = _pictureBitmap;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            _picturePreview.IsVisible = false;
        }
    }

    private MeasureField Measure(string label, DesignTokens.Metrics metrics, CultureInfo culture) =>
        new(new MeasureEntry(new MeasureOptions("pt", 0, 500, 1) { LargeStep = 18 }, _services.Units, culture), 0, _services.Texts, metrics, _services.Icons, label, boxWidth: 64)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };

    private void Edit(Func<ListLevelFormat, ListLevelFormat> change)
    {
        if (_loading || _levels.Count == 0) return;
        var edited = change(_levels[_level]);
        if (edited == _levels[_level]) return;
        _levels = [.. _levels.Select((existing, index) => index == _level ? edited : existing)];
        Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Load()
    {
        if (_levels.Count == 0) return;
        _loading = true;
        var level = _levels[_level];
        _numbering.SelectedIndex = Math.Max(0, _request.Numberings.ToList().IndexOf(level.Numbering));
        _bullet.Text = level.Bullet;
        _prefix.Text = level.Prefix;
        _suffix.Text = level.Suffix;
        _start.Value = level.Start;
        _display.Value = level.DisplayLevels;
        _indent.Value = level.Indent;
        _hanging.Value = level.Hanging;
        _loading = false;
        Refresh();
    }

    private void Refresh()
    {
        var level = _levels[_level];
        _bulletRow.IsVisible = level.Numbering == ListNumbering.Bullet;
        if (_pictureRow is not null)
        {
            _pictureRow.IsVisible = level.Numbering == ListNumbering.Bullet;
            if (!ReferenceEquals(_shownPicture, level.Picture))
            {
                _shownPicture = level.Picture;
                ShowPicture(level.Picture);
            }
        }
        foreach (var row in _numberRows) row.IsVisible = level.Numbering is not (ListNumbering.None or ListNumbering.Bullet);
        if (_services.Presets.Matching(_presets, level, _level) is { } preset) _gallery.Select(preset);
        else _gallery.Select(-1);
    }
}

public sealed record ListLevelsField(string Key, string Label, ListFormatRequest Request, IListLevelsEditors Editors) : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = Editors.Create(Request, metrics);
        return new FormFieldEditor(editor, () => editor.Answer, Labelled: false) { Observe = changed => editor.Changed += (_, _) => changed() };
    }
}
