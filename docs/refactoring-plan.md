# Refactoring plan towards design patterns

User rule (2026-10-02): use as many design patterns as possible according to
[refactoring.guru](https://refactoring.guru/design-patterns/csharp), split responsibilities
into classes, **fewer private static methods and more class hierarchies**. Study reference in
[design-patterns.md](design-patterns.md).

The code census (October 2026) counted about **80 static classes** and files with up to
23 private static methods, almost all organised around a `switch` on the type. The plan proceeds in
waves, each with its tests green before and after and its own commit: the structure changes,
not the behaviour.

## What may stay static
Only constants and pure mathematics without policy: the engine's numeric functions
(`Sheets.Engine/Evaluation/Functions/Numerics`), geometry and metrics (`OoxmlGeometry`,
`XlsxMetrics`, `Snapping`, `CanvasZoom`), design tokens (`DesignTokens`, `Palette`,
`PdfEncoding`), assembly markers and the composition root (`Program`,
`ServiceConfiguration`).

## Waves, in order

| # | Scope | Pattern | Target |
|---|---|---|---|
| 1 | `ImageFilters`, `TextCase`, `ListLabels` | Strategy, State | one filter, one transformation, one label formatter per class |
| 2 | Drawing objects: `DrawingObjectRenderer`, `ShapeStyling`, `OdfShapeMapper`, `XlsxShapeMapper` | Visitor, Composite | `IEmbeddedObjectVisitor<T>`, recursive groups |
| 3 | `SlideRenderer` | Strategy per element | `ISlideElementRenderer` |
| 4 | ODP/PPTX readers | Template Method + Chain of Responsibility | `DeckReaderBase`, chained `ISlideElementReader` |
| 5 | ODP/PPTX writers | Template Method + Builder + Strategy | `DeckWriterBase`, per-element writers, format vocabularies |
| 6 | Odf*/Xlsx* mapper family | Abstract Factory, Template Method | `ChartMapperBase`, mappers as instances |
| 7 | Form controls (codecs and painters) | Factory Method | one class per control type |
| 8 | Math formulas (`FormulaLayout`, MathML, OMML, drawing) | Visitor | `IFormulaNodeVisitor<T>` |
| 9 | Formula rewriting and evaluator | Visitor | `ExpressionRewriter` and subclasses |
| 10 | Static sheet operations (`SheetFill`, `SheetFilter`, `SheetSearch`, …) | Strategy | injected services |
| 11 | Static Deck logic (`MasterEditing`, `SlideResize`, `SlideSections`, `HeaderFooterXml`, …) | Builder, Strategy, Adapter | services and builders |
| 12 | Deck and Ledger commands | Command, Template Method, Memento | `DeckCommandBase`, per-action commands |
| 13 | `DeckView`, `SlideView`, `SheetGridView` | State, Observer, Mediator | interaction states, selection model |
| 14 | Infrastructure (`PythonDiscovery`, `VideoToolbox`, `PdfSigning`, remotes, menu layout) | Chain of Responsibility, Adapter, Builder, Facade | services |

**Closed: waves 1–28.** No static method remains in the mapper family.

Every closed wave is marked here and in the log of [CLAUDE.md](../CLAUDE.md).
