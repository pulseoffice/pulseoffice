# Shared components of block A: C1, C5, C8, C9

All in `src/PulseOffice.UI.Controls`, with no dependency on any module. Headless tests in
`tests/PulseOffice.UI.Controls.Tests/SharedComponentsA1Tests.cs`.

## C1 — `MeasureField` (`Fields/`)

- `IMeasureUnits` / `MeasureUnits`: mm, cm, pt, in, px (lengths, base in points, 96 px per inch), %, °,
  s and ms, lines (with the aliases `lines`, `Zeilen`, `líneas`, `lignes`). `Convert` works only within the same
  dimension and rounds conversion noise to ten decimals.
- `IMeasureLabelReader` / `MeasureLabelReader`: from "Margin (mm)" it extracts the label "Margin" and the unit "mm", only if
  the unit is known (it leaves "Name (optional)" alone).
- `MeasureOptions(Unit, Minimum, Maximum, Step)` with `LargeStep` (default: step × 10), `Integer`, `Presets`,
  `ShowSlider`.
- `MeasureEntry`: UI-free logic. The value always stays **in the declared unit**; the displayed unit can be
  changed with `ShowIn` among the compatible ones. `Parse` accepts a unit written after the number ("2 cm" in a field in
  mm is 20) and answers `Accepted`, `Clamped` (outside the limits, brought back inside) or `Invalid`.
- `MeasureField(entry, initial, texts, metrics, icons?, accessibleName?, boxWidth)`: unit visible in the field (a
  button with a menu of the compatible units when there is more than one), ↑/↓ arrows by the step, ⇧ or Page Up/Down by
  the large step, mouse wheel when the field has focus, presets menu, optional slider, validation message
  below the field (also as `HelpText` for screen readers). `AttachScrubber(label)` makes the
  label draggable horizontally (4 px per step, ⇧ for the large step). Event `ValueCommitted`.
- **Automatic in `FormPromptView`** (and therefore in `TabbedForms`, `FormFieldsPanel`): a `NumberField` with `Unit`
  or with the unit in parentheses at the end of its label becomes a `MeasureField`; the label loses "(mm)" and
  becomes the drag handle. The returned value does not change. `NumberField` additionally has `LargeStep`,
  `Presets`, `ShowSlider`. Services come from `FormServices` (optional constructor parameter).
  Currently: 32 label keys with the unit in parentheses (Deck 18, image colour 5, page 5, Folio 2, others
  2) plus the Folio `NumberField`s that already declared `Unit` (character, paragraph).

## C5 — `HelpTip` (`Dialogs/`)

- `IHelpTextSplitter` / `HelpTextSplitter`: a short text (up to 110 characters, one line) stays whole; otherwise
  the first sentence (or first paragraph) is the subtitle and the rest is the detail. Texts are not rewritten.
- `HelpTip(text, texts, metrics, splitter?)`: grey subtitle plus a round "?" button (accessible name "More
  information", `HelpText` with the detail) that shows the rest in the hover tooltip and in a panel on
  click or via keyboard.
- Applied in `FormPromptView` (hence to all modules and all tabs of `TabbedForms`) and in
  `TextPromptView`.

## C8 — `IconChoiceGroup` (`Choices/`)

- `IconChoice(Key, Icon, Label)`; `IconChoiceGroup(choices, icons?, metrics, accessibleName, selected, showLabels)`.
- Segments in an inset frame; the active choice has an accent border. Arrows, Home, End move the choice;
  only one segment is a tab stop (roving focus). Each segment has an accessible name and a tooltip;
  without an icon in the catalogue it shows the label.
- `SelectedIndex`, `SelectedKey`, `SelectKey`, event `SelectionChanged`. In the modules: `IconChoiceField` (returns
  the index, like `ChoiceField`, so it replaces a drop-down without touching whoever reads the value).

## C9 — `SwatchStrip` (`Choices/`)

- `Swatch(Key, Label, Fill)` with `Foreground` and `Sample` ("Ab" for cell styles) and `Caption` (for captioned
  presets, "Normal", "Narrow"…).
- `SwatchStrip(swatches, texts, metrics, accessibleName, selectedKey, allowCustom, dismissal?)`: same keyboard as
  C8 (shared logic in `ExclusiveChoices`); "More…" opens the shared `ColorPickerView` and the chosen colour
  appears as a swatch next to the button. `Choose(colour)` selects the matching swatch if there is one.
- `SelectedKey`, `CustomColour`, `Value` (custom colour or the swatch's `Fill`), event `Changed`. In the modules:
  `SwatchStripField`.

New texts in the five languages: `measure.invalid`, `measure.invalidRange`, `measure.clamped`, `measure.unit`,
`measure.presets`, `help.more`, `swatch.more`. New styles in `PulseTheme.axaml`: `ToggleButton.iconChoice`,
`ToggleButton.swatch`, `Button.helpTip`.
