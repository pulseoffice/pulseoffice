# Round 176: shell leftovers of round 174

Area: the open items of round 174 (agent export of protected documents, opening the same file through a symbolic
link), document properties on the ODF side and their parity with OOXML, and every other place that writes a copy of a
document.

## Defects

1. **Serious: the MCP agent's export wrote a password-protected document in clear.** `ShellAgentWorkspace.ExportAsync`
   called the module's `WriteTo` directly. It now goes through `IDocumentCopyWriter` (new `Copies` member of
   `ShellAgentServices`), which encrypts the copy with the document's protection, or writes it without a password only
   when the format cannot carry one. PDF export is unchanged (a PDF is the user's explicit clear rendering).
   Test: `Round176AgentExportTests` (Desktop.Tests); the three positional constructions of `ShellAgentServices` in
   Desktop.Tests were updated.
2. **Medium: the same file opened twice through a symbolic link and its target.** `DocumentOpenRouter.FindOpen` and
   the in-progress guard compared `Path.GetFullPath`. The link-resolving canonicalisation of the recent list moved into
   the shared `CanonicalPaths` service, used by both the router and `RecentDocuments`.
   Test: `Round176OpenThroughLinkTests` (Shell.Tests; skipped where symbolic links cannot be created).
3. **Medium: confirming the properties dialog split a keyword holding a comma.** The dialog joined keywords with commas
   and split on commas, so a keyword like "Rome, Italy" (kept intact by ODF and, since round 174, by OOXML) became two
   as soon as any property was edited. `KeywordListText` (Document.Metadata) now holds the join and split rule (`; `
   when a keyword holds a comma, split on `;` when present) for both the dialog and the OOXML core properties mapper.
   Test: `Round176KeywordTextTests` (Formats.OpenXml.Tests).
4. **Medium: ODF meta wrote custom values that do not fit their kind as typed values.** A number entered as "3,5" was
   written as `meta:value-type="float"` with an invalid value (other readers reject or drop it), and a boolean "True"
   was not normalised; the OOXML writer already fell back to text. `OdfMetaMapper` now writes an unparsable number or
   date and a non-boolean as `string`, and booleans as `true`/`false`. Test: `Round176CustomPropertyTests`
   (Formats.Odf.Tests).
5. **Minor: reading ODF meta kept dates and editing cycles the file does not carry.** Created, modified and editing
   cycles were only set when present, unlike the OOXML reader, which clears them. Test: `Round176MetaReadTests`.
6. **Minor: saving a version wrote the document in clear to a temporary file.** The version snapshot went through a
   file in the system temporary folder (readable by other users with the default umask) before being stored inside the
   document; it is now written to memory through `WriteTo(document, path, stream)`.

## Reviewed without defects

Properties dialog: read-only refusal, one undo step with exact before and after, no step when nothing changed,
custom kinds kept; ODF title, subject, description, initial creator, creator, language, keywords (one element each)
and user-defined properties with kinds; send and template copies (round 174); Save a Copy goes through the encrypted
save path; recovery snapshots are encrypted; remote downloads store the server's bytes as they are.

## Open items

- `OdsWriter.WriteBody` spools the content body into a `DeleteOnClose` file in the system temporary folder, in clear
  and with the default permissions, while saving a protected spreadsheet. A fix needs an owner-only spool behind
  `PulseOffice.Platform` injected into the writer, which is built in about 160 places; left for a dedicated change.
- `AtomicFileWriter.WriteThroughPathAsync` uses a scratch file in the shared temporary folder for non-local
  destinations; the content is the final (possibly encrypted) output, but the file is not owner-only.
- Opening a stored version writes it to a temporary file so the router can open it; the version bytes are the
  document's native package in clear.
- PDF export and printing of a protected document produce clear output by design, without a warning.

## Not built or run

Per the cycle's rules the coordinator builds and tests after the join.
