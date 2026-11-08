using System.Diagnostics;
using Acornima;
using Jint;
using Jint.Runtime;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Scripting.JavaScript;

public sealed class JavaScriptEngine : IScriptEngine
{
    private const string AllocationGuard = """
        ((limit) => {
          const refuse = () => { throw new RangeError('Allocation exceeds the memory limit of the macro'); };
          const bytes = (value, size) => {
            if (typeof value === 'number' || typeof value === 'bigint') return Number(value) * size;
            if (value !== null && typeof value === 'object' && !(value instanceof ArrayBuffer) && !(typeof SharedArrayBuffer === 'function' && value instanceof SharedArrayBuffer)) {
              const length = Number(value.length);
              return Number.isFinite(length) ? length * size : 0;
            }
            return 0;
          };
          const guard = (name, measure) => {
            const original = globalThis[name];
            if (typeof original !== 'function') return;
            const guarded = new Proxy(original, {
              construct(target, args, newTarget) {
                if (measure(args) > limit) refuse();
                return Reflect.construct(target, args, newTarget === guarded ? target : newTarget);
              },
            });
            Object.defineProperty(original.prototype, 'constructor', { value: guarded, writable: false, configurable: false });
            Object.defineProperty(globalThis, name, { value: guarded, writable: false, configurable: false });
          };
          const buffer = (args) => Math.max(bytes(args[0], 1), args[1] && typeof args[1] === 'object' ? Number(args[1].maxByteLength) || 0 : 0);
          guard('ArrayBuffer', buffer);
          guard('SharedArrayBuffer', buffer);
          for (const name of ['Int8Array', 'Uint8Array', 'Uint8ClampedArray', 'Int16Array', 'Uint16Array', 'Int32Array', 'Uint32Array',
                              'Float16Array', 'Float32Array', 'Float64Array', 'BigInt64Array', 'BigUint64Array']) {
            const size = globalThis[name] ? globalThis[name].BYTES_PER_ELEMENT : 1;
            guard(name, (args) => bytes(args[0], size));
          }
        })
        """;

    private readonly CompletionSites _sites = new();

    public ScriptLanguage Language => ScriptLanguage.JavaScript;
    public string DisplayName => "JavaScript (Jint)";
    public string RuntimeVersion => typeof(Engine).Assembly.GetName().Version?.ToString() ?? "unknown";

    public bool IsSandboxed => true;

    public IReadOnlySet<string> Keywords { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete",
        "do", "else", "export", "extends", "finally", "for", "function", "if", "import", "in",
        "instanceof", "let", "new", "return", "super", "switch", "this", "throw", "try", "typeof",
        "var", "void", "while", "with", "yield", "async", "await", "static", "get", "set",
        "true", "false", "null", "undefined", "of",
    };

    public IReadOnlyList<ScriptDiagnostic> Analyze(string source)
    {
        try
        {
            new Parser().ParseScript(source);
            return [];
        }
        catch (ParseErrorException ex)
        {
            var position = new ScriptPosition(
                ex.LineNumber > 0 ? ex.LineNumber : 1,
                ex.Column > 0 ? ex.Column : 1);
            return [ScriptMessages.Syntax(ex.Description ?? ex.Message, position, "JS0001")];
        }
    }

    public async Task<ScriptResult> ExecuteAsync(string source, ScriptContext context, CancellationToken ct = default)
    {
        var syntax = Analyze(source);
        if (syntax.Any(d => d.Severity == ScriptDiagnosticSeverity.Error))
            return new ScriptResult(false, null, syntax, TimeSpan.Zero);

        if (ct.IsCancellationRequested)
            return new ScriptResult(false, null, [ScriptMessages.Cancelled().Diagnostic("JS0007")], TimeSpan.Zero);

        var stopwatch = Stopwatch.StartNew();

        var running = Task.Run(() =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(context.Permissions.Timeout);
            try
            {
                using var engine = new Engine(options =>
                {
                    options.LimitRecursion(256);
                    options.LimitMemory(context.Permissions.MaxMemoryBytes);
                    options.TimeoutInterval(context.Permissions.Timeout);
                    options.CancellationToken(timeout.Token);

                    options.DisableStringCompilation();
                    options.CatchClrExceptions();
                    options.Constraints.MaxArraySize = (uint)Math.Clamp(context.Permissions.MaxMemoryBytes / 8, 1, uint.MaxValue);
                });

                engine.Invoke(engine.Evaluate(AllocationGuard), context.Permissions.MaxMemoryBytes);

                foreach (var (name, value) in context.Globals)
                    engine.SetValue(name, value);

                if (context.OnOutput is { } output)
                {
                    engine.SetValue("print", new Action<object?>(v => output(v?.ToString() ?? "undefined")));
                    engine.SetValue("console", new { log = new Action<object?>(v => output(v?.ToString() ?? "undefined")) });
                }

                var result = engine.Evaluate(source);
                stopwatch.Stop();

                return new ScriptResult(true, result.IsUndefined() ? null : result.ToObject(), [], stopwatch.Elapsed);
            }
            catch (JavaScriptException ex)
            {
                stopwatch.Stop();
                var position = new ScriptPosition(ex.Location.Start.Line, ex.Location.Start.Column + 1);
                return new ScriptResult(false, null,
                    [ScriptMessages.Failure(ex.Message, position, "JS0100")],
                    stopwatch.Elapsed, ex.JavaScriptStackTrace);
            }
            catch (TimeoutException)
            {
                stopwatch.Stop();
                return Failure(ScriptMessages.Timeout(context.Permissions.Timeout), "JS0002", stopwatch.Elapsed);
            }
            catch (ExecutionCanceledException)
            {
                stopwatch.Stop();
                return ct.IsCancellationRequested
                    ? Failure(ScriptMessages.Cancelled(), "JS0007", stopwatch.Elapsed)
                    : Failure(ScriptMessages.Timeout(context.Permissions.Timeout), "JS0002", stopwatch.Elapsed);
            }
            catch (Exception ex) when (ex is MemoryLimitExceededException or OutOfMemoryException)
            {
                stopwatch.Stop();
                return Failure(ScriptMessages.Memory(), "JS0003", stopwatch.Elapsed);
            }
            catch (RecursionDepthOverflowException)
            {
                stopwatch.Stop();
                return Failure(ScriptMessages.Recursion(), "JS0004", stopwatch.Elapsed);
            }
            catch (JintException ex)
            {
                stopwatch.Stop();
                return Failure(ScriptMessages.Internal(ex.Message), "JS0005", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return Failure(ScriptMessages.Internal(ex.Message), "JS0006", stopwatch.Elapsed);
            }
        }, CancellationToken.None);

        var limit = context.Permissions.Timeout + TimeSpan.FromSeconds(2);
        if (await Task.WhenAny(running, Task.Delay(limit, CancellationToken.None)).ConfigureAwait(false) == running)
            return await running.ConfigureAwait(false);

        return Failure(ScriptMessages.Timeout(context.Permissions.Timeout), "JS0002", limit);

        static ScriptResult Failure(ScriptMessage message, string code, TimeSpan elapsed) =>
            new(false, null, [message.Diagnostic(code)], elapsed);
    }

    public IReadOnlyList<CompletionItem> Complete(
        string source, ScriptPosition position, ScriptApiCatalog api, ModuleKind module)
    {
        ArgumentNullException.ThrowIfNull(api);

        var site = _sites.Find(source, position, ScriptLanguage.JavaScript);

        if (site.Kind == CompletionSiteKind.None) return [];
        if (site.Kind == CompletionSiteKind.Member)
            return [.. api.Complete(site.Path, module).Select(ToCompletion)];

        var items = api.Complete(string.Empty, module).Select(ToCompletion).ToList();
        items.AddRange(Keywords.Select(k => new CompletionItem(k, CompletionKind.Keyword)));
        return items;
    }

    private static CompletionItem ToCompletion(ApiSymbol symbol) =>
        new(symbol.Name, MapKind(symbol.Kind), symbol.Signature ?? symbol.TypeName, symbol.Documentation)
        { IsApiSymbol = true };

    private static CompletionKind MapKind(ApiSymbolKind kind) => kind switch
    {
        ApiSymbolKind.Method or ApiSymbolKind.Constructor => CompletionKind.Method,
        ApiSymbolKind.Property or ApiSymbolKind.Field => CompletionKind.Property,
        ApiSymbolKind.Class or ApiSymbolKind.Interface => CompletionKind.Class,
        ApiSymbolKind.Enum or ApiSymbolKind.EnumMember => CompletionKind.Enum,
        ApiSymbolKind.Namespace => CompletionKind.Module,
        _ => CompletionKind.Variable,
    };

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
