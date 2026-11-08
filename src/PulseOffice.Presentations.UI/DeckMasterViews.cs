using PulseOffice.Document.Tables;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using System;
using System.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

internal sealed class DeckMasterViews(DeckViewState state, IDeckViewHost host, ISlideRenderer renderer, IHandoutLayout handoutLayout, ISlideFactory masterSlides, IDeckChoiceNames names, Design.ILayoutChoices layouts)
{
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly Presentation _deck = state.Deck;
    private readonly DeckActions _actions = state.Actions;
    private readonly ITextCatalog _texts = state.Texts;
    private readonly DesignTokens.Metrics _metrics = state.Metrics;
    private readonly ISlideRenderer _renderer = renderer;
    private readonly IHandoutLayout _handoutLayout = handoutLayout;
    private readonly ISlideFactory _masterSlides = masterSlides;

    private readonly Design.ILayoutChoices _layouts = layouts;

    private SlideLayoutTemplate MasterTemplate()
    {
        var master = _deck.Master;
        if (_state.MasterLayoutId is { } id && master.Layouts.FirstOrDefault(layout => string.Equals(layout.Id, id, StringComparison.Ordinal)) is { } chosen)
            return chosen;
        var fallback = master.Template(SlideLayout.TitleAndContent);
        _state.MasterLayoutId = fallback.Id;
        return fallback;
    }

    public Control BuildMaster()
    {
        var template = MasterTemplate();
        var canvas = new SlideView(_renderer)
        {
            Size = _deck.Size, DeckTheme = _deck.Theme, Master = _deck.Master,
            Padding = _metrics.SpacingLarge, Editable = true,
        };
        canvas.Show(_masterSlides.Create("__master__", template));
        canvas.ElementChangeRequested += (_, change) =>
        {
            if (change.Element is TextElement text)
                _actions.SetPlaceholderBounds(template.Id, text.Text.Role, change.Bounds);
        };

        var layouts = _deck.Master.Layouts.Select(layout => new DeckNamedChoice<SlideLayoutTemplate>(layout, _layouts.Title(_deck.Master, layout))).ToList();
        var layoutPicker = new ComboBox { ItemsSource = layouts, MinWidth = 200, SelectedItem = layouts.FirstOrDefault(choice => ReferenceEquals(choice.Value, template)) };
        AutomationProperties.SetName(layoutPicker, _texts["deck.master.layout"]);
        layoutPicker.SelectionChanged += (_, _) =>
        {
            if (layoutPicker.SelectedItem is DeckNamedChoice<SlideLayoutTemplate> { Value: var chosen } && !ReferenceEquals(chosen, template))
            {
                _state.MasterLayoutId = chosen.Id;
                _host.RefreshMode();
            }
        };

        var custom = !_actions.Layouts.Catalog.IsStandard(_deck.Master, template);
        var nameBox = new TextBox { Text = _layouts.Title(_deck.Master, template), MinWidth = 160, IsEnabled = custom && !_actions.IsReadOnly };
        AutomationProperties.SetName(nameBox, _texts["deck.layouts.name"]);
        void CommitName()
        {
            if (!nameBox.IsEnabled || string.IsNullOrWhiteSpace(nameBox.Text)) return;
            _actions.Layouts.RenameLayout(_deck.Master, template.Id, nameBox.Text);
        }
        nameBox.LostFocus += (_, _) => CommitName();
        nameBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            CommitName();
            e.Handled = true;
        };

        var duplicate = ToolButton("deck.layouts.duplicateAction", !_actions.IsReadOnly, () =>
        {
            if (_actions.Layouts.DuplicateLayout(_deck.Master, template.Id, _texts.Format("deck.layouts.copyName", _layouts.Title(_deck.Master, template))) is { } copy)
            {
                _state.MasterLayoutId = copy.Id;
                _host.RefreshMode();
            }
        });
        var delete = ToolButton("deck.layouts.deleteAction", custom && !_actions.IsReadOnly, () =>
        {
            _actions.Layouts.DeleteLayout(_deck.Master, template.Id);
            _state.MasterLayoutId = null;
            _host.RefreshMode();
        });

        var roles = names.ColorRoles();
        var backgroundPicker = new ComboBox { ItemsSource = roles, SelectedItem = roles.FirstOrDefault(choice => choice.Value == _deck.Master.Background) };
        backgroundPicker.SelectionChanged += (_, _) =>
        {
            if (backgroundPicker.SelectedItem is DeckNamedChoice<ColorRole> { Value: var role } && role != _deck.Master.Background)
                _actions.SetMasterBackground(role);
        };

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            Margin = new Thickness(_metrics.SpacingLarge, _metrics.SpacingMedium),
            Children =
            {
                new TextBlock { Text = _texts["deck.master.layout"], VerticalAlignment = VerticalAlignment.Center }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                layoutPicker,
                new TextBlock { Text = _texts["deck.layouts.name"], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(_metrics.SpacingMedium, 0, 0, 0) }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                nameBox,
                duplicate,
                delete,
                new TextBlock { Text = _texts["deck.master.background"], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(_metrics.SpacingLarge, 0, 0, 0) }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                backgroundPicker,
            },
        };

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(canvas, 1);
        grid.Children.Add(toolbar);
        grid.Children.Add(canvas);

        return new Border { Child = grid }.Themed(Border.BackgroundProperty, Palette.Keys.Canvas);
    }

    public Control BuildPrintMaster(PrintMasterKind kind)
    {
        var view = new PrintMasterView(_renderer, _handoutLayout, _texts["deck.printMaster.notesSample"]) { Deck = _deck, Kind = kind };
        view.BoxChanged += (_, change) =>
        {
            var master = _deck.NotesMaster;
            _actions.SetNotesMaster(change.Box == NotesMasterBox.Slide ? master with { SlideBox = change.Bounds } : master with { NotesBox = change.Bounds });
            view.InvalidateVisual();
        };

        var notes = kind == PrintMasterKind.Notes;
        string Header() => notes ? _deck.NotesMaster.Header : _deck.Handout.Header;
        string Footer() => notes ? _deck.NotesMaster.Footer : _deck.Handout.Footer;
        bool Number() => notes ? _deck.NotesMaster.PageNumber : _deck.Handout.PageNumber;

        void Apply(string? header = null, string? footer = null, bool? number = null, double? size = null, int? perPage = null)
        {
            if (notes)
                _actions.SetNotesMaster(_deck.NotesMaster with
                {
                    Header = header ?? _deck.NotesMaster.Header,
                    Footer = footer ?? _deck.NotesMaster.Footer,
                    PageNumber = number ?? _deck.NotesMaster.PageNumber,
                    FontSize = size ?? _deck.NotesMaster.FontSize,
                });
            else
                _actions.SetHandoutMaster(_deck.Handout with
                {
                    Header = header ?? _deck.Handout.Header,
                    Footer = footer ?? _deck.Handout.Footer,
                    PageNumber = number ?? _deck.Handout.PageNumber,
                    SlidesPerPage = perPage ?? _deck.Handout.SlidesPerPage,
                });
            view.InvalidateVisual();
        }

        var header = new TextBox { Text = Header(), Width = 180, PlaceholderText = _texts["deck.printMaster.header"] };
        header.LostFocus += (_, _) => Apply(header: header.Text ?? string.Empty);
        var footer = new TextBox { Text = Footer(), Width = 180, PlaceholderText = _texts["deck.printMaster.footer"] };
        footer.LostFocus += (_, _) => Apply(footer: footer.Text ?? string.Empty);
        var number = new CheckBox { Content = _texts["deck.printMaster.pageNumber"], IsChecked = Number(), VerticalAlignment = VerticalAlignment.Center };
        number.IsCheckedChanged += (_, _) => Apply(number: number.IsChecked == true);

        Control extra;
        if (notes)
        {
            var size = new NumericUpDown { Minimum = 6, Maximum = 48, Increment = 1, Value = (decimal)_deck.NotesMaster.FontSize, Width = 110, FormatString = "0" };
            size.ValueChanged += (_, _) => { if (size.Value is { } value) Apply(size: (double)value); };
            extra = Labelled(_texts["deck.printMaster.fontSize"], size);
        }
        else
        {
            var choices = HandoutMaster.Choices;
            var picker = new ComboBox { ItemsSource = choices, SelectedItem = _handoutLayout.Nearest(_deck.Handout.SlidesPerPage) };
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is int value) Apply(perPage: value); };
            extra = Labelled(_texts["deck.printMaster.slidesPerPage"], picker);
        }

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingMedium,
            Margin = new Thickness(_metrics.SpacingLarge, _metrics.SpacingMedium),
            Children = { header, footer, number, extra },
        };

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(view, 1);
        grid.Children.Add(toolbar);
        grid.Children.Add(view);
        return new Border { Child = grid }.Themed(Border.BackgroundProperty, Palette.Keys.Canvas);
    }

    private Button ToolButton(string key, bool enabled, Action run)
    {
        var button = new Button { Content = _texts[key], IsEnabled = enabled, VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => run();
        return button;
    }

    private Control Labelled(string label, Control control) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = _metrics.SpacingSmall,
        Children =
        {
            new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
            control,
        },
    };
}
