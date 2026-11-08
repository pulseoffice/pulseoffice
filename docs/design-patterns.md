# Design patterns: working reference for PulseOffice

Operational reference of the 22 classic (GoF) patterns for .NET 10 / C# code composed with
`Microsoft.Extensions.DependencyInjection` and modelled with immutable records. The reference
catalogue is refactoring.guru's; the explanations and skeletons here are written for this
project, not copied. The skeletons have no comments, like the rest of the code (rule 5).

The three families:

| Family | Question it answers | Patterns |
|---|---|---|
| Creational | who builds the object, and how | Factory Method, Abstract Factory, Builder, Prototype, Singleton |
| Structural | how objects and classes are composed | Adapter, Bridge, Composite, Decorator, Facade, Flyweight, Proxy |
| Behavioural | who does what, and how they talk | Chain of Responsibility, Command, Iterator, Mediator, Memento, Observer, State, Strategy, Template Method, Visitor |

---

## Creational patterns

### Factory Method

**Intent.** A base class declares a method that creates a product; subclasses decide which
concrete product. The code that uses the product works only with its interface.

**Problem.** The code creates a concrete type directly (`new OdsWriter()`) and adding a
variant forces you to touch every creation point.

**When to apply it.**
- `new` of concrete classes scattered in an otherwise identical algorithm;
- a `switch` on an extension or a "kind" that ends with a `new`;
- a base class that does all the work except choosing *which* collaborator to use.

**Structure in C#.** Abstract creator with an abstract creation method, sealed concrete creators.
With DI the "creator" often becomes an injected factory.

```csharp
public interface IPackageWriter
{
    void Write(Stream target, Workbook workbook);
}

public abstract class SpreadsheetExporter
{
    protected abstract IPackageWriter CreateWriter();

    public void Export(Stream target, Workbook workbook)
    {
        var writer = CreateWriter();
        writer.Write(target, workbook);
    }
}

public sealed class OdsExporter(IOdfStyleMapper styles) : SpreadsheetExporter
{
    protected override IPackageWriter CreateWriter() => new OdsPackageWriter(styles);
}

public sealed class XlsxExporter(IOoxmlStyleMapper styles) : SpreadsheetExporter
{
    protected override IPackageWriter CreateWriter() => new XlsxPackageWriter(styles);
}
```

**With DI.** The product's dependencies reach the creator through the constructor; the creator is
registered by interface. Idiomatic variant: `Func<T>` or a registered `IXxxFactory`, so the
container remains the only one that knows the concrete types.

**Pros.** Decouples use and creation; a single place to add variants (Open/Closed).
**Cons.** A parallel hierarchy of creators; for a single product it is often overkill — just
inject the product.

**Relations.** Often the first step towards Abstract Factory; it is a special case of Template
Method (the variable step is "create"); Prototype is the alternative that avoids the creator hierarchy.

### Abstract Factory

**Intent.** An interface creates a whole *family* of related objects, ensuring that the pieces
used together belong to the same family.

**Problem.** Objects that must be consistent with each other (reader, writer, style and image
mapper of a format) are chosen one by one, and sooner or later they get mixed up.

**When to apply it.**
- several `switch`es on the same discriminant (format, platform, theme) in different places;
- pairs of `OdfX`/`OoxmlX` classes that must always be chosen together;
- platform services with one implementation per operating system.

**Structure in C#.**

```csharp
public interface IPresentationFormatFamily
{
    string Extension { get; }
    IPresentationReader CreateReader();
    IPresentationWriter CreateWriter();
    IFidelityChecker CreateFidelityChecker();
}

public sealed class OdpFamily(IOdfPackage package) : IPresentationFormatFamily
{
    public string Extension => ".odp";
    public IPresentationReader CreateReader() => new OdpReader(package);
    public IPresentationWriter CreateWriter() => new OdpWriter(package);
    public IFidelityChecker CreateFidelityChecker() => new OdpFidelity();
}

public sealed class PptxFamily : IPresentationFormatFamily
{
    public string Extension => ".pptx";
    public IPresentationReader CreateReader() => new PptxReader();
    public IPresentationWriter CreateWriter() => new PptxWriter();
    public IFidelityChecker CreateFidelityChecker() => new PptxFidelity();
}

public sealed class PresentationFormatRegistry(IEnumerable<IPresentationFormatFamily> families)
{
    public IPresentationFormatFamily For(string path) =>
        families.First(f => path.EndsWith(f.Extension, StringComparison.OrdinalIgnoreCase));
}
```

**With DI.** Each family is registered as `IPresentationFormatFamily`; a registry receives
`IEnumerable<>` and chooses. Adding a family is one registration line.

**Pros.** Guaranteed consistency between the pieces; the client knows no concrete type.
**Cons.** Adding a product *type* to the family touches all the families.

**Relations.** Usually implemented with Factory Method; it may return Builders; families are often
singletons in the container; with Bridge the factory chooses the right implementation.

### Builder

**Intent.** Build a complex object step by step, separating the construction sequence
from the representation produced.

**Problem.** Constructors with ten optional parameters, or long and repeated construction code
(an XML tree, a ZIP package with a manifest) mixed with the logic.

**When to apply it.**
- telescoping constructor or many `null`s passed by position;
- multi-phase construction with ordering constraints (mimetype first, manifest at the end);
- the same process must produce different representations;
- long blocks of nested `new XElement(...)` / `new OpenXmlElement(...)`.

**Structure in C#.** Builder with state and fluent methods; for records, `with` already covers many
simple cases, and the Builder is needed when there is a sequence or validation.

```csharp
public sealed class OdfPackageBuilder
{
    private readonly List<(string Path, byte[] Data, string MediaType)> _entries = [];
    private string? _mimeType;

    public OdfPackageBuilder MimeType(string mimeType)
    {
        _mimeType = mimeType;
        return this;
    }

    public OdfPackageBuilder Entry(string path, byte[] data, string mediaType)
    {
        _entries.Add((path, data, mediaType));
        return this;
    }

    public OdfPackage Build()
    {
        if (_mimeType is null) throw new InvalidOperationException("Missing mimetype");
        var manifest = ManifestFor(_entries);
        return new OdfPackage(_mimeType, [.. _entries], manifest);
    }

    private static XDocument ManifestFor(IEnumerable<(string Path, byte[] Data, string MediaType)> entries) =>
        new(new XElement("manifest", entries.Select(e => new XElement("file-entry",
            new XAttribute("full-path", e.Path), new XAttribute("media-type", e.MediaType)))));
}
```

**With DI.** The Builder has state, so it is not registered as a singleton: a factory is injected
(`IOdfPackageBuilderFactory` or `Func<OdfPackageBuilder>`) or it is registered as transient. The
stateless dependencies (mappers) reach the factory.

**Pros.** Readable construction, validation in one place, ordering invariants respected.
**Cons.** More classes; pointless for simple objects where a record with `init` and `with` suffices.

**Relations.** An optional Director fixes the sequence; Composite is often the product; Abstract
Factory may return different builders per format.

### Prototype

**Intent.** Create new objects by copying an existing specimen, without depending on its class.

**Problem.** Duplicating a complex object from outside requires knowing its private fields and its
concrete type; and some copies require rules (new identifiers, references to rewrite).

**When to apply it.**
- "duplicate" on elements of variable type (slides, shapes, controls);
- copies that must regenerate ids or realign internal references;
- preset configurations to clone and tweak.

**Structure in C#.** With records the shallow copy is `with {}`; Prototype is needed when the
copy has rules. An injectable cloning service is better than a scattered `Clone()`.

```csharp
public interface ISlideCloner
{
    Slide Clone(Slide source);
}

public sealed class SlideCloner(IIdGenerator ids) : ISlideCloner
{
    public Slide Clone(Slide source)
    {
        var map = source.Elements.ToDictionary(e => e.Id, _ => ids.Next());
        return source with
        {
            Id = ids.Next(),
            SectionName = null,
            Elements = [.. source.Elements.Select(e => e with { Id = map[e.Id] })],
            Animations = [.. source.Animations.Select(a => a with { TargetId = map[a.TargetId] })],
        };
    }
}
```

**With DI.** The cloner is a stateless service (singleton); preset prototypes can
live in a registered catalogue.

**Pros.** Correct copies of complex objects without factory hierarchies.
**Cons.** Deep copy and circular references must be handled explicitly.

**Relations.** Alternative to Factory Method; Memento can use Prototype for snapshots;
useful with Composite and Decorator to duplicate already assembled structures.

### Singleton

**Intent.** Guarantee a single instance of a class and an access point to it.

**Problem.** Shared resources (command registry, translation catalogue) must be one
only. The classic Singleton, however, introduces global state, hides dependencies and makes tests fragile.

**When to apply it.** In this project **never in the classic form** (`static Instance`). The
container's `Singleton` lifetime is used: a single instance, but declared as a dependency.

**Structure in C#.**

```csharp
public interface ICommandRegistry
{
    void Register(CommandDefinition command);
    IReadOnlyList<CommandDefinition> All { get; }
}

public sealed class CommandRegistry : ICommandRegistry
{
    private readonly List<CommandDefinition> _commands = [];
    public void Register(CommandDefinition command) => _commands.Add(command);
    public IReadOnlyList<CommandDefinition> All => _commands;
}

services.AddSingleton<ICommandRegistry, CommandRegistry>();
```

**With DI.** `AddSingleton` replaces the pattern. Beware of thread safety and of not
injecting stateful scoped/transient services into a singleton (captive dependency).

**Pros.** One instance, explicit dependencies, replaceable in tests.
**Cons (classic form).** Global state, hidden coupling, initialisation order.

**Relations.** Facade and Abstract Factory are often singletons; Flyweight shares many immutable
instances, Singleton a single mutable one.

---

## Structural patterns

### Adapter

**Intent.** Make an object collaborate with an incompatible interface, translating the calls.

**Problem.** An external library (Jint, IronPython, Open XML SDK, Hunspell) has an API different from
the one our code expects.

**When to apply it.**
- third-party types appearing in the domain;
- repeated conversions at the boundaries (streams, enums, exceptions);
- several equivalent libraries to make interchangeable.

**Structure in C#.**

```csharp
public interface ISpellChecker
{
    bool IsCorrect(string word);
    IReadOnlyList<string> Suggest(string word);
}

public sealed class HunspellSpellChecker(WordList dictionary) : ISpellChecker
{
    public bool IsCorrect(string word) => dictionary.Check(word);
    public IReadOnlyList<string> Suggest(string word) => [.. dictionary.Suggest(word)];
}
```

**With DI.** The "our" interface lives in the domain project, the adapter in an infrastructure
project that references the library; the container binds them.

**Pros.** Isolates external dependencies; substitutability; tests with fakes.
**Cons.** One more layer; risk of adapters leaking library concepts.

**Relations.** Bridge is designed up front, Adapter is applied afterwards; Decorator keeps the interface,
Adapter changes it; Facade simplifies a whole subsystem, Adapter one object.

### Bridge

**Intent.** Separate an abstraction from its implementation into two independent hierarchies, so
that they grow without multiplying.

**Problem.** Two dimensions of variation (document type × drawing surface) produce
one class per combination.

**When to apply it.**
- class names composed of two axes (`PdfSlideRenderer`, `ScreenSheetRenderer`, …);
- per-platform implementations behind the same abstraction;
- combinatorial explosion in subclasses.

**Structure in C#.**

```csharp
public interface IPageCanvas
{
    void DrawPath(ShapeOutline outline, Paint paint);
    void DrawText(string text, Rect bounds, TextStyle style);
}

public abstract class PrintableDocument(IPageCanvas canvas)
{
    protected IPageCanvas Canvas { get; } = canvas;
    public abstract void PaintPage(int index);
}

public sealed class SheetPrintable(IPageCanvas canvas, Worksheet sheet) : PrintableDocument(canvas)
{
    public override void PaintPage(int index) => Canvas.DrawText(sheet.Name, Rect.Empty, TextStyle.Default);
}

public sealed class PdfCanvas : IPageCanvas
{
    public void DrawPath(ShapeOutline outline, Paint paint) { }
    public void DrawText(string text, Rect bounds, TextStyle style) { }
}
```

**With DI.** The implementation is resolved from the container (or from a per-platform provider) and
passed to the abstraction; the two hierarchies are registered separately.

**Pros.** Two independent axes; implementations replaceable at runtime.
**Cons.** More indirection; premature with a single axis of variation.

**Relations.** Abstract Factory creates the right implementation; Strategy has the same shape but
concerns an algorithm, Bridge a structure; Adapter is the after-the-fact remedy.

### Composite

**Intent.** Compose objects into trees and treat leaves and containers with the same interface.

**Problem.** Hierarchical structures (shape groups, menu tree, nested formulas) require
`if (x is Group)` everywhere.

**When to apply it.**
- `is`/`switch` checks to distinguish leaf from container;
- recursive operations (measure, draw, search) repeated by hand;
- naturally tree-shaped data.

**Structure in C#.** With immutable records the composite is a record containing a list of nodes.

```csharp
public abstract record DrawingNode(Rect Bounds)
{
    public abstract Rect Extent();
}

public sealed record ShapeNode(Rect Bounds, ShapeKind Kind) : DrawingNode(Bounds)
{
    public override Rect Extent() => Bounds;
}

public sealed record GroupNode(Rect Bounds, ImmutableArray<DrawingNode> Children) : DrawingNode(Bounds)
{
    public override Rect Extent() => Children.Select(c => c.Extent()).Aggregate(Rect.Union);
}
```

**With DI.** Nodes are data, not services: they are not registered. Operations that require
services move into a Visitor or into a service that walks the tree.

**Pros.** Uniform client; adding node types is simple; natural recursion.
**Cons.** Common interface sometimes too generic; constraints on children hard to express.

**Relations.** Iterator and Visitor traverse it; Builder builds it; Decorator has a similar structure
but a single child; Flyweight reduces repeated leaf nodes; Chain can follow the parents.

### Decorator

**Intent.** Add responsibilities to an object by wrapping it in another with the same
interface, without subclasses.

**Problem.** Cross-cutting behaviours (logging, caching, retries, permission checks) to
combine freely on a service.

**When to apply it.**
- subclasses created only to add logging/caching to a method;
- `if (logging)`/`if (cache)` inside a service;
- combinations of optional behaviours.

**Structure in C#.**

```csharp
public sealed class LoggingQueryRunner(IQueryRunner inner, ILogger<LoggingQueryRunner> log) : IQueryRunner
{
    public async Task<DataSnapshot> RunAsync(QueryDefinition query, CancellationToken token)
    {
        log.LogInformation("Running query on {Connection}", query.Connection);
        return await inner.RunAsync(query, token);
    }
}

services.AddSingleton<SqlQueryRunner>();
services.AddSingleton<IQueryRunner>(sp => new LoggingQueryRunner(
    sp.GetRequiredService<SqlQueryRunner>(),
    sp.GetRequiredService<ILogger<LoggingQueryRunner>>()));
```

**With DI.** The Microsoft container has no native decoration: you register the concrete type and then
the interface with a factory that wraps it (or use Scrutor's `Decorate`).

**Pros.** Behaviours composable at runtime, single responsibility.
**Cons.** The order of the layers matters; deep stacks are hard to read when debugging.

**Relations.** Proxy has the same shape but controls access instead of adding; Composite has
several children; Chain of Responsibility can stop, a Decorator normally does not; Strategy changes
the inside, Decorator the "skin".

### Facade

**Intent.** Offer a simple interface to a complex subsystem.

**Problem.** The client must orchestrate many services (engine, structure, history, UI
thread) for a conceptually single operation.

**When to apply it.**
- the same group of five services injected together in many places;
- an API exposed to the outside (scripts) that must not see the internals;
- identical sequences of calls repeated.

**Structure in C#.**

```csharp
public sealed class SheetScriptFacade(
    ICalculationEngine engine,
    IUndoService undo,
    IUiDispatcher dispatcher) : ISheetScriptApi
{
    public void Write(string address, string content) =>
        dispatcher.Invoke(() => undo.Record(() => engine.SetCell(CellAddress.Parse(address), content)));
}
```

**With DI.** The facade receives the services through the constructor and is registered by interface;
clients depend only on it.

**Pros.** Reduces coupling; a documentable entry point.
**Cons.** Risk of a "god object" that knows everything.

**Relations.** Mediator coordinates peers, Facade exposes a subsystem to the outside;
it is often a singleton; Abstract Factory can hide creation behind a facade.

### Flyweight

**Intent.** Share the common intrinsic state among many objects, keeping the extrinsic
state outside, to save memory.

**Problem.** Millions of similar objects (cells formatted the same way, glyphs) exhaust
memory if each carries all its own state.

**When to apply it.**
- many instances with the same values in some fields;
- repeated state that could be an index into a table;
- it is what `CellStyle` by index and the format styles already do.

**Structure in C#.**

```csharp
public sealed record CellStyle(string Font, double Size, bool Bold, Color Background);

public sealed class StyleTable
{
    private readonly Dictionary<CellStyle, int> _index = [];
    private readonly List<CellStyle> _styles = [];

    public int Intern(CellStyle style)
    {
        if (_index.TryGetValue(style, out var id)) return id;
        _styles.Add(style);
        return _index[style] = _styles.Count - 1;
    }

    public CellStyle this[int id] => _styles[id];
}
```

**With DI.** The table belongs to the document, not to the container: it is model state. The
flyweight must be immutable (records with structural equality are).

**Pros.** Large memory savings; cheap equality.
**Cons.** More complex code; extrinsic state must be passed explicitly.

**Relations.** Composite for shared leaves; Singleton is "a single instance", Flyweight "one instance
per value"; Factory for interning.

### Proxy

**Intent.** Provide a substitute that controls access to the real object: lazy loading,
remote access, permissions, caching.

**Problem.** The real object is expensive to create, remote or must be protected, but the client must not
know it.

**When to apply it.**
- lazy loading of heavy resources (dictionaries, script engines);
- access control (macro sandbox);
- remote objects with the same interface as local ones.

**Structure in C#.**

```csharp
public sealed class LazyThesaurus(Func<IThesaurus> load) : IThesaurus
{
    private readonly Lazy<IThesaurus> _real = new(load);
    public IReadOnlyList<string> Synonyms(string word) => _real.Value.Synonyms(word);
}

public sealed class SandboxedFileSystem(IFileSystem inner, IMacroPermissions permissions) : IFileSystem
{
    public Stream Open(string path) =>
        permissions.CanRead(path) ? inner.Open(path) : throw new UnauthorizedAccessException(path);
}
```

**With DI.** Registration with a factory as for the Decorator; injected `Lazy<T>` or `Func<T>`
cover the most common virtual proxy.

**Pros.** Transparent control of lifetime and access.
**Cons.** Hidden latency; possible confusion with Decorator.

**Relations.** Same structure as Decorator; Adapter changes the interface, Proxy does not; Facade
simplifies, Proxy substitutes.

---

## Behavioural patterns

### Chain of Responsibility

**Intent.** Pass a request along a chain of handlers; each one handles it or passes it to the
next.

**Problem.** Several candidates can handle a request (open a file, resolve a key,
recognise a format) and the choice must not be hard-wired into the client.

**When to apply it.**
- long `if/else if` of the "if you recognise it, do it" kind;
- a list of handlers that grows with the modules;
- a pipeline in which each one can stop.

**Structure in C#.** With DI the chain is an ordered `IEnumerable<IHandler>`.

```csharp
public interface IWorkspaceOpener
{
    bool CanOpen(string path);
    Task<OpenDocument> OpenAsync(string path);
}

public sealed class DocumentOpenRouter(IEnumerable<IWorkspaceOpener> openers)
{
    public Task<OpenDocument> OpenAsync(string path) =>
        (openers.FirstOrDefault(o => o.CanOpen(path))
            ?? throw new NotSupportedException(path)).OpenAsync(path);
}
```

**With DI.** Each handler is registered on the same interface; the registration order is
the order of the chain (or a priority is exposed).

**Pros.** Independent handlers, addable without touching the client.
**Cons.** No guarantee that someone handles it; implicit order.

**Relations.** Command as the request that travels; Composite (climbing up to parents); Decorator has
the same shape but does not stop; Strategy chooses *one* up front, Chain discovers it.

### Command

**Intent.** Turn a request into an object, so that it can be queued, logged,
undone and repeated.

**Problem.** Actions are direct calls, so they cannot be undone, repeated or
projected onto several surfaces (menu, ribbon, shortcuts, palette).

**When to apply it.**
- undo/redo is needed;
- the same action is reachable from several points of the interface;
- macros, queues or operation logs.

**Structure in C#.**

```csharp
public interface IUndoableAction
{
    void Undo();
    void Redo();
}

public sealed record SetCellAction(ICalculationEngine Engine, CellAddress Address, string Before, string After)
    : IUndoableAction
{
    public void Undo() => Engine.SetCell(Address, Before);
    public void Redo() => Engine.SetCell(Address, After);
}

public sealed class UndoStack
{
    private readonly Stack<IUndoableAction> _done = new();
    private readonly Stack<IUndoableAction> _undone = new();

    public void Push(IUndoableAction action)
    {
        _done.Push(action);
        _undone.Clear();
    }

    public void Undo()
    {
        if (_done.TryPop(out var action)) { action.Undo(); _undone.Push(action); }
    }
}
```

**With DI.** Command definitions (title, context, execution) are registered in the registry;
undoable actions are objects created on the fly with their dependencies captured.

**Pros.** Undoability, invoker/receiver decoupling, composition into transactions.
**Cons.** Many small classes; the "before" state must be captured correctly.

**Relations.** Memento for the "before" when no point inverse exists; Composite for
transactions (macro commands); Chain carries commands; Prototype to copy them; Strategy is "how",
Command is "what to do".

### Iterator

**Intent.** Traverse a collection without exposing its internal representation.

**Problem.** Complex structures (sparse cell store, object tree) force the
client to know how they are stored.

**When to apply it.**
- loops that know the implementation (nested dictionaries, internal indexes);
- several ways of traversing the same structure (by row, by column, only filled cells);
- lazy traversals over huge structures.

**Structure in C#.** In C# the iterator is `IEnumerable<T>` with `yield return`.

```csharp
public sealed class SparseCellStore
{
    private readonly SortedDictionary<int, SortedDictionary<int, CellValue>> _rows = [];

    public IEnumerable<(CellAddress Address, CellValue Value)> NonEmpty(CellRange range)
    {
        foreach (var (row, columns) in _rows)
        {
            if (row < range.Top || row > range.Bottom) continue;
            foreach (var (column, value) in columns)
                if (column >= range.Left && column <= range.Right)
                    yield return (new CellAddress(row, column), value);
        }
    }
}
```

**With DI.** Nothing specific: it is a property of the data structure. Beware of lazy
iterators with side effects (see `OdfVersionMapper.Write` in CLAUDE.md).

**Pros.** Encapsulation; multiple and lazy traversals.
**Cons.** Changes to the collection during iteration; surprising laziness.

**Relations.** Composite (tree traversal); Visitor combines with it to operate on the
elements; Memento can save an iterator's position.

### Mediator

**Intent.** Centralise communication between objects, which stop referring to each other and
talk only to the mediator.

**Problem.** Interface components (ribbon, grid, status bar, panels) reference
each other and every change propagates through a web of dependencies.

**When to apply it.**
- views holding references to other views;
- cross updates that are hard to follow;
- components that cannot be reused because they know their neighbours.

**Structure in C#.**

```csharp
public interface IActiveSheetContext
{
    event EventHandler? Changed;
    SheetSelection? Selection { get; }
    void Attach(ISheetTarget target);
    void Detach(ISheetTarget target);
}

public sealed class ActiveSheetContext : IActiveSheetContext
{
    private ISheetTarget? _target;
    public event EventHandler? Changed;
    public SheetSelection? Selection => _target?.Selection;

    public void Attach(ISheetTarget target)
    {
        _target = target;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Detach(ISheetTarget target)
    {
        if (ReferenceEquals(_target, target)) _target = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
```

**With DI.** The mediator is a singleton (or scoped per document) injected into all the
participants, which no longer know each other.

**Pros.** Reduces star-shaped coupling; reusable components.
**Cons.** The mediator tends to grow into a "god object".

**Relations.** Often implemented with Observer; Facade talks towards a subsystem, Mediator between
peers; Chain and Command to route requests.

### Memento

**Intent.** Capture an object's internal state so it can be restored, without violating its
encapsulation.

**Problem.** Operations without a point inverse (deleting rows turns formulas into `#REF!`)
require going back to the previous state as a whole.

**When to apply it.**
- undoing structural operations;
- snapshots, versions, autosave;
- "try and restore" (goal seek, solver).

**Structure in C#.** With immutable records the state memento is often the record itself.

```csharp
public sealed record SheetSnapshot(
    ImmutableDictionary<CellAddress, CellContent> Cells,
    SheetLayout Layout,
    ImmutableArray<EmbeddedObject> Objects);

public interface ISheetSnapshots
{
    SheetSnapshot Capture(Worksheet sheet);
    void Restore(Worksheet sheet, SheetSnapshot snapshot);
}

public sealed record SnapshotAction(ISheetSnapshots Snapshots, Worksheet Sheet, SheetSnapshot Before, SheetSnapshot After)
    : IUndoableAction
{
    public void Undo() => Snapshots.Restore(Sheet, Before);
    public void Redo() => Snapshots.Restore(Sheet, After);
}
```

**With DI.** The service that captures and restores is injectable; the memento is data.

**Pros.** Reliable restoration without exposing the internals.
**Cons.** Memory (large snapshots); it must be bounded or made incremental.

**Relations.** Command uses it for undo; Prototype to copy the state; Iterator to
save the position.

### Observer

**Intent.** An object automatically notifies its changes to its subscribers.

**Problem.** Views must follow the model (history, active document, settings) without
the model knowing them.

**When to apply it.**
- manual `Refresh()` calls scattered after every change;
- the model knows the views;
- several consumers of the same change.

**Structure in C#.** .NET events, `INotifyPropertyChanged`, or `IObservable<T>`.

```csharp
public sealed class Workspace
{
    public event EventHandler<OpenDocument?>? ActiveChanged;
    public event EventHandler<OpenDocument>? ActiveUpdated;

    public void Activate(OpenDocument? document) => ActiveChanged?.Invoke(this, document);
    public void Touch(OpenDocument document) => ActiveUpdated?.Invoke(this, document);
}

public sealed class StatusBarPresenter : IDisposable
{
    private readonly Workspace _workspace;

    public StatusBarPresenter(Workspace workspace)
    {
        _workspace = workspace;
        _workspace.ActiveUpdated += OnUpdated;
    }

    private void OnUpdated(object? sender, OpenDocument document) { }
    public void Dispose() => _workspace.ActiveUpdated -= OnUpdated;
}
```

**With DI.** The subject is a shared service; observers subscribe in the constructor and
unsubscribe in `Dispose` (the container disposes them). Notifications from outside the UI
thread must be marshalled through the dispatcher.

**Pros.** Minimal coupling; several consumers.
**Cons.** Memory leaks if you do not unsubscribe; cascades and unpredictable notification order
(see the `Changed` → reactivation defect in CLAUDE.md: distinct events for distinct facts).

**Relations.** Mediator is often implemented with Observer; Command can be the notified payload.

### State

**Intent.** An object changes behaviour when its internal state changes, as if it changed
class.

**Problem.** Methods full of `switch (_mode)` repeating the same discriminant (edit
mode, active drawing tool, slide show phase).

**When to apply it.**
- the same `switch` on a mode enum in several methods;
- transitions between modes that are scattered and hard to verify;
- pointer handlers that behave differently per tool.

**Structure in C#.**

```csharp
public interface IPointerTool
{
    IPointerTool Press(Point point);
    IPointerTool Move(Point point);
    IPointerTool Release(Point point);
}

public sealed class SelectTool(ISelection selection) : IPointerTool
{
    public IPointerTool Press(Point point) => selection.HitHandle(point) is { } handle
        ? new ResizeTool(selection, handle)
        : this;
    public IPointerTool Move(Point point) => this;
    public IPointerTool Release(Point point) => this;
}

public sealed class ResizeTool(ISelection selection, Handle handle) : IPointerTool
{
    public IPointerTool Press(Point point) => this;
    public IPointerTool Move(Point point) { selection.Resize(handle, point); return this; }
    public IPointerTool Release(Point point) => new SelectTool(selection);
}
```

**With DI.** States are created by the context (or by a factory) with the context's
dependencies; they are not registered individually if they have state.

**Pros.** Each mode in a class; explicit transitions.
**Cons.** Overkill for two trivial states.

**Relations.** Strategy has the same structure, but strategies do not know each other nor replace
themselves; Flyweight for stateless states; Singleton/DI for shared states.

### Strategy

**Intent.** Define a family of interchangeable algorithms behind an interface and choose
one at runtime.

**Problem.** Variants of an algorithm (chart painters, formats, database providers)
grown as branches of a `switch` or as parallel static methods.

**When to apply it.**
- a `switch` on the type/kind that calls different implementations;
- a static class with `DoForX`, `DoForY`, `DoForZ`;
- behaviour to configure or replace in tests.

**Structure in C#.**

```csharp
public interface IChartPlot
{
    bool CanPlot(ChartKind kind);
    void Plot(IDrawingSurface surface, ChartData data, Rect bounds);
}

public sealed class BarPlot : IChartPlot
{
    public bool CanPlot(ChartKind kind) => kind is ChartKind.Bar or ChartKind.Column;
    public void Plot(IDrawingSurface surface, ChartData data, Rect bounds) { }
}

public sealed class ChartRenderer(IEnumerable<IChartPlot> plots)
{
    public void Render(IDrawingSurface surface, ChartDefinition chart, ChartData data, Rect bounds) =>
        plots.First(p => p.CanPlot(chart.Kind)).Plot(surface, data, bounds);
}
```

**With DI.** Each strategy is registered on the same interface; selection happens via
`CanHandle`, by key (.NET 8+ keyed services: `AddKeyedSingleton`) or by configuration.

**Pros.** Open/Closed; strategies testable on their own; no `switch`.
**Cons.** The client must know the selection criterion; too many classes for one-line variants
(a delegate suffices there).

**Relations.** State is its version with transitions; Template Method varies by inheritance,
Strategy by composition; Decorator changes the outside; Bridge is its structural form.

### Template Method

**Intent.** Define the skeleton of an algorithm in the base class, leaving some steps
to subclasses without changing their sequence.

**Problem.** Almost identical duplicated algorithms (writing an ODS or ODP package, exporting to
different formats) that diverge over time.

**When to apply it.**
- two or more classes with long methods identical except for a few steps;
- a fixed sequence with variable steps (prepare, write content, write styles, close);
- optional hooks in a common procedure.

**Structure in C#.**

```csharp
public abstract class OdfDocumentWriter<TDocument>(IOdfPackageFactory packages)
{
    public void Write(Stream target, TDocument document)
    {
        var package = packages.Create(MimeType);
        package.Add("content.xml", BuildContent(document));
        package.Add("styles.xml", BuildStyles(document));
        WriteExtras(package, document);
        package.Save(target);
    }

    protected abstract string MimeType { get; }
    protected abstract XDocument BuildContent(TDocument document);
    protected abstract XDocument BuildStyles(TDocument document);
    protected virtual void WriteExtras(IOdfPackage package, TDocument document) { }
}

public sealed class OdpWriter(IOdfPackageFactory packages) : OdfDocumentWriter<Presentation>(packages)
{
    protected override string MimeType => "application/vnd.oasis.opendocument.presentation";
    protected override XDocument BuildContent(Presentation document) => new();
    protected override XDocument BuildStyles(Presentation document) => new();
}
```

**With DI.** Common dependencies pass through the base constructor; the concrete, sealed
subclasses are registered normally.

**Pros.** Eliminates duplication; the sequence lives in one place.
**Cons.** Rigid inheritance; the base can become a constraint (Liskov principle).

**Relations.** Factory Method is a specialised Template Method; Strategy is the alternative by
composition when the steps vary independently.

### Visitor

**Intent.** Separate operations from the structure they operate on, adding new operations to
a closed hierarchy without modifying it.

**Problem.** On a stable hierarchy of types (formula nodes, embedded objects) many
different operations are needed (evaluate, render as text, rewrite, serialise to ODF and OOXML), and
putting them all in the types bloats them.

**When to apply it.**
- the same `switch` on record type repeated in many services;
- closed hierarchy, open operations;
- serialisers for several formats of the same hierarchy.

**Structure in C#.** With records you can use classic double dispatch, or, on sealed
hierarchies, pattern matching as a "functional visitor" concentrated in a single service per
operation.

```csharp
public interface IFormulaVisitor<out T>
{
    T Visit(NumberNode node);
    T Visit(ReferenceNode node);
    T Visit(BinaryNode node);
}

public abstract record FormulaNode
{
    public abstract T Accept<T>(IFormulaVisitor<T> visitor);
}

public sealed record NumberNode(double Value) : FormulaNode
{
    public override T Accept<T>(IFormulaVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record ReferenceNode(CellAddress Address) : FormulaNode
{
    public override T Accept<T>(IFormulaVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record BinaryNode(string Operator, FormulaNode Left, FormulaNode Right) : FormulaNode
{
    public override T Accept<T>(IFormulaVisitor<T> visitor) => visitor.Visit(this);
}

public sealed class OdfFormulaRenderer : IFormulaVisitor<string>
{
    public string Visit(NumberNode node) => node.Value.ToString(CultureInfo.InvariantCulture);
    public string Visit(ReferenceNode node) => $"[.{node.Address}]";
    public string Visit(BinaryNode node) => $"{node.Left.Accept(this)}{node.Operator}{node.Right.Accept(this)}";
}
```

**With DI.** Stateless visitors are singleton services; stateful ones (rewriters with
context) are created by a factory.

**Pros.** New operations without touching the types; the logic of an operation in one place.
**Cons.** Adding a *type* requires modifying all the visitors; it can break
encapsulation.

**Relations.** Composite is the typical structure it visits; Iterator for traversal;
Command can be an applied visitor; Strategy chooses *which* visitor.

---

## Relations between patterns (summary)

- **Typical evolution**: you start from Factory Method and move towards Abstract Factory, Prototype or
  Builder when more flexibility is needed.
- **Same shape, different intent**: Bridge, State and Strategy (and partly Adapter) all rely
  on delegated composition; they differ in what varies and in who decides the substitution.
- **Wrapping**: Adapter changes the interface, Decorator enriches it, Proxy controls it, Facade
  simplifies it for a whole subsystem.
- **Senders and receivers**: Chain passes the request in sequence, Command encapsulates it, Mediator
  centralises, Observer broadcasts.
- **Trees**: Composite defines them, Builder builds them, Iterator walks them, Visitor applies
  operations to them, Flyweight shares their leaves.
- **Undo**: Command decides *what* to undo, Memento keeps *the state* to restore.
- **Inheritance versus composition**: Template Method and Factory Method vary by subclass;
  Strategy and Abstract Factory vary by injected object.

---

## Refactoring signals

| Signal in the code | Pattern to apply |
|---|---|
| `switch`/`if` on the type of a record repeated in several services | Visitor (closed hierarchy) or polymorphic method |
| `switch` on a "kind" that picks an algorithm | Strategies registered in DI |
| `if/else if` "who can handle this?" | Chain of Responsibility (`IEnumerable<IHandler>`) |
| `static` class with parallel `DoX`, `DoY`, `DoZ` | Strategy or abstract hierarchy + sealed |
| `static` class that builds, clones or transforms | Factory / Prototype / injectable service |
| `OdfX`/`OoxmlX` pairs always chosen together | Abstract Factory per format |
| `new Concrete()` inside a shared algorithm | Factory Method or injected factory |
| Telescoping constructor, many positional `null`s | Builder (or record with `init` + `with`) |
| Long blocks of nested `new XElement`/`OpenXmlElement` | Dedicated Builder |
| Two classes with almost identical methods | Template Method |
| The same `switch (_mode)` in several methods | State |
| Cross references between views | Mediator |
| `Refresh()` called by hand after every change | Observer |
| Operation not undoable or duplicated between menu and ribbon | Command |
| Point inverse impossible, "how it was before" is needed | Memento |
| `if (x is Group)` to distinguish leaf and container | Composite |
| Logging/caching/retries mixed with the logic | Decorator |
| Heavy resource created at startup, rarely used | Proxy (`Lazy<T>`) |
| External library types in the domain | Adapter |
| Combinatorial explosion of subclasses on two axes | Bridge |
| Millions of objects with repeated state | Flyweight (table by index) |
| Five services always injected together | Facade |
| `static Instance` | `AddSingleton` in the container |
| Loops that know the internal structure | Iterator (`IEnumerable` + `yield`) |

---

## Rules for PulseOffice

1. **`static` only for constants and truly pure mathematical computation** (no state, no I/O, no
   dependencies). Everything that builds, clones, chooses or transforms is a service behind
   an interface, registered in the container. No classic Singleton: `AddSingleton` is used.
2. **Families of variants become hierarchies or strategies**: an abstract base with `sealed`
   subclasses when they share state and algorithm; strategies registered in DI when they are
   interchangeable and chosen at runtime.
3. **Template Method** for shared algorithms with variable steps (ODF writers of ODS and ODP,
   exporters, pagination).
4. **Strategy or Chain of Responsibility** for selection by type: Strategy when the criterion is
   known (key, `CanHandle`), Chain when each handler decides whether to take the request
   (opening documents, format recognition, shortcuts).
5. **Visitor** for operations on closed hierarchies of records (formula nodes, embedded
   objects, math tree nodes): one visitor per operation and per format.
6. **Composite** for trees: shape groups, menus and ribbon, formulas, document structure.
7. **Builder** for complex construction of XML and OpenXML: packages, manifests, parts with
   relationships, subdocuments. Never page-long blocks of `new XElement` inside a mapper.
8. **Factory Method / Abstract Factory** for per-format families (ODF versus OOXML): reader,
   writer, mappers and fidelity check of the same family are chosen together, so
   rule 7 (the two formats aligned) has a shape in the code.
9. **Command** for undoable actions: every change to the document goes through the history;
   transactions as composite commands.
10. **Observer** for change notification, with **distinct events for distinct facts**
    (activation versus update) and guaranteed unsubscription in `Dispose`.
11. **Memento** for snapshots: structural undo, versions, restore, what-if trials.
    With immutable records the snapshot is often the record itself.
12. **Immutable records** as data (nodes, styles, settings), `sealed` services as
    behaviour; flyweights are records with structural equality interned by index.
13. **Adapter** for every external library (Jint, IronPython, Open XML SDK, Hunspell): the interface
    lives in the domain project, the adapter in the infrastructure project.

## Study notes from the refactoring.guru pages

All 22 pages (concept and C# example) were read one by one. The nuances that change the choices here:

- **Visitor**: double dispatch eliminates `switch`es on the type; the cost is that every new element
  class forces updating **all** the visitors. It suits closed and stable hierarchies
  (embedded objects, formula nodes, path segments), not those that grow every
  week. It combines with Composite to traverse trees (shape groups).
- **Template Method**: steps can be abstract, optional with a default behaviour, or empty
  **hooks**. The template method itself is not overridden. Risk: too many steps make it rigid; and
  a subclass that switches off a mandatory step violates Liskov.
- **Strategy vs Template Method**: composition and runtime choice versus static inheritance. The
  site admits that for **simple, stateless** variants an anonymous function suffices; in PulseOffice
  the user's rule nonetheless prefers classes, and functions remain only as adapters
  (`transform.Apply`) towards whoever asks for a `Func`.
- **State vs Strategy**: states may know each other and make the context change state; strategies
  do not. The capitalisation cycle and the interaction modes of the views are State.
- **Chain of Responsibility**: a base handler with the reference to the next; a request may
  remain unhandled, so the chain must have an explicit bottom (Null Object or exception).
- **Factory Method** is a Template Method specialised in creating; it evolves into Abstract Factory when
  the products are families that must go together (ODF versus OOXML).
- **Builder**: solves the telescoping constructor and builds composite trees without exposing intermediate
  states; the Director is optional but useful for reusing sequences.
- **Prototype**: a registry of named prototypes avoids configuration-only subclasses; watch out for
  circular references when cloning.
- **Singleton**: in .NET it is expressed with the container's `AddSingleton` lifetime, never with a public static
  field (the site itself points out the damage to testability and single responsibility).
- **Adapter** changes the interface of a single object; **Facade** simplifies a subsystem of many
  objects and risks becoming a god object; **Proxy** keeps the same interface and governs
  access (lazy, protection, caching, logging); **Decorator** keeps the interface and adds
  behaviour, with the order of the layers mattering.
- **Bridge**: two independent hierarchies (abstraction and implementation) instead of the Cartesian
  product of subclasses; useful where a concept varies both by format and by module.
- **Flyweight**: shared immutable intrinsic state, extrinsic state passed from outside; it is what
  `CellStyle` by index already does.
- **Command** with **Memento** for undo: the command saves the state before acting; the
  Memento is immutable and readable only by its originator.
- **Observer** coincides with C# `event`s; subscribers have no guaranteed order.
- **Mediator** moves communication between components into a single object; it too risks becoming
  a god object, so it should be split by area.
- **Iterator** in C# is `IEnumerable<T>` with `yield return`: it is used that way, without hand-written
  iterator classes, except for tree traversals that deserve an object of their own.
