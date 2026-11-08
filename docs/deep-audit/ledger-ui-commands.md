# Deep audit: Ledger's commands (root of PulseOffice.Sheets.UI, Ledger*Commands.cs)

Area: 62 files, 10,699 lines. Read in full, in alphabetical order.

## Coverage

| File | Lines read/total | Outcome |
|---|---|---|
| LedgerAnalysisCommands.cs | 471/471 | no defect fixed; observations below |
| LedgerCalculationCommands.cs | 164/164 | no defects |
| LedgerCellFormatCommands.cs | 205/205 | **serious fixed**: increase/decrease decimals |
| LedgerCellShiftCommands.cs | 92/92 | no defects |
| LedgerChangeCommands.cs | 92/92 | **minor fixed**: redoing the switch-off bypassed protection |
| LedgerChartCommands.cs | 249/249 | **medium fixed**: «Delete chart» removed every object |
| LedgerCommands.cs | 24/24 | no defects |
| LedgerCommentCommands.cs | 133/133 | no defects |
| LedgerCompareCommands.cs | 117/117 | no defects |
| LedgerConditionCommands.cs | 129/129 | no defects |

| LedgerConditionalRuleCommands.cs | 157/157 | no defects |
| LedgerDataCommands.cs | 167/167 | observation (performance) |
| LedgerDataDialogCommands.cs | 234/234 | no defects (false alarm withdrawn, see observations) |
| LedgerDatabaseRangeCommands.cs | 126/126 | no defects |
| LedgerDocumentExtraCommands.cs | 156/156 | **minor fixed**: export image (extension, read-only) |

| LedgerDrawCommands.cs | 75/75 | no defects |
| LedgerEditDataCommands.cs | 148/148 | **medium fixed**: «Clear filter» (moved to LedgerFilterCommands) |
| LedgerExternalDataCommands.cs | 102/102 | no defects |
| LedgerFillCommands.cs | 205/205 | observation (rows hidden one by one) |
| LedgerFilterCommands.cs | 157/157 | **medium fixed**: the column menu ignored protection |

| LedgerFinalCommands.cs | 455/455 | **serious fixed**: thousands separator; **serious fixed**: delete pivot |
| LedgerFormCommands.cs | 81/81 | no defects |
| LedgerFormDialogCommands.cs | 100/100 | no defects |
| LedgerFormatDialogCommands.cs | 30/30 | no defects |
| LedgerFunctionCommands.cs | 64/64 | no defects |

| LedgerGalleryCommands.cs | 218/218 | no defects |
| LedgerImageCommands.cs | 336/336 | observation (optional `icons` dependency) |
| LedgerInsertCommands.cs | 147/147 | no defects (regression test added) |
| LedgerMacroCommands.cs | 39/39 | no defects |
| LedgerMergeCommands.cs | 61/61 | no defects |

| LedgerMiscCommands.cs | 238/238 | **serious fixed**: date field `=OGGI()`; **medium fixed**: undo lost the formula |
| LedgerNameCommands.cs | 215/215 | minor fixed: dead `Remove` method removed |
| LedgerObjectCommands.cs | 134/134 | no defects |
| LedgerObjectFileCommands.cs | 138/138 | minor fixed: media id from the file name |
| LedgerPaneCommands.cs | 98/98 | no defects |

| LedgerPanelCommands.cs | 32/32 | no defects |
| LedgerPartialCommands.cs | 291/291 | **medium fixed**: undo of the series lost the formulas; minor: standard filter and protection |
| LedgerPasteCommands.cs | 85/85 | no defects |
| LedgerPivotCommands.cs | 125/125 | minor fixed: error reported as success |
| LedgerPrintCommands.cs | 131/131 | no defects |

| LedgerRedactionCommands.cs | 95/95 | no defects |
| LedgerRibbonCommands.cs | 258/258 | observation (fixed € currency) |
| LedgerScriptCommands.cs | 54/54 | no defects |
| LedgerSelectionCommands.cs | 98/98 | no defects |
| LedgerSelectionExtraCommands.cs | 218/218 | **medium fixed**: visible rows/columns looked at the next one |


| LedgerShapeCommands.cs | 566/566 | minor fixed: SVG id from file made unique |
| LedgerSheetCommands.cs | 383/383 | no defects |
| LedgerSheetExtraCommands.cs | 369/369 | **medium fixed**: «Insert sheet from file» not undoable |
| LedgerSheetToolsCommands.cs | 160/160 | minor fixed: «Go to sheet» with an unbounded index |
| LedgerShellViewCommands.cs | 59/59 | no defects |

| LedgerSignatureCommands.cs | 56/56 | no defects |
| LedgerSparklineCommands.cs | 138/138 | no defects |
| LedgerSpellingCommands.cs | 320/320 | **medium fixed**: synonyms replaced a formula |
| LedgerStructureCommands.cs | 109/109 | no defects |
| LedgerStyleCommands.cs | 251/251 | observation (style name only on non-empty cells) |

| LedgerTableInsertCommands.cs | 112/112 | observation (table beyond the last row) |
| LedgerTailCommands.cs | 578/578 | observation (failed read writes an empty area) |
| LedgerTextCommands.cs | 77/77 | no defects |
| LedgerToolCommands.cs | 401/401 | minor fixed: dead `NullIfEmpty` helper removed; observations |
| LedgerViewCommands.cs | 44/44 | no defects |

| LedgerWebCommands.cs | 102/102 | **medium fixed**: external edit reloaded only on the first save; duplicate of the shared service |
| LedgerZoomCommands.cs | 30/30 | no defects |

Full coverage: 62 files out of 62, 10,699 lines read in full.

## Defects

### Serious (fixed)
- `LedgerCellFormatCommands.cs:193` (`WithDecimals`): «more/fewer decimals» rebuilt the code from scratch as
  `0.00`/`#,##0.00`, losing percentage (`0.00%` → `0.000`, 50% became 0.500), currency and literal text,
  scientific notation, negative/zero sections with colours, and turned dates into numbers. Now it changes only the
  decimal part of the first numeric placeholder of each section (outside quotes, square brackets, escapes) and
  leaves date/time/text/fraction formats unchanged. Test `LedgerCommandsAuditTests` (fails 6/7 before).

- `LedgerFinalCommands.cs:105` (`ToggleThousands`): «Thousands separator» replaced the format with id 4
  (`#,##0.00`) or 0 (Standard): a percentage, a currency, a format with its own colours or decimals became
  `#,##0.00` and, on the second click, Standard. Same defect as «Add decimal». Now it adds or removes only the
  grouping in the integer placeholder of each section; the code logic is now a single service
  `INumberFormatCodeEditor` (`NumberFormatCodeEditor.cs`), used by decimals and thousands. Tests
  `IlSeparatoreDelleMigliaiaNonCambiaIlRestoDelFormato`, `RiconosceIlSeparatoreDelleMigliaia`.
- `LedgerFinalCommands.cs:376` (`DeletePivot`): «Delete pivot table» always removed the *last* pivot of the
  document, whichever one was under the cursor, and left its cells in the sheet. Now it deletes the one that
  contains the cursor with `IPivotCoordinator.Remove` (which empties the area) inside an undoable checkpoint, and
  otherwise says so (`pivot.notUnderCursor`, five languages). To be tested live.

- `LedgerMiscCommands.cs:79`: «Date field» wrote into the model the formula `=OGGI()`, an Italian name that exists only
  in the display vocabulary: in the canonical (English) form it is an unknown name, hence `#NOME?` in every
  language and an ODS/XLSX file with a non-existent function. Now `=TODAY()`. Test
  `IlCampoDataScriveUnaFormulaCanonicaCheSiCalcola` (fails before).

### Medium (fixed)
- `LedgerChartCommands.cs:72-81`: «Delete chart» removed all objects anchored in the selection
  (images, shapes, data tables), not only charts. Now it filters `ChartDefinition`. Test
  `EliminaIlGraficoNonToglieImmaginiEFormeNellaSelezione`.

- `LedgerMiscCommands.cs:215,234`: undoing a mark/symbol/title/field inserted on a cell with a formula
  put back only the computed value: the formula was lost. Now undo restores the formula. Test
  `AnnullareUnSimboloInseritoRestituisceLaFormula` (fails before).

- `LedgerPartialCommands.cs:122-129` («Series»): same defect as Misc, undo wrote only the value and the
  formulas overwritten by the series were lost. Now it restores the formula. No automatic test (the command
  opens an Avalonia view); the fix mirrors the one tested in `AnnullareUnSimboloInseritoRestituisceLaFormula`.

- `LedgerSelectionExtraCommands.cs:178,203`: «Select only visible rows/columns» checked `IsRowHidden(row + 1)`
  and `IsColumnHidden(column + 1)` on indices already zero-based: it looked at the next row/column, so it kept a
  hidden row and discarded the visible one before it. Test `SelezionaRigheVisibiliGuardaLaRigaGiustaNonLaSuccessiva`
  (fails before).

- `LedgerSheetExtraCommands.cs:101` («Insert sheet from file»): added the sheets outside the history, ⌘Z did not
  remove them. Now the addition is inside a document checkpoint (`Checkpointed`). No automatic test (it opens the
  file picker).
- `LedgerSpellingCommands.cs:190` («Synonyms»): on a cell with a formula yielding text it replaced the formula with the
  synonym, and undo put back only the value. The spell check already skips formulas; now synonyms do
  too. Test `IlDizionarioDeiSinonimiNonSostituisceUnaFormula` (fails before).

- `LedgerWebCommands.cs:63-91` («Edit with external program»): the reload looked for the object with
  `IndexOf(placed)` on the original reference; after the first save in the external editor the object had already been
  replaced, so every later save was silently ignored (and, after switching sheet, the first one too).
  Moreover the command duplicated `IRasterImageOperations.ExternalEditAsync`, already used by Folio and Deck (rule 10). Now
  it delegates to the shared service and replaces the object by identifier, on whatever sheet it is, with undo by
  identifier. Test `LaModificaEsternaRicaricaOgniSalvataggioNonSoloIlPrimo`. In the shared service
  (`RasterImageOperations`, outside the area, bare minimum) the reloaded image now also updates
  `PixelWidth`/`PixelHeight`, which kept the old values.

### Medium (fixes of the earlier observations)
- `LedgerDataCommands.cs:124` («Remove duplicates»): deleted one row at a time; now by contiguous blocks
  (`Blocks`), from the bottom. On sorted data, where duplicates are adjacent, the calls to `DeleteRows` go from one
  per duplicate to one per group. Test `RimuoviDuplicatiCancellaPerBlocchiContigui` (fails before: 3 calls
  instead of 2).
- Extension-from-image-type table repeated in `RasterImageFiles` (two tables), `RasterImageOperations`,
  `LedgerWebCommands`, `LedgerDocumentExtraCommands`, with differences (`.tiff`/`.tif`, WebP missing in two): now a
  single service `IRasterMediaTypes` (`PulseOffice.Drawing/Assets/RasterMediaTypes.cs`, no dependency on
  Avalonia), registered in `CommandingComposition` and injected.
- Optional dependencies `?? new …()` / `= null` in the constructors removed: `LedgerConditionalRuleCommands`
  (`IConditionalRuleRemoval`), `LedgerDataDialogCommands` (`ISheetRangeReferences`), `LedgerImageCommands`
  (`IIconCatalog`): now injection only (they were already registered in the container).

### Minor (fixed)
- `LedgerShapeCommands.cs:517`: the SVG inserted from file took the file name as id, repeatable between two objects
  (same defect already fixed for media); now a Guid.
- `LedgerSheetToolsCommands.cs:155` («Go to sheet»): selected with the bounded index but wrote in the status
  bar `names[index]` with the raw index; now the index is bounded only once.
- `LedgerPartialCommands.cs:208` («Standard filter»): applied the filter also on a protected sheet without
  `AllowFilter`; now it refuses like the autofilter.
- `LedgerPivotCommands.cs:103`: an error while creating the pivot appeared with severity «success».
- `LedgerObjectFileCommands.cs:92`: the media chosen from file in the player took as id the file name without
  extension, which can repeat between two objects; now a Guid as everywhere. `LedgerNameCommands.cs`: removed the
  private `Remove` method never called; `LedgerMiscCommands.cs`: removed dead `_ = sheet;` and `_ = engine;`.
- `LedgerDocumentExtraCommands.cs:148`: «Export image» proposed `.jpg` for TIFF and WebP (and for PNG only by
  chance), and the command was disabled in read-only documents, where exporting modifies nothing. Now the table
  covers jpeg/gif/bmp/tiff/webp with PNG as fallback and the command stays active in read-only mode. Test
  `EsportaImmagineProponeLEstensioneDelSuoFormato` (fails before).
- `LedgerChangeCommands.cs:27`: redoing (⌘⇧Z) «switch off recording» after protecting the changes bypassed
  the `IsProtected` check; now the redone step respects it. Test `RipetereLoSpegnimentoNonScavalca…`.

### Minor (not fixed, observations)
- False alarm withdrawn: the direct writes `sheet[address] = cell` (special filter with copy, inserted fields,
  links) do **not** bypass the engine, which listens to `Worksheet.ContentChanged`; verified with the test
  `InserireUnCampoSuUnaFormulaPassaDalMotoreAncheAnnullando`, which passes on the original code too. The change to
  `LedgerDataDialogCommands` of commit d66a5fc8 was reverted to the previous state.
- `LedgerDataCommands.cs:124`: «Remove duplicates» deletes rows one at a time with `DeleteRows`: on thousands of
  duplicates it is quadratic. It should be grouped by contiguous blocks.
- `LedgerFillCommands.cs:36,47`: «Hide rows/columns» calls `HideRow` for every row: with whole columns
  selected that is a million calls. **Not fixed**: the cost is not in the calls but in the model,
  `SheetLayout` keeps one entry per hidden row (`_rowHeights`, `_hiddenRowSizes`), so even a `HideRows`
  would have to write a million entries. The real fix is an interval representation of hidden rows
  in `PulseOffice.Sheets` (core, outside this area), with the ODS/XLSX readers/writers that already reason in
  repetitions. To be planned separately.
- `LedgerPartialCommands.cs:284` and `LedgerChangeCommands`: structure protection (and change
  recording) are history steps; undoing past the protection removes it without a password. It is the same choice
  for all protections of the app: it should be decided once (the reference suite does not put them in the history).
- `LedgerRibbonCommands.cs:30-31`: the ribbon's currency and accounting have the euro fixed, whatever the language
  or the currency of the document.
- `LedgerSelectionExtraCommands.cs:150-217`: with a single-rectangle selection, «unprotected cells» and
  «visible rows» select the rectangle that contains them, excluded cells in between included.
- `LedgerStyleCommands.cs:188`: when applying a named style, the name is recorded only on the non-empty cells of the
  selection (the format goes on all of them): a formatted empty cell does not count as «of that style».
- `LedgerShapeCommands.cs:209-295`: connectors, alignment and order do not check `ObjectsEditable`; they are stopped by the
  `LedgerProtectionGuard` guard (rule 19), so no forbidden effect.
- `LedgerTailCommands.cs:212-223,306,526`: if opening succeeds but reading the table fails, `Read`
  reports it and returns an empty table, which is then written anyway into the linked area (emptying it) and the refresh
  counts it as updated. The error is visible and undoable; rare case.
- `LedgerTableInsertCommands.cs:59`, `LedgerToolCommands.cs:327-340` (statistics): the destination area is not
  bounded to the last row/column of the sheet; near the edge out-of-bounds addresses are built.
- `LedgerToolCommands.cs:180`: «Text to columns» checks protection only on the selection, not on the columns to the
  right where it writes; they are stopped anyway by the effect guard (rule 19).
- `LedgerAnalysisCommands.cs:41-149`: trace arrows modified outside the history (as in the reference suite: they are
  view state, not content; no fix).

## Overall outcome for the commands
- Coverage: 62 files out of 62 (10,699 lines), all read in full with Read.
- Serious fixed: 4 (decimals, thousands separator, delete pivot, date field `=OGGI()`).
- Medium fixed: 10 (delete chart, undo of inserted marks, undo of the series, visible rows/columns,
  clear filter, column menu and protection, undoable sheet from file, synonyms on a formula, external
  edit reloaded only once, remove duplicates by blocks) plus two structural fixes (shared
  image type service, injection only in constructors).
- Minor fixed: 9. Left as observations: hiding rows as intervals (requires the core), fixed currency of the
  ribbon, single-rectangle selection, protection in the history (user decision, not touched), edge cases at the
  sheet border and failed external read.

## Live tests
- Image in Ledger, «Edit with external program»: save twice in the editor, both versions must
  reach the sheet; ⌘Z goes back to the first.
- «Insert sheet from file» (ODS): ⌘Z must remove the added sheets.
- Cell with `="casa"`, «Synonyms»: it must say there is no word, the formula stays.
- Format 0.00% on a cell, «Add decimal» and «Delete decimal»: it must stay a percentage.
