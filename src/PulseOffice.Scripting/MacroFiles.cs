using PulseOffice.Platform;

namespace PulseOffice.Scripting;

public sealed class MacroFilePathException(string message) : UnauthorizedAccessException(message);

public interface IMacroFileFolder
{
    string Root { get; }

    string Resolve(string name, IReadOnlySet<string> extensions);
}

public sealed class MacroFileFolder(string root, IHostSystem host) : IMacroFileFolder
{
    public MacroFileFolder(IHostSystem host)
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PulseOffice", "Macros"), host)
    {
    }

    public MacroFileFolder(string root)
        : this(root, new RuntimeHostSystem())
    {
    }

    public static readonly IReadOnlySet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".csv", ".tsv", ".json", ".xml", ".md", ".html", ".htm", ".log",
    };

    public string Root { get; } = Path.GetFullPath(root);

    public string Resolve(string name, IReadOnlySet<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        if (string.IsNullOrWhiteSpace(name)) throw new MacroFilePathException("The file name is empty.");

        var candidate = Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.GetFullPath(Path.Combine(Root, name));
        var prefix = Path.EndsInDirectorySeparator(Root) ? Root : Root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, host.PathComparison))
            throw new MacroFilePathException($"'{name}' is outside the macro folder {Root}.");

        if (name.Split('/', '\\').Any(segment => segment == ".."))
            throw new MacroFilePathException($"'{name}' climbs out of its folder.");

        var fileName = Path.GetFileName(candidate);
        if (fileName.Length == 0 || fileName.StartsWith('.') || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new MacroFilePathException($"'{name}' is not a valid file name.");

        if (!extensions.Contains(Path.GetExtension(candidate)))
            throw new MacroFilePathException($"Macros may not write '{Path.GetExtension(candidate)}' files.");

        Directory.CreateDirectory(Root);
        for (var directory = Path.GetDirectoryName(candidate); directory is not null && directory.Length >= Root.Length; directory = Path.GetDirectoryName(directory))
            if (Directory.Exists(directory) && new DirectoryInfo(directory).LinkTarget is not null)
                throw new MacroFilePathException($"'{name}' passes through a link.");

        if (File.Exists(candidate) && new FileInfo(candidate).LinkTarget is not null)
            throw new MacroFilePathException($"'{name}' is a link.");

        return candidate;
    }
}

public sealed class ScriptFilesFacade(IScriptExecutionPermissions permissions, IMacroFileFolder folder, IConfinedFileWriter writer)
{
    private const int MaximumCharacters = 16 * 1024 * 1024;

    public string Folder
    {
        get
        {
            permissions.Demand(ScriptCapability.MacroFolderWrite);
            return folder.Root;
        }
    }

    public string WriteText(string name, string text)
    {
        var path = Target(name, text);
        Replace(path, name, _ => { }, text);
        return path;
    }

    public string AppendText(string name, string text)
    {
        var path = Target(name, text);
        Replace(path, name, temporary => CopyExisting(path, name, text, temporary), text);
        return path;
    }

    public bool Exists(string name)
    {
        permissions.Demand(ScriptCapability.MacroFolderWrite);
        return File.Exists(folder.Resolve(name, MacroFileFolder.TextExtensions));
    }

    private void Replace(string path, string name, Action<FileStream> prefix, string text)
    {
        try
        {
            writer.Replace(folder.Root, path, stream =>
            {
                prefix((FileStream)stream);
                using var output = new StreamWriter(stream);
                output.Write(text);
            });
        }
        catch (ConfinedPathException ex)
        {
            throw new MacroFilePathException($"'{name}' cannot be written safely: {ex.Message}");
        }
    }

    private void CopyExisting(string path, string name, string text, FileStream temporary)
    {
        if (!File.Exists(path)) return;

        using var existing = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (new FileInfo(path).LinkTarget is not null)
            throw new MacroFilePathException($"'{name}' became a link.");
        if (existing.Length + (long)text.Length * 4 > MaximumCharacters * 4L)
            throw new InvalidOperationException($"'{name}' would grow beyond the limit for macro files.");
        existing.CopyTo(temporary);
    }

    private string Target(string name, string text)
    {
        permissions.Demand(ScriptCapability.MacroFolderWrite);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumCharacters) throw new InvalidOperationException("The text is too long for a macro file.");
        return folder.Resolve(name, MacroFileFolder.TextExtensions);
    }
}
