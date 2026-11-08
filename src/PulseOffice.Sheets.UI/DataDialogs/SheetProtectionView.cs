using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.UI.Controls.Dialogs;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record SheetProtectionChoice(string Password, SheetProtection Permissions);

public sealed class SheetProtectionView : LedgerDialogView
{
    private readonly bool _unprotect;
    private readonly IReadOnlyList<(string Key, CheckBox Box)> _permissions;

    public SheetProtectionView(LedgerDialogKit kit, SheetProtection current)
        : base(kit, current?.IsEnabled == true ? "sheet.unprotect.explanation" : "sheet.protect.explanation")
    {
        ArgumentNullException.ThrowIfNull(current);
        _unprotect = current.IsEnabled;
        Password = new TextBox { PasswordChar = '•', Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        Confirmation = new TextBox { PasswordChar = '•', Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        _permissions =
        [
            ("selectLocked", kit.Check("protect.allow.selectLocked", current.AllowSelectLockedCells)),
            ("selectUnlocked", kit.Check("protect.allow.selectUnlocked", current.AllowSelectUnlockedCells)),
            ("format", kit.Check("protect.allow.format", current.AllowFormatCells)),
            ("insertRows", kit.Check("protect.allow.insertRows", current.AllowInsertRows)),
            ("insertColumns", kit.Check("protect.allow.insertColumns", current.AllowInsertColumns)),
            ("deleteRows", kit.Check("protect.allow.deleteRows", current.AllowDeleteRows)),
            ("deleteColumns", kit.Check("protect.allow.deleteColumns", current.AllowDeleteColumns)),
            ("sort", kit.Check("protect.allow.sort", current.AllowSort)),
            ("filter", kit.Check("protect.allow.filter", current.AllowFilter)),
        ];
        Current = current;

        var password = Layout.AddSection(kit["protect.section.password"]);
        password.Add(kit["protect.password"], Password);
        if (!_unprotect)
        {
            password.Add(kit["protect.confirm"], Confirmation);
            password.Add(null, kit.Note(kit["protect.passwordHint"]), DialogRowKind.Indented);
            var allowed = Layout.AddSection(kit["protect.section.allow"]);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = kit.Metrics.SpacingLarge, RowSpacing = 2 };
            for (var index = 0; index < _permissions.Count; index++)
            {
                var box = _permissions[index].Box;
                Grid.SetColumn(box, index % 2);
                Grid.SetRow(box, index / 2);
                if (index % 2 == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                grid.Children.Add(box);
            }

            allowed.Add(null, grid, DialogRowKind.Wide);
            Finish("protect.apply", allowed);
        }
        else
        {
            Finish("protect.remove", password);
        }

        Password.TextChanged += (_, _) => Refresh();
        Confirmation.TextChanged += (_, _) => Refresh();
        Refresh();
    }

    public SheetProtection Current { get; }

    public TextBox Password { get; }

    public TextBox Confirmation { get; }

    public IReadOnlyList<CheckBox> Permissions => [.. _permissions.Select(permission => permission.Box)];

    public SheetProtectionChoice Choice => new(Password.Text ?? string.Empty, Current with
    {
        AllowSelectLockedCells = Allowed("selectLocked"),
        AllowSelectUnlockedCells = Allowed("selectUnlocked"),
        AllowFormatCells = Allowed("format"),
        AllowInsertRows = Allowed("insertRows"),
        AllowInsertColumns = Allowed("insertColumns"),
        AllowDeleteRows = Allowed("deleteRows"),
        AllowDeleteColumns = Allowed("deleteColumns"),
        AllowSort = Allowed("sort"),
        AllowFilter = Allowed("filter"),
    });

    protected override object? Result() => Choice;

    private bool Allowed(string key) => _permissions.First(permission => permission.Key == key).Box.IsChecked == true;

    private void Refresh() =>
        Report(!_unprotect && !string.Equals(Password.Text ?? string.Empty, Confirmation.Text ?? string.Empty, StringComparison.Ordinal)
            ? Kit["protect.mismatch"]
            : null);
}
