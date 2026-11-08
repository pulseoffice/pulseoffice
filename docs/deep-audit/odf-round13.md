# Deep audit round 13: Formats.Odf

## Coverage

The previous logs (`odf.md`, `formats-round2.md`, `formats-round3.md`, `package.md`) already record a full reading of
every file of `Formats.Odf` and `Formats.Package` except six. This round read those six in full and then made a
cross-cutting pass on repetition attributes (`number-rows-repeated`, `number-columns-repeated`) in every reader, the
main source of size amplification from a small hostile file.

| File | Lines read/total | Outcome |
|---|---|---|
| `OdfDimensionLines.cs` | 13/13 | no defect |
| `OdfObjectNameMapper.cs` | 20/20 | no defect |
| `OdfXml.cs` | 20/20 | no defect |
| `AutomaticStyleTable.cs` | 26/26 | no defect |
| `OdfAltTextMapper.cs` | 27/27 | minor, left: `Store` writes nothing when the node already has only a title or only a description |
| `Spreadsheet/OdsRepeatBudget.cs` | 35/35 | no defect (4 Mi cell budget, `ODF270`) |
| Repetition pass: `Tables/TableCells.cs`, `Spreadsheet/StreamingContent.cs`, `Spreadsheet/SheetPartReaders.cs`, `Odt/OdtBodyReading.cs` (tables), `Charts/ChartReading.cs`, `Shapes/DeckObjectCodecs.cs` (Deck tables) | relevant methods | two defects below; the others are capped (ODS budget, ODT 1024 columns and `TakeRepetition`) |

## Defects

| # | Severity | Where | Defect | Fix | Test (before -> after) |
|---|---|---|---|---|---|
| 1 | Serious (denial of service) | `Shapes/DeckObjectCodecs.cs:546-553`, `:634-650`, `:480` | Deck table reading capped each repetition at 1,000 but not the totals: N row elements x 1,000 rows and M cell elements x 1,000 columns, each cell read again. A few KB of ODP gave 400,000 rows, or (40 x 40 elements) 1.6 billion cells and a hung test host | totals capped: 1,000 rows, 1,000 columns (cells and column widths), 100,000 cells | `Righe_e_celle_ripetute_a_dismisura_in_una_tabella_di_Deck_restano_entro_un_tetto` (3 cases): 399,999 rows / host hang -> green |
| 2 | Medium (memory amplification) | `Charts/ChartReading.cs:222` (`InlineChartDocumentReader.Data`) | Rows of an inline chart table had no cap: each row of a few bytes with one cell repeated 1,024 times made 1,023 doubles | stop at 2^20 values | `Una_tabella_di_grafico_incorporata_con_celle_ripetute_a_dismisura_resta_entro_un_tetto`: 2,046,000 values -> green |

Both truncations happen only on tables far beyond what an editor writes (Deck tables of more than 1,000 rows or
columns, charts with more than a million values); they are not declared in `CheckFidelity` because the readers have no
diagnostic channel at that point and the data are hostile by construction.

## Remaining

- Minor `OdfAltTextMapper.Store` (see coverage).
- `InlineChartDocumentReader` ignores `number-rows-repeated` on chart rows (our writer never emits it).

## Tests run

`PulseOffice.Formats.Odf.Tests` in full. No live tests needed.
