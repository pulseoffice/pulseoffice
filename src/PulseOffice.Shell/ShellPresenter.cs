namespace PulseOffice.Shell;

public interface IShellPresenter
{
    Task ShowCommandPaletteAsync();

    Task ShowShortcutsAsync();

    Task CloseActiveDocumentAsync();

    void RequestQuit();
}
