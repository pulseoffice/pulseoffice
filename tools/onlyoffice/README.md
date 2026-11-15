# onlyoffice

A second, optional OOXML reader for cross-checks: OnlyOffice's bundled converter `x2t`, run headless inside the
`org.onlyoffice.desktopeditors` flatpak.

```bash
tools/onlyoffice/x2t input.docx output.odt
tools/onlyoffice/x2t input.xlsx output.ods
tools/onlyoffice/x2t input.pptx output.odp
```

The output format follows the extension. Exit code 0 means the converter read and wrote the file; any other code
(for example 80 or 89) means it refused the input. The converter reads the file into OnlyOffice's own document model,
so a successful conversion to ODF is a faithful proof that OnlyOffice can open the file, and the ODF it writes can be
read back by PulseOffice or by the reference suite to compare text and values.

Conversion to PDF or to images does not work this way: those outputs need the editor's JavaScript renderer
(`DoctRenderer`), which fails without the font and SDK configuration the desktop app prepares at start-up.

The script does `cd` into the converter's folder and sets `LD_LIBRARY_PATH` there, because `x2t` finds its libraries
and `DoctRenderer.config` next to itself. The flatpak shares `/tmp` and the home folder, so input and output paths
can be anywhere in either. Nothing is installed or changed in the flatpak.

The reference suite has its own launcher, `tools/reference-suite/run`: a native installation when one exists
(macOS application bundle, `/usr/bin`, `/usr/local/bin`, `/opt`), otherwise its flatpak with `/tmp` shared.
