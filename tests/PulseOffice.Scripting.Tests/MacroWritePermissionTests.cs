using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Platform;
using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting.JavaScript;
using PulseOffice.Scripting.Python;
using Xunit;

namespace PulseOffice.Scripting.Tests;

[Collection("scripting")]
public sealed class MacroWritePermissionTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "pulse-macro-files-" + Guid.NewGuid().ToString("N"));
    private readonly ScriptExecutionPermissions _permissions = new();
    private readonly Sha256MacroFingerprints _fingerprints = new();
    private readonly FileMacroWriteGrants _grants;
    private readonly MacroPermissionPolicy _policy;
    private readonly MacroFileFolder _folder;

    public MacroWritePermissionTests()
    {
        _grants = new FileMacroWriteGrants(Path.Combine(_scratch, "config", "macro-file-permissions.json"), NullLogger<FileMacroWriteGrants>.Instance);
        _policy = new MacroPermissionPolicy(_fingerprints, _grants);
        _folder = new MacroFileFolder(Path.Combine(_scratch, "Macros"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_scratch)) Directory.Delete(_scratch, recursive: true);
    }

    private const string JavaScript = "files.WriteText('totali.csv', 'a;b')";
    private const string Python = "files.WriteText('totali.csv', 'a;b')";

    private IScriptEngine Engine(ScriptLanguage language) =>
        new PermissionScopedScriptEngine(language == ScriptLanguage.JavaScript ? new JavaScriptEngine() : new IronPythonScriptEngine(), _permissions);

    private Task<ScriptResult> RunAsync(ScriptLanguage language, string source) =>
        Engine(language).ExecuteAsync(source, new ScriptContext
        {
            Module = ModuleKind.Sheets,
            Globals = new Dictionary<string, object>
            {
                ["files"] = new ScriptFilesFacade(_permissions, _folder, new ConfinedFileWriter(new FileSystemLinks())),
                ["page"] = new FileProbe(_permissions),
            },
            Permissions = _policy.For(language, source, ScriptPermissions.Sandboxed with { Timeout = TimeSpan.FromSeconds(10), AllowsUnsandboxedEngines = true }),
        }, TestContext.Current.CancellationToken);

    private string Written => Path.Combine(_folder.Root, "totali.csv");

    [Theory]
    [InlineData(ScriptLanguage.JavaScript, JavaScript)]
    [InlineData(ScriptLanguage.Python, Python)]
    public async Task Senza_permesso_la_scrittura_e_rifiutata(ScriptLanguage language, string source)
    {
        var result = await RunAsync(language, source);

        Assert.False(result.Succeeded);
        Assert.False(File.Exists(Written));
    }

    [Theory]
    [InlineData(ScriptLanguage.JavaScript, JavaScript)]
    [InlineData(ScriptLanguage.Python, Python)]
    public async Task Con_il_permesso_la_scrittura_riesce_nella_cartella_delle_macro(ScriptLanguage language, string source)
    {
        _grants.Grant(_fingerprints.Of(language, source));

        var result = await RunAsync(language, source);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("a;b", await File.ReadAllTextAsync(Written, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cambiato_il_codice_il_permesso_decade()
    {
        _grants.Grant(_fingerprints.Of(ScriptLanguage.JavaScript, JavaScript));
        var changed = JavaScript + ";files.WriteText('altro.txt', 'x')";

        var result = await RunAsync(ScriptLanguage.JavaScript, changed);

        Assert.False(result.Succeeded);
        Assert.False(File.Exists(Written));
        Assert.False(_policy.For(ScriptLanguage.JavaScript, changed, ScriptPermissions.Sandboxed).MacroFolderWrite);
    }

    [Fact]
    public void Lo_stesso_codice_in_un_altro_linguaggio_non_eredita_il_permesso()
    {
        _grants.Grant(_fingerprints.Of(ScriptLanguage.JavaScript, JavaScript));

        Assert.False(_policy.For(ScriptLanguage.Python, JavaScript, ScriptPermissions.Sandboxed).MacroFolderWrite);
        Assert.True(_policy.For(ScriptLanguage.JavaScript, JavaScript.Replace("\n", "\r\n"), ScriptPermissions.Sandboxed).MacroFolderWrite);
    }

    [Fact]
    public async Task Il_permesso_concesso_non_apre_la_scrittura_libera_dei_file()
    {
        const string source = "page.Write('/tmp/x.pdf')";
        _grants.Grant(_fingerprints.Of(ScriptLanguage.JavaScript, source));

        var result = await RunAsync(ScriptLanguage.JavaScript, source);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Revocato_il_permesso_la_scrittura_torna_rifiutata()
    {
        var macro = _fingerprints.Of(ScriptLanguage.JavaScript, JavaScript);
        _grants.Grant(macro);
        _grants.Revoke(macro);

        var result = await RunAsync(ScriptLanguage.JavaScript, JavaScript);

        Assert.False(result.Succeeded);
        Assert.False(File.Exists(Written));
    }

    [Fact]
    public void Il_permesso_resta_nelle_impostazioni_locali_e_si_rilegge()
    {
        var macro = _fingerprints.Of(ScriptLanguage.JavaScript, JavaScript);
        _grants.Grant(macro);

        var reread = new FileMacroWriteGrants(Path.Combine(_scratch, "config", "macro-file-permissions.json"), NullLogger<FileMacroWriteGrants>.Instance);

        Assert.True(reread.IsGranted(macro));
    }

    [Fact]
    public void Se_il_permesso_non_si_salva_non_resta_concesso_e_non_scoppia()
    {
        var blocker = Path.Combine(_scratch, "bloccante");
        Directory.CreateDirectory(_scratch);
        File.WriteAllText(blocker, "file al posto della cartella");
        var grants = new FileMacroWriteGrants(Path.Combine(blocker, "macro-file-permissions.json"), NullLogger<FileMacroWriteGrants>.Instance);
        var macro = _fingerprints.Of(ScriptLanguage.JavaScript, JavaScript);

        Assert.False(grants.Grant(macro));
        Assert.False(grants.IsGranted(macro));
    }

    [Fact]
    public void Se_la_revoca_non_si_salva_il_permesso_resta_e_lo_dice()
    {
        var path = Path.Combine(_scratch, "config", "macro-file-permissions.json");
        var grants = new FileMacroWriteGrants(path, NullLogger<FileMacroWriteGrants>.Instance);
        var macro = _fingerprints.Of(ScriptLanguage.JavaScript, JavaScript);
        Assert.True(grants.Grant(macro));
        Directory.CreateDirectory(path + ".tmp");

        Assert.False(grants.Revoke(macro));
        Assert.True(grants.IsGranted(macro));
        Assert.True(new FileMacroWriteGrants(path, NullLogger<FileMacroWriteGrants>.Instance).IsGranted(macro));
    }

    [Fact]
    public void Un_file_dei_permessi_rovinato_non_concede_nulla()
    {
        var path = Path.Combine(_scratch, "config", "broken.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"not\": \"a list\" ");

        var grants = new FileMacroWriteGrants(path, NullLogger<FileMacroWriteGrants>.Instance);

        Assert.False(grants.IsGranted(_fingerprints.Of(ScriptLanguage.JavaScript, JavaScript)));
    }

    [Fact]
    public void Il_documento_non_porta_alcun_permesso()
    {
        var properties = typeof(PulseOffice.Document.Forms.DocumentMacro).GetProperties().Select(property => property.Name).ToList();

        Assert.DoesNotContain(properties, name =>
            name.Contains("Permission", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Grant", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Trust", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("../fuori.txt")]
    [InlineData("sotto/../../fuori.txt")]
    [InlineData("/etc/hosts.txt")]
    [InlineData("programma.sh")]
    [InlineData("libreria.dll")]
    [InlineData(".nascosto.txt")]
    [InlineData("")]
    public void I_percorsi_fuori_dalla_cartella_o_non_di_testo_sono_rifiutati(string name)
    {
        Assert.ThrowsAny<UnauthorizedAccessException>(() => _folder.Resolve(name, MacroFileFolder.TextExtensions));
    }

    [Fact]
    public void Una_sottocartella_che_e_un_collegamento_e_rifiutata()
    {
        Directory.CreateDirectory(_folder.Root);
        var outside = Path.Combine(_scratch, "fuori");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Path.Combine(_folder.Root, "ponte"), outside);

        Assert.Throws<MacroFilePathException>(() => _folder.Resolve("ponte/x.txt", MacroFileFolder.TextExtensions));
    }

    [Fact]
    public void Un_collegamento_a_un_file_che_non_esiste_ancora_e_rifiutato()
    {
        Directory.CreateDirectory(_folder.Root);
        var outside = Path.Combine(_scratch, "fuori", "bersaglio.txt");
        File.CreateSymbolicLink(Path.Combine(_folder.Root, "esca.txt"), outside);

        Assert.Throws<MacroFilePathException>(() => _folder.Resolve("esca.txt", MacroFileFolder.TextExtensions));
    }

    [Fact]
    public void Un_collegamento_messo_dopo_il_controllo_non_porta_la_scrittura_fuori()
    {
        using var scope = _permissions.Enter(ScriptPermissions.Sandboxed with { MacroFolderWrite = true });
        Directory.CreateDirectory(_folder.Root);
        var outside = Path.Combine(_scratch, "fuori");
        Directory.CreateDirectory(outside);
        var target = Path.Combine(outside, "bersaglio.txt");
        File.WriteAllText(target, "originale");
        var swapping = new SwappingFolder(_folder, target);
        var files = new ScriptFilesFacade(_permissions, swapping, new ConfinedFileWriter(new FileSystemLinks()));

        try
        {
            files.WriteText("esca.txt", "macro");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
        }

        try
        {
            files.AppendText("esca.txt", "macro");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
        }

        Assert.Equal("originale", File.ReadAllText(target));
    }

    [Fact]
    public void Appending_to_a_file_that_is_a_hard_link_never_changes_the_other_name()
    {
        using var scope = _permissions.Enter(ScriptPermissions.Sandboxed with { MacroFolderWrite = true });
        Directory.CreateDirectory(_folder.Root);
        var outside = Path.Combine(_scratch, "fuori");
        Directory.CreateDirectory(outside);
        var target = Path.Combine(outside, "bersaglio.txt");
        File.WriteAllText(target, "originale");
        new HardLinks().Create(Path.Combine(_folder.Root, "esca.txt"), target);
        var files = new ScriptFilesFacade(_permissions, _folder, new ConfinedFileWriter(new FileSystemLinks()));

        files.AppendText("esca.txt", "+macro");

        Assert.Equal("originale", File.ReadAllText(target));
        Assert.Equal("originale+macro", File.ReadAllText(Path.Combine(_folder.Root, "esca.txt")));
    }

    [Fact]
    public void Appending_keeps_what_the_file_already_held()
    {
        using var scope = _permissions.Enter(ScriptPermissions.Sandboxed with { MacroFolderWrite = true });
        var files = new ScriptFilesFacade(_permissions, _folder, new ConfinedFileWriter(new FileSystemLinks()));

        files.AppendText("registro.log", "uno\n");
        files.AppendText("registro.log", "due\n");

        Assert.Equal("uno\ndue\n", File.ReadAllText(Path.Combine(_folder.Root, "registro.log")));
    }

    private sealed class SwappingFolder(IMacroFileFolder inner, string outside) : IMacroFileFolder
    {
        public string Root => inner.Root;

        public string Resolve(string name, IReadOnlySet<string> extensions)
        {
            var path = inner.Resolve(name, extensions);
            if (File.Exists(path) || new FileInfo(path).LinkTarget is not null) File.Delete(path);
            File.CreateSymbolicLink(path, outside);
            return path;
        }
    }

    [Fact]
    public void Un_nome_con_sottocartelle_resta_dentro_la_cartella()
    {
        var path = _folder.Resolve("report/totali.csv", MacroFileFolder.TextExtensions);

        Assert.StartsWith(_folder.Root + Path.DirectorySeparatorChar, path, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ScriptLanguage.JavaScript, "files.WriteText(`a\u2028b.txt`, '')", "files.WriteText(`a\nb.txt`, '')")]
    [InlineData(ScriptLanguage.Python, "x = \"\"\"a\fb\"\"\"", "x = \"\"\"a\nb\"\"\"")]
    [InlineData(ScriptLanguage.Python, "# note\u0085files.WriteText('a.txt', '')", "# note\nfiles.WriteText('a.txt', '')")]
    public void Code_that_differs_only_in_characters_the_languages_do_not_treat_as_line_breaks_has_its_own_fingerprint(
        ScriptLanguage language, string granted, string other)
    {
        Assert.NotEqual(_fingerprints.Of(language, granted), _fingerprints.Of(language, other));
    }

    [Fact]
    public void Windows_and_classic_line_endings_keep_the_fingerprint()
    {
        Assert.Equal(_fingerprints.Of(ScriptLanguage.JavaScript, "a\nb\nc"), _fingerprints.Of(ScriptLanguage.JavaScript, "a\r\nb\rc"));
    }
}
