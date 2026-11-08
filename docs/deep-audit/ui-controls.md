# Deep audit: PulseOffice.UI.Controls

Previous round (commits c9efaf91, f9d58bd8): styles panel, vector asset picker, DialogHost.

## Coverage (round 2: root and folders Dialogs, Services, Settings, Ribbon, Theming, Themes, Notifications, Prompts)

| File | Lines read/total | Outcome |
|---|---|---|
| ChoicePromptView.cs | 121/121 | ok |
| ColorField.cs | 143/143 | ok |
| ColorPickerView.cs | 339/339 | minor: the palette swatches only have the tooltip (hexadecimal), no automation name beyond that |
| ColorPromptView.cs | 39/39 | ok |
| CommandPaletteView.cs | 343/343 | ok |
| DialogHost.cs | 611/611 | fixed: close button without automation name; title of an underlying dialog left in the old language when it comes back on top |
| DialogChrome.cs | 69/69 | ok |
| DialogFooter.cs | 106/106 | ok |
| DialogParts.cs | 95/95 | ok |
| FlyoutDismissal.cs | 40/40 | ok |
| FormPromptView.cs | 342/342 | minor (remaining): errors of the «Browse» button swallowed without a message |
| NumberBox.cs | 165/165 | ok |
| NumberEntry.cs | 46/46 | ok |
| NumericBounds.cs | 34/34 | ok |
| FormFieldsPanel.cs | 25/25 | ok |
| IconView.cs | 56/56 | fixed: a new `Pen` at every drawing of every icon (ribbon, palette, lists) |
| PrintPromptView.cs | 223/223 | fixed: the preview's ‹ › buttons had no automation name |
| RangeField.cs | 101/101 | fixed (in References/ReferencePicking.cs): outside a modal the pick button raised `InvalidOperationException` in an async void handler |
| RecoveryPromptView.cs | 71/71 | ok |
| RibbonView.cs | 175/175 | ok |
| ShortcutsView.cs | 132/132 | fixed: search box without automation name |
| SidePanelView.cs | 181/181 | ok |
| StatisticsView.cs | 80/80 | ok |
| StatusBarView.cs | 243/243 | minor (latent, remaining): a segment's box stays bound to the first `StatusSegment` instance with that id; today no segment with `Accept` is removed and registered again |
| SymbolPickerView.cs | 130/130 | ok |
| TabbedDialogView.cs | 93/93 | ok |
| TabbedForms.cs | 72/72 | fixed: two tabs with the same key made confirmation fail (`ToDictionary` on duplicate keys), while the live preview kept the last |
| TextPromptView.cs | 116/116 | ok |
| TitleBar.cs | 240/240 | ok |
| PulseOffice.UI.Controls.csproj | 36/36 | ok |
| Services/DialogService.cs | 43/43 | fixed (in DialogHost.cs): `DialogOptions.AccessibleDescription`, set by ten callers, never reached the dialog; the frame did not even have the name |
| Services/DocumentFocusTarget.cs | 6/6 | ok |
| Services/RibbonStateNotifier.cs | 15/15 | ok |
| Services/ContextMenuFactory.cs | 27/27 | ok |
| Services/DocumentKeyScope.cs | 42/42 | ok |
| Services/InterfaceLock.cs | 44/44 | ok |
| Services/AutoCorrectProfileStore.cs | 126/126 | ok |
| Services/FileTypeNames.cs | 72/72 | ok |
| Services/AvaloniaFileDialogService.cs | 100/100 | minor (remaining): the `CancellationToken` does not reach the system picker (Avalonia's API does not accept it) |
| Services/EmbeddedObjectPicture.cs | 82/82 | ok |
| Services/RibbonMemory.cs | 76/76 | minor (remaining): write errors silenced, without log |
| Services/ShortcutBindingStore.cs | 64/64 | minor (remaining): as above |
| Services/SystemClipboard.cs | 127/127 | ok |
| Services/StatusBarService.cs | 268/268 | ok (the `Changed` event also comes from the timer: the view brings it back to the interface thread) |
| Dialogs/DialogRow.cs | 110/110 | ok |
| Dialogs/SteppedDialog.cs | 87/87 | minor (remaining): `StepStrip` with an empty list throws from `Math.Clamp` instead of with a clear message |
| Dialogs/HelpTip.cs | 137/137 | ok |
| Dialogs/TwoPaneDialogLayout.cs | 160/160 | ok |
| Dialogs/DialogLayoutMeasures.cs | 22/22 | ok (constants) |
| Dialogs/DialogSection.cs | 78/78 | ok |
| Dialogs/ChoiceOptionCard.cs | 89/89 | ok |
| Dialogs/FormSectionGrouping.cs | 71/71 | ok |
| Settings/SettingsView.cs | 431/431 | fixed: search and «advanced» switch without automation name |
| Settings/SwitchToggle.cs | 125/125 | minor (remaining): the peer does not announce the state change (`RaisePropertyChangedEvent`) |
| Settings/ScannerDeviceOptions.cs | 25/25 | ok |
| Settings/SettingRow.cs | 224/224 | ok |
| Settings/SettingEditors.cs | 455/455 | fixed: path «Browse» without name; minor (remaining): the colour is written to the settings at every movement in the spectrum |
| Ribbon/CommandSlot.cs | 170/170 | ok |
| Ribbon/RibbonSlot.cs | 83/83 | minor (remaining): arrow and main button of a split take the same automation name |
| Ribbon/ChoiceGrid.cs | 120/120 | ok |
| Ribbon/MenuSlot.cs | 160/160 | ok |
| Ribbon/ColorSplitSlot.cs | 104/104 | ok |
| Ribbon/ChoiceSlot.cs | 376/376 | fixed: the inline gallery rebuilt all tiles (thumbnails included) at every ribbon update, even without changes |
| Ribbon/TableGridSlot.cs | 195/195 | ok |
| Ribbon/RibbonGroupView.cs | 206/206 | ok |
| Ribbon/ColorPaletteView.cs | 174/174 | minor (remaining): swatches with only the hexadecimal as tooltip |
| Ribbon/RibbonBuildContext.cs | 149/149 | ok |
| Ribbon/RibbonItemFactory.cs | 222/222 | ok |
| Ribbon/BandGallerySlot.cs | 217/217 | minor (remaining): up/down/more arrows without their own name (they take the gallery title from `RibbonSlot.Announce`) |
| Ribbon/NumberBoxSlot.cs | 129/129 | ok |
| Ribbon/RibbonTabStrip.cs | 75/75 | fixed: two new `Pen`s at every drawing of every tab |
| Ribbon/FontFamilySlot.cs | 69/69 | ok |
| Ribbon/RibbonGroupsPanel.cs | 65/65 | ok |
| Ribbon/SampleTypeScale.cs | 18/18 | ok |
| Ribbon/BorderGallerySlot.cs | 211/211 | ok |
| Ribbon/RibbonBandPanel.cs | 57/57 | ok |
| Theming/PulseTheme.axaml | 705/705 | ok: the 15 keys used (`PulseAccent*`, `PulseSurface*`, `PulseText*`, `PulseBorder*`, `PulseHover`, `PulsePressed`, `PulseDanger`, `PulseFieldBorder`) are defined in the light and dark `Palette` or in `ThemePreference` |
| Theming/PulseTheme.axaml.cs | 9/9 | ok |
| Theming/ThemedElement.cs | 27/27 | ok |
| Theming/ThemedSurface.cs | 18/18 | ok |
| Theming/ResourceBrushes.cs | 19/19 | ok |
| Themes/ThemePaletteGallery.cs | 83/83 | minor (remaining): the thumbnail creates formatted text and brushes at every drawing (few elements, only on invalidation); the paper white is intended (page preview) |
| Notifications/Notification.cs | 52/52 | ok |
| Notifications/NotificationHistory.cs | 93/93 | ok |
| Notifications/NotificationToastHost.cs | 114/114 | ok |
| Notifications/NotificationQueue.cs | 90/90 | ok |
| Notifications/NotificationHistoryButton.cs | 119/119 | fixed: subscription to the catalogue's `LanguageChanged` (alive as long as the app) never unhooked; every closed window stayed retained |
| Notifications/NotificationHistoryView.cs | 185/185 | ok |
| Notifications/NotificationToastView.cs | 172/172 | ok |
| Notifications/NotificationLook.cs | 20/20 | ok |
| Notifications/NotificationCenter.cs | 126/126 | ok (timer events brought back to the interface thread by the host) |
| Prompts/ZoomPrompt.cs | 52/52 | ok |
| Prompts/ZoomView.cs | 250/250 | minor (remaining): the preview creates pens and brushes at every drawing (only at every change, inside a modal) |
| Prompts/DocumentPasswordPrompt.cs | 139/139 | ok |
| Prompts/TextInputRules.cs | 40/40 | ok |
| Prompts/QrCodePrompt.cs | 61/61 | ok |
| Prompts/SignaturesView.cs | 151/151 | fixed: «Browse» in an async void handler without protection; a picker error brought the app down |
| Prompts/VersionComparisonView.cs | 140/140 | ok |
| Prompts/VersionsView.cs | 134/134 | ok |
| Prompts/TextToolPrompts.cs | 114/114 | ok |

## Defects

### Medium
- DialogHost.cs (Present): coming back on top after the overlying dialog closed, the title was not recomputed and stayed in the previous language if the language had changed in the meantime. Fixed.
- IconView.cs (Render): allocation of a `Pen` at every drawing. The pen is now reused as long as the brush does not change. Fixed.

- References/ReferencePicking.cs:62 (from RangeField.cs:70): `RangeField` is also used in `FormFieldsPanel`, outside any
  modal; clicking the pick button called `IDialogCollapse.Collapse`, which throws with no dialog open, inside an
  `async void` handler: unhandled exception. Now picking does not start if there is no dialog on top. Fixed, with test.

- TabbedForms.cs:67: the final collection used `ToDictionary` on the values of all tabs and threw
  `ArgumentException` with a repeated key, while `FormValuesFeed.Current` keeps the last. Confirmation now uses the
  same collection as the preview. Fixed, with test.

- DialogHost.cs (Describe): the dialog frame had neither name nor description for accessibility; it now carries the
  title and `AccessibleDescription`, updated on language change. Fixed, with test.

- Ribbon/ChoiceSlot.cs (Update): `RibbonView.Update` calls `Refresh` on every group at every state change
  (selection, cursor); the inline gallery emptied and recreated the tiles and their thumbnails every time. It now
  rebuilds them only if the choices or the current entry change (record equality) or the theme changes. Fixed, with test.

- Notifications/NotificationHistoryButton.cs:71: `texts.LanguageChanged += …` in the constructor, never removed; the text
  catalogue is a singleton, so the button (and with it the status bar and the window) stayed reachable after
  closing. The subscription now follows attachment to and detachment from the visual tree. Fixed, with test.

- Prompts/SignaturesView.cs:70: `choose.Click += async …` without handling: an exception from the file picker
  reached the dispatcher as an unhandled exception. The button is now disabled during the choice and the picker's
  expected errors do not escape. Fixed, with test.

### Minor
- Ribbon/RibbonTabStrip.cs: pens allocated at every drawing. Now reused as long as the theme brush is the same. Fixed.
- PrintPromptView.cs: ‹ › buttons without name. Fixed.
- ShortcutsView.cs: search without name. Fixed.
- Settings/SettingsView.cs, Settings/SettingEditors.cs: search, «advanced» switch and «Browse» without name. Fixed.
- DialogHost.cs: the close button (icon only) had no automation name. Fixed with `prompt.close`.
- FormPromptView.cs: «Browse» exceptions silently swallowed. Remaining (no notification channel injected into the view).
- ColorPickerView.cs: swatches without automation name. Remaining.

### Note on the tests
- `DeepAuditDialogHostTests` (previous round) used `Session.Dispatch(async () => …)`: the overload chosen is
  `Dispatch<Task>`, which returns the inner task without awaiting it, so the assertions after the first `await`
  did not make the test fail. Rewritten without `await`. The same pattern appears in dozens of tests of
  `PulseOffice.Desktop.Tests` (outside the area, reported to the coordinator).

## Closing of round 2

- Coverage: root (30 files, 4,467 lines) and folders Dialogs, Services, Settings, Ribbon, Theming, Themes,
  Notifications, Prompts (69 files, 8,819 lines) read in full; in addition References/ReferencePicking.cs touched for the
  RangeField fix.
- Checks: `PulseOffice.UI.Controls.Tests` 296/296, `PulseOffice.Architecture.Tests` 38/38. Every fix has a test
  that failed before (verified by reverting the patch), except the automation names of printing, shortcuts and
  settings and the pens of the tab strip.
- Veil and keyboard (rule 6): DialogHost handles Esc (three consistent paths), Tab and the return of focus inside the
  frame; the window's shortcuts are already blocked by `ShellWindow` with `IsDialogOpen`. No modal without a veil
  found in this area.
- To try live: screen reader on a dialog (title and description announced); language change with two
  dialogs open and closing the one on top; ribbon styles gallery while the cursor moves (no
  flicker, selection updated); «Browse» in signatures; the reference pick button in a side panel.
- Remaining for the next UI.Controls rounds: the other folders (AutoCorrection, Borders, Charts, Choices, Dictionaries,
  Drawing, Exchange, Fields, Fills, Fonts, Forms, Formulas, Galleries, Links, Lists, Macros, Navigation, Objects, Pages,
  Panels, Preview, Printing, References, Review, Rows, Rulers, Search, Shapes, Shortcuts, Sorting, Spectrum, Spelling,
  StylePanels, Tables, Tabs, Templates, Text, TextFormat, Zoom).

## Round 3: Shapes, Objects, Charts, Text, TextFormat, Printing, Fonts, Forms

### Shapes (24 files, 3,263 lines, read in full)
ShapeStyleView 456/456, ShapeRenderer 360/360, FontworkGalleryView 344/344, ShapeTextEditor 303/303, Painting/Painters
251/251, ShapeBrushes 233/233, AssetPickerView 163/163, ObjectFrameChrome 139/139, FontworkDesigner 128/128,
AdjustHandleChrome 120/120, BuiltInAssetSource 111/111, ShapeGalleryView 95/95, FontworkRenderer 94/94,
OutlineGeometry 68/68, DrawingObjectRenderer 62/62, Painting/PaintVisitor 60/60, ShapeStyling 53/53, FontworkPrompt
48/48, ShapeIconCatalog 44/44, ObjectNudgeKeys 36/36, FontworkPreview 33/33, IconPainter 26/26, FontworkLooks 23/23,
ShapeBackdrop 13/13.

- Severe, ShapeRenderer.cs:163: with extrusion active, a shape whose larger side is under one point (flat
  horizontal line, shape shrunk by zoom) made `Math.Clamp(depth, 1, <1)` throw inside the drawing. Fixed, with
  test (`DeepAuditShapesTests`, fails without the fix).
- Medium, ShapeStyleView.cs:372: a non-finite value in the model (text size NaN from a corrupt file) made
  `(decimal)NaN` throw on opening the dialog. The field now starts from the minimum. Fixed (without test: the dialog
  requires a concrete `FontPickerFactory` that the tests cannot build).
- Minor, ShapeGalleryView.cs, AssetPickerView.cs: tiles, search and collection choice without automation name
  (only the tooltip). Fixed.
- Remaining (minor): ShapeRenderer allocates brushes and pens at every drawing for glow, extrusion (up to 48),
  arrows and bevel; AssetPreview and ShapePreview look up the accent resource at every drawing; ShapeStyling.Apply
  writes `Text` even when the shape has rich `Runs`, which drawing prefers, so text changed in the
  style dialog does not show on a shape with formatted text (to be decided with the model's owner).
- Protection and history: ShapeStyleView, FontworkGalleryView, AssetPickerView and ShapeGalleryView return a value
  with `Completed`; ShapeTextEditor emits `Committed` with original and modified. None writes to the model: the
  write goes through the module host.

### Objects (18 files, 2,292 lines, read in full)
ImageMapEditorView 460/460, ColorReplacerView 217/217, ColorCorrectionView 204/204, RasterImageFiles 192/192,
BitmapPixels 151/151, RasterImageOperations 147/147, MediaPlayerView 145/145, ContourEditorView 137/137,
PackagedObjectRenderer 118/118, ExternalLinksView 103/103, ImageLinks 70/70, ExternalEditWatcher 67/67, RasterRecolor
66/66, ImageContourTracer 58/58, AnimatedImageProbe 55/55, PackagedFilePrompt 43/43, AltTextPrompt 34/34,
EmbeddedFileLauncher 25/25.

- Severe, RasterImageFiles.cs:127: «Compress image» on an animated GIF re-encoded it as a single-frame PNG
  (smaller, hence accepted): the animation silently disappeared. A GIF with several frames now stays as is and
  the user sees «image not compressed». `RasterImageFiles` receives `IGifCodec` (already registered). Fixed, with test
  (`DeepAuditCompressTests`, fails without the fix).
- Medium, MediaPlayerView.cs:60 and 57: «Open» and «Play» were `async` handlers without handling: an unreadable file, a
  file too large for memory or a non-writable temporary folder reached the dispatcher as an unhandled
  exception. The error now goes to the status bar (`status.open.failed`) and «Open» is disabled during the choice.
  Fixed (without test: the view requires a real `IFileDialogService` to reach the branch).
- Minor, ImageMapEditorView.cs: target, description and list of areas without automation name. Fixed.
- Remaining (minor): ColorReplacerView and ColorCorrectionView create a new `WriteableBitmap` at every change for
  the preview without releasing the previous one; ImageMapCanvas and ContourCanvas allocate brushes and pens at every drawing;
  RasterImageFiles.PickAsync and PackagedFilePrompt read the whole file into memory without a size cap.
- Protection and history: all operations return a new `RasterImageObject` (or `null`) to the host;
  `ExternalEditAsync` delivers it with the host's `edited` callback. No direct write to the model.
- Security: EmbeddedFileLauncher refuses executable types (`ExecutableFileTypes` strips trailing dots and spaces) and
  writes only `Path.GetFileName` into a new temporary folder. No defect.

### Charts (19 files, 2,761 lines, read in full)
ChartPickerView 529/529, Plots/StatisticalPlot 361/361, Plots/BarPlot 211/211, Plots/LinePlot 195/195, ChartRenderer
192/192, Plots/MeterPlot 184/184, Plots/HierarchyPlot 147/147, Plots/RadarPlot 135/135, Plots/ChartPlot 112/112,
Plots/ScatterPlot 104/104, Plots/PiePlot 100/100, ChartLegendPainter 98/98, ChartPicker 92/92, Plots/FinancialPlot
86/86, ChartAxisPainter 80/80, ChartEditor 45/45, EmbeddedChartPrompt 40/40, ChartPreview 29/29, ChartText 21/21.

- Minor, ChartPickerView.cs: type and family tiles only had the tooltip; title, axis titles,
  legend, range and series arrangement had no name (the captions are separate TextBlocks). Fixed.
- Non-finite values: `ChartPlotBase.Value`/`IsPlotted` and `ChartText.Number` discard them; charts without `KeepsGaps`
  receive `WithoutGaps()`. No defect found on NaN, empty series or a single category.
- Remaining (minor): all plots allocate brushes and pens at every drawing (StatisticalPlot.DrawWaterfall and
  MeterPlot.Gradient also do `Color.Parse` for every bar or cell); chart texts in fixed black (correct on the
  white sheet, intended).
- Protection and history: ChartPicker returns `ChartSettings`, ChartEditor a new `ChartDefinition` or `null` if
  nothing changes; no write to the model.

### Text (13 files, 1,579 lines, read in full)
Rich/TextTypesetting 374/374, Editing/InlineTextEditor 319/319, TabStopsField 221/221, Rich/TextFrameComposer 206/206,
TextEditingRouter 100/100, TypingAutoCorrection 89/89, Editing/InlineImeClient 74/74, Rich/RichTextFramePainter 63/63,
LanguageChoices 44/44, Rich/HyperlinkAppearance 33/33, TextEditingCompanion 25/25, Editing/TextInputSink 22/22,
Editing/InlineEditingServices 9/9.

- Minor, TabStopsField.cs: position, alignment, fill character, decimal character and «✕» of the rows
  (and the new position) had no automation name. Fixed.
- Remaining (minor): TextCaretGeometry.Hit and Rows build a highlight geometry for every character of the
  paragraph at every click and every arrow (linear in the paragraph's length, acceptable in shape text
  boxes but not in a long text); RichTextFramePainter allocates a brush for every highlight at every drawing;
  InlineImeClient does not unhook from the inner client when the editor leaves the tree.
- Events: TypingAutoCorrection.Session unhooks in `Dispose`; InlineTextEditor stops blinking on detach.
- Protection and history: InlineTextEditor and TextEditingRouter act only on the hidden TextBox; the text reaches the
  model through the module host.

### TextFormat (13 files, 1,454 lines, read in full)
ListLevelsEditor 243/243, CharacterSampleView 207/207, CharacterFormatTabs 177/177, ParagraphFormatTabs 138/138,
ListFormat 127/127, ListSampleView 120/120, ParagraphSampleView 110/110, CharacterFormat 86/86, ListPresets 77/77,
ListFormatTabs 54/54, TextFormatPreviews 47/47, ParagraphFormat 43/43, FormAnswers 25/25.

- Medium, ListSampleView.cs:30: a list without levels made `IndexOutOfRange` throw inside `Render` (the live
  preview of Bullets and numbering, and `ListLevelsEditor.SelectLevel` threw `Math.Clamp(…, 0, -1)`). The preview
  is now empty and choosing the level does nothing. Fixed, with test (`DeepAuditTextFormatTests`, fails without).
- Medium, remaining (outside the area): in the Character dialog the «Default» language (null tag) cannot be chosen to
  remove a set language: `CharacterFormatTabs.Read` does `Tag ?? current.Language` and
  `FolioCharacterDialog.cs:55` discards the null anyway. «Unchanged» must be distinguished from «default» in Folio's
  change record; to be decided with the owner of Documents.UI.
- Remaining (minor): CharacterSampleView recreates a `FormattedText` for every character at every pass (up to nine for the
  outline) and at every drawing; the prefix and suffix fields of ListLevelsEditor take their name only from the row's
  label.
- Values: FormAnswers discards non-finite numbers and out-of-range choices; the numeric fields have limits.
- Protection and history: the dialogs return states (`CharacterFormatState`, `ParagraphFormatState`,
  `ListLevelsAnswer`); applying them is the module host's job.

### Printing (8 files, 1,535 lines, read in full)
PdfExportView 411/411, PrintPagesView 219/219, AvaloniaPageCanvas 212/212, DrawingReplay 200/200, PrintPreviewView
199/199, HeaderFooterZonesEditor 157/157, HeaderFooterTokens 96/96, PdfExportPrompt 41/41.

- Minor, PrintPreviewView.cs: the ⏮ ‹ › ⏭ − + buttons only had the tooltip. The tooltip is now also the
  automation name. Fixed. The two layout and zoom boxes remain without a name (a new key is needed in the
  five languages).
- Remaining (medium): AvaloniaPageCanvas.DrawImage creates a new `WriteableBitmap` at every drawing of the preview and does not
  release it (native memory retained until the finalizer; it cannot be released immediately because drawing is
  deferred: a per-image cache is needed); it copies rows assuming `RowBytes == width × 4`.
- Remaining (minor): PrintPagesView allocates the shadow brush and the border pen at every drawing.
- Security: PDF passwords live only in TextBoxes with a masking character and in the returned options;
  no log. With PDF/A archiving, security does not go through (`Security = null`).

### Fonts (8 files, 1,211 lines, read in full)
FontBrowserView 484/484, FontPicker 394/394, RuntimeFonts 126/126, DisplayFonts 53/53, FontDescriptions 50/50,
FontActivation 48/48, LicenseBadge 31/31, FontSubstitutes 25/25.

- Severe, FontBrowserView.cs:79 and 136: the font list was loaded in an `async` handler of
  `AttachedToVisualTree` without handling, and «Refresh» likewise; with the online catalogue unreachable
  (`HttpRequestException`, unreadable disk) the exception reached the dispatcher and «Refresh» stayed disabled. Loading
  now handles the same expected errors as FontPicker, shows the list that is there and re-enables the button.
  Fixed, with test (`DeepAuditFontBrowserTests`, fails without).
- Minor, FontBrowserView.cs: search, list, sample text and preview size without automation name. Fixed.
- Remaining (minor): closing the dialog with Esc does not cancel a download in progress (only «Cancel» calls
  `_transfer.Cancel`); `_transfer` is never released; in FontPicker and FontBrowserView the choices start with `_ =`
  and an unexpected error would be lost; FontPicker's search has no automation name.
- Protection and history: FontPicker emits `FamilyChanged`, FontBrowserView returns the name; applying it is the
  host's job. RuntimeFonts loads the documents' files into its own collection, with corrupt-file exceptions
  handled.

### Forms (10 files, 1,121 lines, read in full)
FormControlRenderer 352/352, FormControlDialogs 333/333, InterfaceModeField 85/85, PropertyFields 78/78,
SignatureLineRenderer 77/77, ChoiceItemsField 57/57, CalendarField 43/43, SignatureLinePrompt 43/43, FormDesignMode
28/28, FormControlNaming 25/25.

- Medium, FormControlDialogs.cs:266: in the properties of a numeric control (spin box, scroll
  bar) a minimum greater than the maximum went to the model as it was. The two bounds are now ordered. Fixed
  (without test: `Merge` is reachable only through the complete tabbed dialog with the control catalogue and macro
  events).
- Minor, PropertyFields.cs and ChoiceItemsField.cs: name, value and text of the rows only had the placeholder. Fixed.
- Remaining (minor): a control name already in use is not refused in the dialog (the host must do it);
  InterfaceModeSketch and the control painters allocate brushes and pens at every drawing; choices with index -1
  would become invalid enumeration values (today ChoiceField always starts from a valid index).
- Protection and history: FormControlDialogs returns `FormControlProperties` or the tab order;
  SignatureLinePrompt a new object. No direct write to the model.

## Closing of round 3

- Coverage: Shapes (24 files, 3,263 lines), Objects (18, 2,292), Charts (19, 2,761), Text (13, 1,579), TextFormat (13,
  1,454), Printing (8, 1,535), Fonts (8, 1,211), Forms (10, 1,121): 113 files, 15,216 lines, all read in full.
- Fixed: 3 severe (extrusion of tiny shapes, animated GIFs compressed to one frame, unreachable font
  catalogue), 5 medium (non-finite values in the style dialog, media player, list preview without
  levels, minimum and maximum of form controls, and the empty level choice), automation names in nine views.
- Remaining: «Default» language of the Character dialog not applicable (touches Documents.UI); print preview
  bitmaps not released; style text on shapes with formatted text; allocations in drawings; form control
  names not checked; font download not cancelled by Esc.
- Checks: `PulseOffice.UI.Controls.Tests` 300/300, `PulseOffice.Architecture.Tests` 38/38. Four new tests, each
  verified to fail without the fix.
- To try live: shape with extrusion reduced to a line; «Compress image» on an animated GIF in Deck;
  media player «Open» on a file without permissions; font catalogue without network («Refresh» becomes
  active again); screen reader on shape gallery, chart picker, tab stops, print preview.
- Remaining for the next UI.Controls rounds: AutoCorrection, Borders, Choices, Dictionaries, Drawing, Exchange, Fields,
  Fills, Formulas, Galleries, Links, Lists, Macros, Navigation, Pages, Panels, Preview, References, Review, Rows,
  Rulers, Search, Shortcuts, Sorting, Spectrum, Spelling, StylePanels, Tables, Tabs, Templates, Zoom.

## Round 4 (2026-11-02)

### Medium items remaining from round 3, closed
- Medium, Printing/AvaloniaPageCanvas.cs:158: `DrawImage` created a new `WriteableBitmap` at every drawing
  of the preview without releasing it and copied rows assuming `RowBytes == width × 4`. The factory (singleton) now
  holds a `PageImageBitmaps` with a `ConditionalWeakTable` per image: the bitmap is built only once,
  row by row according to `RowBytes`, and RGB data shorter than the declared dimensions is refused. Fixed (without
  automatic test: the difference is in retained native memory, not in the result).
- Medium, Shapes/ShapeStyling.cs:46: text changed in the style dialog went into `Text` but the rich `Runs`
  remained, which drawing prefers, so the change did not show. Now, if the text changes, the `Runs` are discarded;
  if it stays the same they are kept. Fixed, with test (`DeepAuditShapesTests`, fails without).
- Minor, Printing/PrintPreviewView.cs: layout and zoom boxes without automation name. New keys
  `print.preview.layoutName` and `print.preview.zoomName` in the five languages. Fixed.

### AutoCorrection (7 files, 1,054 lines, read in full)
AutoCorrectReplacementsPage 268/268, AutoCorrectExceptionsPage 181/181, AutoCorrectView 162/162, AutoCorrectChrome
162/162, ReplacementRowView 134/134, AutoCorrectPages 121/121, AutoCorrectImport 26/26.

- Minor: search, «Replace»/«With» fields (new entry and in-row edit), exception fields, language choice
  and option switches without automation name (only placeholder or label next to them). Fixed.
- Remaining (minor): «Import…» starts with `_ =` (the reader already handles expected file errors; an exception from the
  system dialog would be lost); `Refill` at every key of the search recreates all rows (list of a few
  thousand entries: acceptable).
- History and model: the view works on a draft (`AutoCorrectDraft`) and returns the profile with `Completed`;
  no direct write. Subscriptions to the drafts' `Changed` live as long as the dialog that owns them.

### Borders (7 files, 685 lines, read in full)
BorderEditor 200/200, BorderSchema 142/142, BorderFramePainter 100/100, BorderEditorModel 68/68, BorderSchemaGeometry
63/63, BorderGlyphs 61/61, BorderEditorField 51/51.

- Medium, BorderEditorModel.cs:51: `SetWidth(NaN)` passed `Math.Clamp` (which returns NaN) and ended in the stroke and
  the frame. Non-finite values are now ignored. Fixed, with test (`DeepAuditBorderFillTests`, fails without).
- Remaining (minor): BorderFramePainter allocates pens and brushes at every drawing; BorderGlyph recreates the inked frame
  at every drawing; a `BorderFrameScope` with empty `Dashes` would bring the model down (today no caller creates one).

### Choices (3 files, 394 lines, read in full)
SwatchStrip 196/196, IconChoiceGroup 126/126, ExclusiveChoices 72/72.

- No defect. Automation names on every element (`ExclusiveChoices.Add`), keyboard navigation with roving
  index, exclusive selection consistent even when clicking the already chosen element.

### Fills (3 files, 666 lines, read in full)
FillEditor 322/322, FillVisuals 175/175, FillEditorModel 169/169.

- Severe, Objects/RasterImageFiles.cs:78 (reached from FillEditor.cs:209, `async` handler of the «Choose image» click):
  a chosen but unreadable file (permissions, disconnected disk) threw `UnauthorizedAccessException`/`IOException`
  up to the dispatcher. `PickAsync` now reports it in the status bar («unreadable image») and returns null; this applies
  to all callers of `PickAsync`. Fixed, with test (`DeepAuditImagePickTests`, fails without).
- Medium, FillEditorModel.cs: non-finite transparency, angle, gradient stop position and hatch distance
  went into the fill (`Math.Clamp` and `%` preserve NaN). They are now ignored. Fixed, with test.
- Remaining (minor): GradientStopBar and FillSwatch allocate brushes and pens at every drawing.

### Dictionaries (2 files, 594 lines, read in full)
DictionaryManagerView 532/532, DictionaryDescriptions 62/62.

- Minor: search, «Refresh list» (only tooltip) and source choice without automation name. Fixed.
- Remaining (minor): `_closing` is not released; the actions start with `_ =` (the handler already deals with network, disk and
  JSON in `DictionaryManager`, so an unexpected exception would disappear without a message); the progress feedback
  rebuilds the whole detail at every package received.
- History and model: installations and removals touch only the user's dictionaries folder, not the document.

### Drawing (1 file, 169 lines, read in full)
FreeformSketch 169/169.

- No functional defect. Remaining (minor): `Paint` allocates pens at every drawing of the path preview.

### Exchange (1 file, 34 lines, read in full)
ExchangePasteChooser 34/34.

- No defect: the chosen index is brought back within bounds, the empty list shows a notice instead of an empty dialog.

### Fields (5 files, 1,004 lines, read in full)
MeasureField 402/402, AngleDial 279/279, SizeLockField 121/121, MeasureEntry 111/111, MeasureUnits 91/91.

- Medium, SizeLockField.cs:30 and 81: with the lock closed and one of the two measures at zero the ratio was 0 (or
  stayed the old one), and changing the width brought the height to `value / 0 = ∞`, i.e. to the maximum allowed.
  The ratio now exists only with both measures positive; without a ratio the lock does not drag the other measure
  and the new ratio is remembered. Fixed, with test (`SizeLockTests`, fails without).
- Minor, MeasureField.cs: the up and down arrows had no automation name. New keys `measure.increase` and
  `measure.decrease` in the five languages. Fixed.
- Remaining (minor): AngleDial allocates pens at every drawing (one per tick).

### Formulas (2 files, 330 lines, read in full)
FormulaRenderer 303/303, FormulaObjectRenderer 27/27.

- Medium (performance), FormulaObjectRenderer.cs:24: every drawing of an embedded formula repeated the parsing
  of the expression. Parsed formulas now stay in a small cache by text (at most 256, then it is emptied).
  Fixed (without test: the result is identical).
- Remaining (minor): FormulaRenderer measures the nodes several times per drawing (no layout cache: cost
  proportional to nodes × depth) and creates a `FormattedText` for every measurement; the `_hits` field makes the service
  non-reentrant (today no nested drawing).

### Galleries (4 files, 592 lines, read in full)
ThumbnailGallery 381/381, GalleryNavigator 77/77, GalleryPickerView 72/72, ThumbnailGalleryModel 62/62.

- No blocking defect. Remaining (minor): when filtering, the current choice may stay on a hidden tile, and
  Enter or «Confirm» return it anyway (it is the element chosen before the filter).

### Links (4 files, 600 lines, read in full)
HyperlinkView 417/417, LinkAddresses 109/109, LinkModel 40/40, HyperlinkPrompt 34/34.

- Severe, LinkAddresses.cs:91: `File` built `new Uri(path)` for every «rooted» path, and `Address` calls it
  at every key in the file field: typing `//` (or, on Windows, `C:` mid-typing) threw
  `UriFormatException` from the text handler and brought down the links dialog. `Uri.TryCreate` is now used
  and, if the path is not a file URI, the text is kept. Fixed, with test (`DeepAuditLinkAddressTests`, fails
  without).
- Remaining (minor): «Browse…» errors are ignored without a message; the mail recipient is not
  encoded (spaces go into the `mailto:` address).

### Lists (4 files, 673 lines, read in full)
ListEditorControl 224/224, Checklist 213/213, ChoiceListPopup 152/152, EditableList 84/84.

- Medium, ListEditorControl.cs:153: a row's box, losing focus after the list had been shortened,
  read `_list.Items[index]` out of bounds (`ArgumentOutOfRangeException` from the handler). The old row is now
  ignored. Fixed (without test: it needs a focus sequence that the headless engine does not reproduce).
- Minor: new entry field, row boxes, ↑ ↓ ✕ buttons (only tooltip) and checklist search
  without automation name. Fixed.
- Remaining (minor): `ChoiceListPopup` confirms with a tap even on the scroll bar; the «pick
  reference» button starts from an `async` handler (the picker handles its own errors).

### Macros (8 files, 760 lines, read in full)
MacroEventsPanel 235/235, MacroEventRow 203/203, DocumentMacroGate 127/127, MacroEventsChrome 87/87,
MacroEventsView 39/39, MacroEventsDialog 39/39, MacroEventsContracts 24/24, MacroEventsContextSource 6/6.

- Medium, MacroEventsPanel.cs:82: `Edit` was `async void`; an exception in the `finally` block (reconciliation of the
  assignments after the editor) would have reached the dispatcher. It is now `EditAsync`, returns a `Task` and the buttons
  start it explicitly. Fixed.
- Minor, MacroEventRow.cs: the macro choice for the event had no automation name. Fixed (name
  of the event).
- Remaining (minor): «New» saves the macro immediately into the document's library (`IMacroLibrary.Store`) even if the
  dialog is then cancelled: the assignment is undone, the macro stays (as in the reference suite's macro IDE); two macros asking
  for consent at the same instant open two requests.
- Security: `DocumentMacroGate` verifies the signature at every request, blocks broken signatures, remembers consent and refusal
  per document (weak table) and reports it in the status bar. No sensitive data in the logs.

### Navigation (2 files, 377 lines, read in full)
NavigatorPanel 289/289, WindowRegions 88/88.

- Medium, NavigatorPanel.cs:143: the panel subscribed to the document's `Changed` in the constructor and unhooked on
  detach, but did not resubscribe on reattach: closed and reopened (or moved between panels) the Navigator stopped
  following the document. It now subscribes on attach, unhooks on detach and reloads at every attach. Fixed, with
  test (`DeepAuditNavigatorTests`, fails without).
- Remaining (minor): the drag cursor does not go back to normal if the capture is lost midway.

### Pages (4 files, 517 lines, read in full)
PageMarginsEditor 216/216, ColumnLayoutEditor 134/134, LineNumberSampleView 84/84, PageNumberPositions 83/83.

- No severe defect. Remaining (minor): after a «mirrored» template chosen from the gallery the labels stay
  «Left/Right» (they are decided at construction); margins are not compared with the page size (the
  host does it); LineNumberSampleView creates a `FormattedText` per number at every drawing.

### Panels (4 files, 327 lines, read in full)
PanelActionBar 140/140, PanelChoiceBox 85/85, PanelSearchBox 79/79, PanelListFrame 23/23.

- No defect. Remaining (minor): the button that clears the search has no automation name (it does not receive focus;
  Esc does the same thing).

### Preview (3 files, 325 lines, read in full)
LivePreviewPane 150/150, PagePreview 124/124, PreviewDebounce 51/51.

- Minor, LivePreviewPane.cs: a delayed preview still pending when the dialog closed redrew a detached view
  asking the source for the state of a closed dialog. The timer now stops on detach.
  Fixed.
- Remaining (minor): PagePreview creates the paper brush and the watermark text at every drawing.

### References (3 files, 322 lines, read in full)
ReferencePickBar 202/202, ReferencePicking 83/83, ReferencePickButton 37/37.

- No defect: picking is exclusive (`_picking`), the dialog collapse and the sheet listening are closed
  with `using` even if picking is interrupted.

### Review (3 files, 475 lines, read in full)
ReviewPanel 381/381, ReviewSource 56/56, ReviewPanelRequests 38/38.

- Severe (protection bypassed), ReviewPanel.cs:66 and 249: the buttons respected `Abilities.Comment` and `Edit`, but Delete
  in the list deleted the selected comments and Ctrl+Enter in the reply replied or edited even without permission
  (read-only or protected document). Keyboard and buttons now go through the same conditions. Fixed, with test
  (`DeepAuditReviewPanelTests`, fails without).
- Medium, ReviewPanel.cs:122: same defect as the Navigator, the panel did not resubscribe to the document's `Changed`
  after being detached and reattached (the requests did). Fixed, with test.

### Rows (1 file, 162 lines, read in full)
ConditionRowsEditor 162/162.

- Minor: the ✕ button of every row only had the tooltip. Fixed.
- Remaining (minor): `Load` rebuilds all rows at every row added (quadratic, short lists).

### Rulers (3 files, 469 lines, read in full)
RulerStrip 364/364, ParagraphRuler 69/69, RulerTicks 36/36.

- Minor, RulerStrip.cs: if pointer capture was lost mid-drag (window switch), the
  marker stayed «in hand» with the dashed guide drawn until the next click. Losing the capture now
  cancels the drag. Fixed.
- Remaining (minor): `Render` creates a `FormattedText` per label, a pen and a geometry per marker at every
  drawing; Esc does not cancel a drag; the ruler is not reachable from the keyboard (the same quantities are in the
  paragraph dialogs).

### Search (1 file, 104 lines, read in full)
FindReplaceView 104/104.

- Minor: «Find» and «Replace with» only had the placeholder. Fixed.
- Protection: «Replace all» stays disabled and refused with a read-only target.

### Shortcuts (7 files, 937 lines, read in full)
ShortcutEditorView 569/569, KeyCaptureBox 91/91, ShortcutCapture 81/81, ShortcutGesture 69/69, ShortcutDispatcher
53/53, KeyCapsView 50/50, TypingChordGuard 24/24.

- Minor, ShortcutEditorView.cs:133: the category list read `_categories[i - 1]` even for the first, which
  would have thrown if the first category had been a module (today «All» is always first). Made robust.
- Minor: search, «search by key» button and key capture box without automation name. Fixed.
- Remaining (minor): `ShortcutGesture` keeps parser and notation in static fields (pre-existing, outside the scope of
  this round).

### Sorting (2 files, 396 lines, read in full)
SortLevelsView 281/281, SortLevelsModel 115/115.

- No defect: levels validated against existing columns, at least one level always present, «Add» chooses
  a free column (always existing as long as the levels are fewer than the columns), duplicates flagged.

### Spectrum (2 files, 241 lines, read in full)
SpectrumSurface 179/179, SpectrumColor 62/62.

- No defect: non-finite values brought back to 0, fractions with zero length handled. Remaining (minor): the gradients
  are recreated at every drawing.

### Spelling (4 files, 651 lines, read in full)
TextReviewPanel 381/381, TextReviewSession 157/157, SpellingRound 89/89, SpellingPrompt 24/24.

- No severe defect. The session resubscribes on attach and closes the request on detach; an empty replacement
  reading becomes «Ignore» (except for grammar, where an empty replacement is legitimate).
- Remaining (minor): if a request's `CancellationToken` is cancelled from another thread, `Changed` fires from
  that thread (today cancellations come from the interface); `_ = request;` in `Spell` is an inert statement.

### StylePanels (5 files, 782 lines, read in full)
StylesPanel 358/358, StylesPanelModel 203/203, StylePreviewPainter 119/119, StylesPanelSource 68/68,
NaturalTitleComparer 34/34.

- No defect: keyboard (Enter, Delete, F2), context menu and buttons all go through `Can(...)`; deletion
  asks for inline confirmation; the panel resubscribes correctly on reattach (model followed also
  for Navigator and Review).
- Remaining (minor): the natural comparison works per UTF-16 character (surrogate pairs are compared by halves).

### Tables (6 files, 901 lines, read in full)
TableInsertView 277/277, TableStyleGalleryView 240/240, TableSizeGrid 154/154, TableSizeModel 96/96,
TableInsertDesigner 96/96, TableInsertPrompt 38/38.

- Minor (performance), TableStyleGalleryView.cs:229: `TableLookPreview` created a brush for every cell at every
  drawing (rows × columns for every gallery thumbnail). The two inks are now created once per drawing.
  Fixed.
- Remaining (minor): TableSizeGrid allocates brushes and pens at every drawing.

### Tabs (3 files, 464 lines, read in full)
TabStrip 240/240, TabOverlays 143/143, PageTabStrip 81/81.

- No defect. Remaining (minor): `PageTabStrip.Show` gives the automation name with the index not yet brought back
  within bounds (with an index outside the list the name stays empty).

### Templates (2 files, 338 lines, read in full)
TemplateGalleryView 235/235, PageThumbnail 103/103.

- No defect: loading the template's pages (`TemplateLibrary.FirstPages`) already handles unreadable and
  corrupt files. Remaining (minor): the large thumbnail redraws the whole page at every `Render`.

### Zoom (3 files, 138 lines, read in full)
ZoomChoice 64/64, StatusZoomButton 57/57, ZoomLadder 17/17.

- No defect: non-finite values are brought back to 100 % in `ZoomLimits.Clamp`.

## Closing of round 4

- Coverage: AutoCorrection (7 files, 1,054 lines), Borders (7, 685), Choices (3, 394), Dictionaries (2, 594), Drawing
  (1, 169), Exchange (1, 34), Fields (5, 1,004), Fills (3, 666), Formulas (2, 330), Galleries (4, 592), Links (4, 600),
  Lists (4, 673), Macros (8, 760), Navigation (2, 377), Pages (4, 517), Panels (4, 327), Preview (3, 325), References
  (3, 322), Review (3, 475), Rows (1, 162), Rulers (3, 469), Search (1, 104), Shortcuts (7, 937), Sorting (2, 396),
  Spectrum (2, 241), Spelling (4, 651), StylePanels (5, 782), Tables (6, 901), Tabs (3, 464), Templates (2, 338), Zoom
  (3, 138): 110 files, 15,162 lines, all read in full.
- Fixed: 3 severe (unreadable image chosen from disk, file path not convertible in the links
  dialog, Delete and Ctrl+Enter of the Review panel without permission), 12 medium (print preview bitmaps, style text on formatted shapes, non-finite values in
  borders and fills, size lock with a measure at zero, formula cache, list row out
  of bounds, `async void` in macro events, Navigator and Review not resubscribing), minor ones on automation
  names (twenty-one views), ruler, delayed preview, table thumbnails.
- Touched outside the area (bare minimum): `src/PulseOffice.Desktop/Composition/FontComposition.cs`, one line to
  register `IPageImageBitmaps` (the rule on services built in fields requires it).
- Checks: `PulseOffice.UI.Controls.Tests` 310/310, `PulseOffice.Architecture.Tests` 38/38,
  `PulseOffice.Localization.Tests` 31/31. Eight new tests, each verified to fail without the fix.

## Overall outcome UI.Controls

- Four rounds: all folders of `src/PulseOffice.UI.Controls` read in full (rounds 1–3: root, Dialogs,
  Ribbon, Shapes, Objects, Charts, Text, TextFormat, Printing, Fonts, Forms and the others listed above; round 4: the
  thirty-one remaining folders).
- Recurring themes found and closed: `async` handlers without handling of expected errors (font catalogue, image
  choice, macro events); side panels that unhooked on detach without resubscribing
  (Navigator, Review; Styles and Text review were already right); keyboard shortcuts that bypassed the permissions
  respected by the buttons (Review); non-finite values accepted by the dialog models; automation names
  missing on boxes with only a placeholder.
- Only declared minor items remain: allocations of brushes, pens and texts in drawing methods (ruler, previews,
  shapes, border gallery, dials), some `_ =` on actions whose services already handle expected errors,
  `ShortcutGesture` with pre-existing static fields, Esc not cancelling the font download, form control
  names not checked in the dialog, «Default» language of the Character dialog (touches Documents.UI).
- To try live: fill «Choose image» on a file without permissions (notice in the status bar);
  typing `//` in the file field of the link dialog; Navigator and Review closed and reopened while editing
  the document (the list must follow); Delete in the Review panel on a read-only document (nothing must
  disappear); size lock with width 0; text changed in the style dialog on a shape with formatted
  text; print preview of a document with many images (stable memory while scrolling); screen reader
  on the boxes of autocorrect, dictionaries, shortcut keys and Find and replace.
