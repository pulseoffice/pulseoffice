namespace PulseOffice.Platform;

public interface IExecutableFileTypes
{
    bool CanRunCode(string fileName);
}

public sealed class ExecutableFileTypes : IExecutableFileTypes
{
    private readonly HashSet<string> _extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".bat", ".cmd", ".scr", ".pif", ".msi", ".msp", ".msc", ".cpl", ".dll", ".sys", ".ocx",
        ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".ps1", ".psm1", ".psd1", ".hta", ".lnk", ".url", ".reg",
        ".inf", ".scf", ".application", ".appref-ms", ".gadget", ".jar", ".jnlp", ".appx", ".msix", ".appinstaller",
        ".app", ".command", ".tool", ".action", ".workflow", ".pkg", ".mpkg", ".dmg", ".terminal", ".scpt", ".scptd",
        ".applescript", ".osascript", ".webloc", ".inetloc", ".fileloc", ".kext", ".prefpane", ".plugin", ".bundle",
        ".sh", ".bash", ".zsh", ".csh", ".ksh", ".fish", ".desktop", ".run", ".bin", ".deb", ".rpm", ".appimage",
        ".flatpakref", ".snap", ".py", ".pyw", ".pyc", ".pl", ".rb", ".php", ".lua", ".tcl", ".elf", ".out",
        ".docm", ".dotm", ".xlsm", ".xltm", ".xlam", ".pptm", ".potm", ".ppam", ".ppsm", ".sldm", ".xll",
        ".html", ".htm", ".xhtml", ".svg", ".mht", ".mhtml", ".xml", ".chm", ".iso", ".img", ".vhd", ".vhdx",
    };

    public bool CanRunCode(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var name = Path.GetFileName(fileName.TrimEnd('.', ' ', '/', '\\'));
        return name.Length == 0 || !name.Contains('.', StringComparison.Ordinal) || _extensions.Contains(Path.GetExtension(name));
    }
}
