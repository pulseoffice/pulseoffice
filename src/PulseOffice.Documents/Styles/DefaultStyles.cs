using System.Globalization;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Styles;

public static class DefaultStyleNames
{
    public const string Standard = "Standard";
    public const string Heading = "Heading";
    public const string TextBody = "Text body";
    public const string List = "List";
    public const string Caption = "Caption";
    public const string Index = "Index";
    public const string Title = "Title";
    public const string Subtitle = "Subtitle";
    public const string Quotations = "Quotations";
    public const string PreformattedText = "Preformatted Text";
    public const string Header = "Header";
    public const string Footer = "Footer";
    public const string HeaderAndFooter = "Header and Footer";
    public const string Footnote = "Footnote";
    public const string Endnote = "Endnote";
    public const string TableContents = "Table Contents";
    public const string TableHeading = "Table Heading";
    public const string FrameContents = "Frame contents";
    public const string Emphasis = "Emphasis";
    public const string StrongEmphasis = "Strong Emphasis";
    public const string InternetLink = "Internet link";
    public const string FootnoteSymbol = "Footnote Symbol";
    public const string EndnoteSymbol = "Endnote Symbol";
    public const string FootnoteAnchor = "Footnote anchor";
    public const string EndnoteAnchor = "Endnote anchor";
    public const string BulletSymbols = "Bullet Symbols";
    public const string NumberingSymbols = "Numbering Symbols";
    public const string SourceText = "Source Text";
    public const string Frame = "Frame";
    public const string Graphics = "Graphics";
    public const string Ole = "OLE";
    public const string FirstPage = "First Page";
    public const string LeftPage = "Left Page";
    public const string RightPage = "Right Page";
    public const string Landscape = "Landscape";
    public const string Numbering123 = "Numbering 123";
    public const string NumberingAbc = "Numbering abc";
    public const string NumberingIvx = "Numbering ivx";
    public const string NumberingAbcUpper = "Numbering ABC";
    public const string NumberingIvxUpper = "Numbering IVX";
    public const string Quotation = "Quotation";
    public const string List1 = "List 1";
    public const string DefaultTableStyle = "Default Style";

    public const string ContentsHeading = "Contents Heading";
    public const string IndexHeading = "Index Heading";
    public const string IndexSeparator = "Index Separator";
    public const string IllustrationIndexHeading = "Illustration Index Heading";
    public const string TableIndexHeading = "Table index heading";
    public const string BibliographyHeading = "Bibliography Heading";
    public const string UserIndexHeading = "User Index Heading";
    public const string ObjectIndexHeading = "Object index heading";
    public const string IndexLink = "Index Link";

    public static string HeadingLevel(int level) => "Heading " + level.ToString(CultureInfo.InvariantCulture);

    public static string ContentsLevel(int level) => "Contents " + level.ToString(CultureInfo.InvariantCulture);

    public static string IndexLevel(int level) => "Index " + level.ToString(CultureInfo.InvariantCulture);

    public static string UserIndexLevel(int level) => "User Index " + level.ToString(CultureInfo.InvariantCulture);

    public const string IllustrationIndex1 = "Illustration Index 1";
    public const string TableIndex1 = "Table index 1";
    public const string Bibliography1 = "Bibliography 1";
    public const string ObjectIndex1 = "Object index 1";
}

public interface IDefaultStyleSheetFactory
{
    StyleSheet Create();
}

public sealed class DefaultStyleSheetFactory : IDefaultStyleSheetFactory
{
    private const int HeadingLevels = 10;

    private readonly IReadOnlyList<double> _headingSizes = [18, 16, 14, 13, 12, 12, 10, 10, 9, 9];

    private readonly IReadOnlyList<double> _headingSpaceBefore = [12, 10, 7, 6, 6, 3, 3, 3, 3, 3];

    private readonly IReadOnlyList<double> _headingSpaceAfter = [6, 6, 6, 6, 3, 3, 3, 3, 3, 3];

    public StyleSheet Create()
    {
        var sheet = new StyleSheet();
        AddParagraphStyles(sheet);
        AddCharacterStyles(sheet);
        AddListStyles(sheet);
        AddPageStyles(sheet);
        AddFrameStyles(sheet);
        sheet.Table.Add(new TableStyle(DefaultStyleNames.DefaultTableStyle)
        {
            Table = new TableProperties { BorderColor = 0xFF000000, BorderWidth = 0.5, CellPadding = 2.8 },
        });
        return sheet;
    }

    private void AddParagraphStyles(StyleSheet sheet)
    {
        sheet.Paragraph.Add(new ParagraphStyle(DefaultStyleNames.Standard));
        sheet.Paragraph.Add(Child(DefaultStyleNames.TextBody, DefaultStyleNames.Standard, style =>
            style.Paragraph = new ParagraphProperties { SpaceAfter = 7, LineSpacing = 1.15 }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.Heading, DefaultStyleNames.Standard, style =>
        {
            style.NextStyleName = DefaultStyleNames.TextBody;
            style.Paragraph = new ParagraphProperties { SpaceBefore = 12, SpaceAfter = 6, KeepWithNext = true };
            style.Character = new CharacterProperties { Font = "Liberation Sans", FontSize = 14 };
            style.Category = ParagraphStyleCategory.Chapter;
        }));
        for (var level = 1; level <= HeadingLevels; level++)
        {
            var outline = level;
            var size = _headingSizes[level - 1];
            var before = _headingSpaceBefore[level - 1];
            var after = _headingSpaceAfter[level - 1];
            var italic = level is 4 or 6 or 8;
            sheet.Paragraph.Add(Child(DefaultStyleNames.HeadingLevel(level), DefaultStyleNames.Heading, style =>
            {
                style.NextStyleName = DefaultStyleNames.TextBody;
                style.OutlineLevel = outline;
                style.Paragraph = new ParagraphProperties { SpaceBefore = before, SpaceAfter = after };
                style.Character = italic
                    ? new CharacterProperties { FontSize = size, Bold = true, Italic = true }
                    : new CharacterProperties { FontSize = size, Bold = true };
                style.Category = ParagraphStyleCategory.Chapter;
            }));
        }
        sheet.Paragraph.Add(Child(DefaultStyleNames.Title, DefaultStyleNames.Heading, style =>
        {
            style.Paragraph = new ParagraphProperties { Align = TextAlign.Center };
            style.Character = new CharacterProperties { FontSize = 28, Bold = true };
            style.NextStyleName = DefaultStyleNames.Subtitle;
        }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.Subtitle, DefaultStyleNames.Heading, style =>
        {
            style.Paragraph = new ParagraphProperties { Align = TextAlign.Center, SpaceBefore = 3, SpaceAfter = 6 };
            style.Character = new CharacterProperties { FontSize = 18 };
            style.NextStyleName = DefaultStyleNames.TextBody;
        }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.List, DefaultStyleNames.TextBody, style =>
            style.Category = ParagraphStyleCategory.List));
        sheet.Paragraph.Add(Child(DefaultStyleNames.Caption, DefaultStyleNames.Standard, style =>
        {
            style.Paragraph = new ParagraphProperties { SpaceBefore = 6, SpaceAfter = 6 };
            style.Character = new CharacterProperties { FontSize = 12, Italic = true };
            style.Category = ParagraphStyleCategory.Special;
        }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.Index, DefaultStyleNames.Standard, style =>
            style.Category = ParagraphStyleCategory.Index));
        sheet.Paragraph.Add(Child(DefaultStyleNames.Quotations, DefaultStyleNames.Standard, style =>
            style.Paragraph = new ParagraphProperties { MarginLeft = 28.3, MarginRight = 28.3, SpaceAfter = 14.14 }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.PreformattedText, DefaultStyleNames.Standard, style =>
            style.Character = new CharacterProperties { Font = "Liberation Mono", FontSize = 10 }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.HeaderAndFooter, DefaultStyleNames.Standard, style =>
        {
            style.Paragraph = new ParagraphProperties
            {
                TabStops = [new TabStop(240.9) { Alignment = TabAlignment.Center }, new TabStop(481.9) { Alignment = TabAlignment.Right }],
            };
            style.Category = ParagraphStyleCategory.Special;
        }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.Header, DefaultStyleNames.HeaderAndFooter, _ => { }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.Footer, DefaultStyleNames.HeaderAndFooter, _ => { }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.Footnote, DefaultStyleNames.Standard, style =>
        {
            style.Paragraph = new ParagraphProperties { MarginLeft = 17, Indent = -17 };
            style.Character = new CharacterProperties { FontSize = 10 };
            style.Category = ParagraphStyleCategory.Special;
        }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.Endnote, DefaultStyleNames.Standard, style =>
        {
            style.Paragraph = new ParagraphProperties { MarginLeft = 17, Indent = -17 };
            style.Character = new CharacterProperties { FontSize = 10 };
            style.Category = ParagraphStyleCategory.Special;
        }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.TableContents, DefaultStyleNames.Standard, _ => { }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.TableHeading, DefaultStyleNames.TableContents, style =>
        {
            style.Paragraph = new ParagraphProperties { Align = TextAlign.Center };
            style.Character = new CharacterProperties { Bold = true };
        }));
        sheet.Paragraph.Add(Child(DefaultStyleNames.FrameContents, DefaultStyleNames.Standard, _ => { }));
        AddIndexStyles(sheet);
    }

    private void AddIndexStyles(StyleSheet sheet)
    {
        foreach (var heading in new[] { DefaultStyleNames.ContentsHeading, DefaultStyleNames.IndexHeading, DefaultStyleNames.IllustrationIndexHeading,
                     DefaultStyleNames.TableIndexHeading, DefaultStyleNames.BibliographyHeading, DefaultStyleNames.UserIndexHeading, DefaultStyleNames.ObjectIndexHeading })
        {
            sheet.Paragraph.Add(Child(heading, DefaultStyleNames.Heading, style =>
            {
                style.Character = new CharacterProperties { FontSize = 16, Bold = true };
                style.Category = ParagraphStyleCategory.Index;
                style.NextStyleName = DefaultStyleNames.TextBody;
            }));
        }
        for (var level = 1; level <= HeadingLevels; level++)
        {
            sheet.Paragraph.Add(Entry(DefaultStyleNames.ContentsLevel(level), level));
            sheet.Paragraph.Add(Entry(DefaultStyleNames.UserIndexLevel(level), level));
        }
        for (var level = 1; level <= 3; level++) sheet.Paragraph.Add(Entry(DefaultStyleNames.IndexLevel(level), level, false));
        sheet.Paragraph.Add(Child(DefaultStyleNames.IndexSeparator, DefaultStyleNames.Index, style =>
        {
            style.Paragraph = new ParagraphProperties { SpaceBefore = 6 };
            style.Character = new CharacterProperties { Bold = true };
        }));
        sheet.Paragraph.Add(Entry(DefaultStyleNames.IllustrationIndex1, 1));
        sheet.Paragraph.Add(Entry(DefaultStyleNames.TableIndex1, 1));
        sheet.Paragraph.Add(Entry(DefaultStyleNames.Bibliography1, 1, false));
        sheet.Paragraph.Add(Entry(DefaultStyleNames.ObjectIndex1, 1));
    }

    private ParagraphStyle Entry(string name, int level, bool tab = true) => Child(name, DefaultStyleNames.Index, style =>
    {
        style.Paragraph = new ParagraphProperties
        {
            MarginLeft = 14.17 * (level - 1),
            TabStops = tab ? [new TabStop(481.9) { Alignment = TabAlignment.Right, Leader = TabLeader.Dots }] : null,
        };
        style.Category = ParagraphStyleCategory.Index;
    });

    private ParagraphStyle Child(string name, string parent, Action<ParagraphStyle> configure)
    {
        var style = new ParagraphStyle(name) { ParentName = parent };
        configure(style);
        return style;
    }

    private void AddCharacterStyles(StyleSheet sheet)
    {
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.Emphasis) { Character = new() { Italic = true } });
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.StrongEmphasis) { Character = new() { Bold = true } });
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.InternetLink) { Character = new() { Color = 0xFF000080, Underline = true } });
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.FootnoteSymbol));
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.EndnoteSymbol));
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.FootnoteAnchor) { Character = new() { Baseline = TextBaseline.Superscript } });
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.EndnoteAnchor) { Character = new() { Baseline = TextBaseline.Superscript } });
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.BulletSymbols));
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.NumberingSymbols));
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.SourceText) { Character = new() { Font = "Liberation Mono" } });
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.Quotation));
        sheet.Character.Add(new CharacterStyle(DefaultStyleNames.IndexLink));
    }

    private void AddListStyles(StyleSheet sheet)
    {
        sheet.List.Add(new ListStyle(DefaultStyleNames.List1, Levels(level => new ListLevel(NumberFormat.Bullet) { Suffix = string.Empty, Bullet = level % 2 == 0 ? "•" : "◦" })));
        sheet.List.Add(new ListStyle(DefaultStyleNames.Numbering123, Levels(_ => new ListLevel(NumberFormat.Arabic))));
        sheet.List.Add(new ListStyle(DefaultStyleNames.NumberingAbc, Levels(_ => new ListLevel(NumberFormat.LowerLetter) { Suffix = ")" })));
        sheet.List.Add(new ListStyle(DefaultStyleNames.NumberingIvx, Levels(_ => new ListLevel(NumberFormat.LowerRoman))));
        sheet.List.Add(new ListStyle(DefaultStyleNames.NumberingAbcUpper, Levels(_ => new ListLevel(NumberFormat.UpperLetter))));
        sheet.List.Add(new ListStyle(DefaultStyleNames.NumberingIvxUpper, Levels(_ => new ListLevel(NumberFormat.UpperRoman))));
    }

    private IReadOnlyList<ListLevel> Levels(Func<int, ListLevel> level) =>
        [.. Enumerable.Range(0, ListStyle.LevelCount).Select(index => level(index) with { Indent = 18 * (index + 1) })];

    private void AddPageStyles(StyleSheet sheet)
    {
        sheet.Page.Add(new PageStyle(DefaultStyleNames.Standard));
        sheet.Page.Add(new PageStyle(DefaultStyleNames.FirstPage) { NextStyleName = DefaultStyleNames.Standard });
        sheet.Page.Add(new PageStyle(DefaultStyleNames.LeftPage) { Usage = PageUsage.Left, NextStyleName = DefaultStyleNames.RightPage });
        sheet.Page.Add(new PageStyle(DefaultStyleNames.RightPage) { Usage = PageUsage.Right, NextStyleName = DefaultStyleNames.LeftPage });
        sheet.Page.Add(new PageStyle(DefaultStyleNames.Landscape) { Orientation = Foundation.Pages.PageOrientation.Landscape });
        sheet.Page.Add(new PageStyle(DefaultStyleNames.Endnote));
    }

    private void AddFrameStyles(StyleSheet sheet)
    {
        sheet.Frame.Add(new FrameStyle(DefaultStyleNames.Frame)
        {
            Frame = new FrameProperties { Wrap = Objects.WrapMode.Parallel, BorderColor = 0xFF000000, BorderWidth = 0.5, Padding = 4 },
        });
        sheet.Frame.Add(new FrameStyle(DefaultStyleNames.Graphics) { Frame = new FrameProperties { Wrap = Objects.WrapMode.None } });
        sheet.Frame.Add(new FrameStyle(DefaultStyleNames.Ole) { ParentName = DefaultStyleNames.Graphics });
    }
}
