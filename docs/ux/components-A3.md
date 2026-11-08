# Block A components — C4: TwoPaneDialogLayout and DialogSection

Folder: `src/PulseOffice.UI.Controls/Dialogs/`.

## Pieces

| Type | Role |
|---|---|
| `TwoPaneDialogLayout` | Optional explanation at the top, settings on the left in sections, optional preview (`Preview`, any `Control`) on the right in an inset panel. Below `PreviewBelowUnder` (560 px) the preview moves under the settings. Without a preview the right column does not exist: no empty space. `SuggestedWidth` estimates the dialog width from the content. |
| `DialogSection` | Small upper-case title (like the Deck theme) and rows. `Add(label, editor, kind, editorWidth, alignTop)`. |
| `DialogRow` | Two-column "label · control" row. The label column is a `SharedSizeGroup` shared by all sections of the layout, so controls align on the same vertical. Kinds: `Labelled`, `Indented` (check boxes: in the control column), `Wide` (spans both columns). Below `StackRowsUnder` (340 px) the label goes above the field. Sets the control's `AutomationProperties.Name` and `LabeledBy`. |
| `FormSectionGrouping` | Groups a form's fields into sections (see below). |
| `ChoiceOptionCard` | Option card of `ChoicePromptView`: icon (or initial in an accent circle), title, one line of description. |
| `IDialogSizeHint` | (in `Services`) content that knows its own width declares it; `DialogHost` uses it instead of `DialogOptions.PreferredWidth`. |

Slots for the other blocks' components: a `MeasureField`, `IconChoiceGroup` or `SwatchStrip` is passed as the editor of `DialogSection.Add`; a `LivePreviewPane` or `PagePreview` is assigned to `TwoPaneDialogLayout.Preview` (or `FormPromptView.Preview`).

## FormPromptView and TabbedForms

- All existing forms move to the two-column layout with no change to callers.
- **Declared sections**: `FormField.Section` (`init` property) or the constructor
  `new FormPromptView(explanation, texts, [new FormSection("Title", fields), …])`. A section with an empty title is a section without a header.
- **Default grouping** for fields without a section: fields stay in the given order; a run of at least two check boxes following other fields becomes the "Options" section (`form.section.options`). An isolated check box stays with its neighbouring fields, so a check box that controls the next field is not separated from it.
- **Preview**: `FormPromptView.Preview = control`; in `TabbedForms` `new FormTab(...) { Preview = () => control }`.
- **Width**: `FormPromptView` implements `IDialogSizeHint`; the dialog width is that of the content (widest label + widest control + preview), between 360 and 920 px. If the form contains a `CustomFormField` and no preview, the width requested by the caller is kept (the custom editor may want more). `TabbedForms` uses the maximum across tabs with the same rule.
- `TabbedDialogView` no longer imposes a minimum height of 260 px: the height is that of the tallest tab.
- Indicative control widths: text 240, multi-line text 320 (label on top), path 340, number 170 (left-aligned), drop-down according to the longest item (150–320), font 240.

## ChoicePromptView

- `PromptChoice` receives `Description` and `Icon` (a key of the `IIconCatalog`, passed as the last optional constructor argument).
- If there are at least three non-cancel choices, or one has a description or icon, the choices become option cards; cancel stays in the footer on the left. Otherwise (yes/no, Save/Don't save) they remain footer buttons as before: there the footer is the expected form.
- Keyboard: the default card takes focus on opening and is `IsDefault` (Enter); up/down/left/right arrows move from one card to the next (wrapping); Space or Enter chooses.
- Used with descriptions in "How do you want to protect it?" (`DocumentPasswordPrompt`).

## Tests

`tests/PulseOffice.UI.Controls.Tests/TwoPaneDialogLayoutTests.cs` (alignment, narrow stacking, default and declared sections, width, preview on the right and below, no empty panel, tab order) and `ChoicePromptCardsTests.cs` (cards, focus, arrows, choice, fallback to the footer).
