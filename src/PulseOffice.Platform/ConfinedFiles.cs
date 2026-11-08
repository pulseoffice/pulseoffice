using System.Security.Cryptography;

namespace PulseOffice.Platform;

public sealed class ConfinedPathException(string message) : UnauthorizedAccessException(message);

public interface IFileSystemLinks
{
    bool IsLink(string path);
}

public sealed class FileSystemLinks : IFileSystemLinks
{
    public bool IsLink(string path)
    {
        var info = new FileInfo(path);
        if (info.LinkTarget is not null) return true;
        return (info.Exists || Directory.Exists(path)) && info.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }
}

public interface IConfinedFileWriter
{
    void Replace(string root, string path, Action<Stream> produce);
}

public sealed class ConfinedFileWriter(IFileSystemLinks links) : IConfinedFileWriter
{
    public void Replace(string root, string path, Action<Stream> produce)
    {
        ArgumentNullException.ThrowIfNull(produce);
        var target = Path.GetFullPath(path);
        var chain = Chain(Path.GetFullPath(root), target);
        foreach (var directory in chain)
        {
            Directory.CreateDirectory(directory);
            Demand(directory);
        }

        var temporary = Path.Combine(chain[^1], "." + Guid.NewGuid().ToString("N") + ".tmp");
        var moved = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                produce(stream);
            var fingerprint = Fingerprint(temporary);

            DemandChain(chain);
            Demand(temporary);
            Demand(target);

            File.Move(temporary, target, overwrite: true);
            moved = true;

            if (!ChainIsLinkFree(chain) || links.IsLink(target))
            {
                RollBack(target, fingerprint);
                throw new ConfinedPathException($"A folder on the way to '{target}' became a link while writing.");
            }
        }
        finally
        {
            if (!moved && File.Exists(temporary) && !links.IsLink(temporary)) File.Delete(temporary);
        }
    }

    private void RollBack(string path, byte[] fingerprint)
    {
        if (File.Exists(path) && !links.IsLink(path) && Fingerprint(path).AsSpan().SequenceEqual(fingerprint)) File.Delete(path);
    }

    private static byte[] Fingerprint(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return SHA256.HashData(stream);
    }

    private void DemandChain(IReadOnlyList<string> chain)
    {
        if (!ChainIsLinkFree(chain)) throw new ConfinedPathException($"A folder on the way to '{chain[^1]}' became a link.");
    }

    private bool ChainIsLinkFree(IReadOnlyList<string> chain) =>
        chain.All(directory => Directory.Exists(directory) && !links.IsLink(directory));

    private void Demand(string path)
    {
        if (links.IsLink(path)) throw new ConfinedPathException($"'{path}' is a link.");
    }

    private static List<string> Chain(string root, string target)
    {
        var parent = Path.GetDirectoryName(target) ?? throw new ConfinedPathException($"'{target}' has no folder.");
        var relative = Path.GetRelativePath(root, parent);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ConfinedPathException($"'{target}' is outside '{root}'.");

        var chain = new List<string> { root };
        if (relative == ".") return chain;
        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            chain.Add(current);
        }
        return chain;
    }
}
